#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SNAPSHOT_DIR = ROOT / "docs" / "moj-api-reference" / "p09"
EXPECTED = {
    129: ("Marriage Cases Service", "api-129-marriage-cases.contract.json", "PARTIAL_PROVEN"),
    132: ("Is Single Basic Service", "api-132-is-single-basic.contract.json", "DEFERRED_EXTERNAL"),
    130: ("Marriage Couple Last Case Service", "api-130-marriage-couple-last-case.contract.json", "DEFERRED_EXTERNAL"),
    196: ("Family Judgment Text Service", "api-196-family-judgment-text.contract.json", "DEFERRED_EXTERNAL"),
    134: ("Procuration Status Service", "api-134-procuration-status.contract.json", "DEFERRED_EXTERNAL"),
}

def fail(message: str) -> None:
    raise SystemExit(f"P09_CONTRACT_SNAPSHOT_VALIDATION=FAIL: {message}")

def require(condition: bool, message: str) -> None:
    if not condition:
        fail(message)

def load_json(path: Path):
    if not path.is_file():
        fail(f"missing required snapshot file: {path.relative_to(ROOT)}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        fail(f"invalid JSON in {path.relative_to(ROOT)}: {exc}")

manifest = load_json(SNAPSHOT_DIR / "manifest.json")
require(manifest.get("schemaVersion") == 1, "manifest schemaVersion must be 1")
require(manifest.get("phase") == "P09", "manifest phase must be P09")
require(manifest.get("unit") == "P09::official-contract-capture", "manifest unit drifted")
services = manifest.get("services")
require(isinstance(services, list) and len(services) == 5, "manifest must contain exactly five canonical services")
seen_ids = set()
for item in services:
    api_id = item.get("apiId")
    require(api_id in EXPECTED, f"unexpected API id in manifest: {api_id}")
    require(api_id not in seen_ids, f"duplicate API id in manifest: {api_id}")
    seen_ids.add(api_id)
    expected_name, expected_file, expected_status = EXPECTED[api_id]
    require(item.get("serviceName") == expected_name, f"service name drift for API {api_id}")
    require(item.get("officialSource") == f"https://developer.api.cait.gov.kw/api/{api_id}", f"official source drift for API {api_id}")
    require(item.get("snapshot") == expected_file, f"snapshot filename drift for API {api_id}")
    require(item.get("captureStatus") == expected_status, f"capture status drift for API {api_id}")
require(seen_ids == set(EXPECTED), "manifest canonical service set drifted")
actual_contracts = {p.name for p in SNAPSHOT_DIR.glob("*.contract.json")}
expected_contracts = {value[1] for value in EXPECTED.values()}
require(actual_contracts == expected_contracts, "contract file set must match the five canonical services exactly")

docs = {}
for api_id, (expected_name, expected_file, expected_status) in EXPECTED.items():
    doc = load_json(SNAPSHOT_DIR / expected_file)
    docs[api_id] = doc
    require(doc.get("schemaVersion") == 1, f"schemaVersion drift for API {api_id}")
    require(doc.get("apiId") == api_id, f"apiId drift for API {api_id}")
    require(doc.get("serviceName") == expected_name, f"serviceName drift for API {api_id}")
    require(doc.get("officialSource") == f"https://developer.api.cait.gov.kw/api/{api_id}", f"officialSource drift for API {api_id}")
    require(doc.get("captureStatus") == expected_status, f"captureStatus drift for API {api_id}")

marriage = docs[129]
require(marriage.get("evidence", {}).get("environmentScope") == "UAT_ONLY", "API 129 evidence scope must remain UAT_ONLY")
envs = marriage.get("environments", {})
require(envs.get("uat", {}).get("status") == "PROVEN", "API 129 UAT base status must remain PROVEN")
require(envs.get("uat", {}).get("baseUrl") == "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage", "API 129 UAT base URL drifted")
require(envs.get("production", {}).get("status") == "DEFERRED_EXTERNAL", "API 129 Production detail must remain DEFERRED_EXTERNAL")
require(envs.get("production", {}).get("baseUrl") is None, "API 129 must not invent a Production service base/path")
operations = marriage.get("operations")
require(isinstance(operations, list) and len(operations) == 2, "API 129 must contain exactly the two owner-proven UAT operations")
by_path = {op.get("relativePath"): op for op in operations}
require(set(by_path) == {"/genToken", "/marriageCasesAPIGEE"}, "API 129 operation path set drifted")
token = by_path["/genToken"]
require(token.get("method") == "POST", "/genToken method drifted")
require(token.get("requestContentType") == "application/x-www-form-urlencoded", "/genToken content type drifted")
require(token.get("authentication") == [{"scheme":"ApiKeyHeader","headerName":"x-api-key","status":"PROVEN_UAT"}], "/genToken authentication drifted")
require([(x.get("name"),x.get("type"),x.get("required")) for x in token.get("requestFields",[])] == [("username","string",True),("password","string",True)], "/genToken request fields drifted")
success = token.get("successResponses")
require(isinstance(success,list) and len(success)==1 and success[0].get("statusCode")==200, "/genToken proven HTTP 200 status drifted")
schema_fields = success[0].get("schema",{}).get("fields")
require(isinstance(schema_fields,list) and len(schema_fields)==1 and schema_fields[0].get("name")=="data" and schema_fields[0].get("type")=="string", "/genToken response.data shape drifted")
require(token.get("documentedErrorResponses",{}).get("status")=="DEFERRED_EXTERNAL", "/genToken error schema must remain deferred until officially captured")
require(token.get("readOnlyNonDestructive",{}).get("status")=="DEFERRED_EXTERNAL", "/genToken read-only classification must not be inferred")
target = by_path["/marriageCasesAPIGEE"]
require(target.get("method")=="POST", "Marriage target method drifted")
require(target.get("requestContentType")=="application/x-www-form-urlencoded", "Marriage target content type drifted")
require(target.get("authentication") == [{"scheme":"ApiKeyHeader","headerName":"x-api-key","status":"PROVEN_UAT"},{"scheme":"Bearer","headerName":"Authorization","status":"PROVEN_UAT"}], "Marriage target authentication drifted")
target_fields = target.get("requestFields")
require(isinstance(target_fields,list) and len(target_fields)==1 and target_fields[0].get("name")=="civilId" and target_fields[0].get("required") is True and target_fields[0].get("type") is None and target_fields[0].get("typeStatus")=="DEFERRED_EXTERNAL", "Marriage target request-field evidence drifted or invented a type")
for key in ("successResponses","documentedErrorResponses","responseFields","resultMappingCandidates","readOnlyNonDestructive"):
    require(target.get(key,{}).get("status")=="DEFERRED_EXTERNAL", f"Marriage target {key} must remain deferred until official schema evidence exists")

required_deferred_items = {"uatServerBaseUrl","productionServerBaseUrl","httpMethod","relativePath","requestContentType","requestFields","operationAuthentication","successStatusesAndSchema","errorStatusesAndSchemas","responseFields","resultMappingCandidates","readOnlyNonDestructive"}
for api_id in (132,130,196,134):
    doc = docs[api_id]
    require(doc.get("evidence",{}).get("classification")=="OFFICIAL_CAIT_SPECIFICATION_NOT_RETRIEVABLE_WITHOUT_SIGN_IN", f"API {api_id} evidence classification drifted")
    items = doc.get("contractItems")
    require(isinstance(items,dict) and set(items)==required_deferred_items, f"API {api_id} deferred contract-item set drifted")
    for key,item in items.items():
        require(item.get("status")=="DEFERRED_EXTERNAL", f"API {api_id} {key} was promoted without official evidence")

raw = "\n".join(path.read_text(encoding="utf-8") for path in SNAPSHOT_DIR.glob("*.json"))
require(not re.search(r'"examples?"\s*:', raw, re.IGNORECASE), "example values are prohibited in sanitized P09 snapshots")
require(not re.search(r'\b\d{12}\b', raw), "12-digit personal identifier shaped value found in P09 snapshots")
require(not re.search(r'Authorization\s*:\s*Bearer\s+[A-Za-z0-9._~+/-]{16,}', raw, re.IGNORECASE), "Bearer credential-shaped material found in P09 snapshots")
require(not re.search(r'x-api-key\s*[:=]\s*[A-Za-z0-9._~+/-]{16,}', raw, re.IGNORECASE), "API-key credential-shaped material found in P09 snapshots")
require(not re.search(r'password\s*[:=]\s*[^\s"`|]{8,}', raw, re.IGNORECASE), "password credential-shaped material found in P09 snapshots")
print("P09_CONTRACT_SNAPSHOT_VALIDATION=PASS")
print("P09_CANONICAL_SERVICES=5")
print("P09_API129_UAT_EVIDENCE=PARTIAL_PROVEN")
print("P09_EXTERNAL_CONTRACTS_DEFERRED=4")
