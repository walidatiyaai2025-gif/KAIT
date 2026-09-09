#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SNAPSHOT_DIR = ROOT / "docs" / "moj-api-reference" / "p09"
EXPECTED = {
    129: ("Marriage Cases Service", "api-129-marriage-cases.contract.json"),
    132: ("Is Single Basic Service", "api-132-is-single-basic.contract.json"),
    130: ("Marriage Couple Last Case Service", "api-130-marriage-couple-last-case.contract.json"),
    196: ("Family Judgment Text Service", "api-196-family-judgment-text.contract.json"),
    134: ("Procuration Status Service", "api-134-procuration-status.contract.json"),
}
MARRIAGE_BASE = "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/marriage"
VERDICT_BASE = "https://moj-uat.api-non-prod.cait.gov.kw/WSWEB/WS/v1/verdict"
PROCURATION_BASE = "https://moj-uat.api-non-prod.cait.gov.kw/MOJProcuration/V1/api"
PRODUCTION_PREFIX = "https://moj.api.cait.gov.kw"


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


def fields(operation):
    return [(x.get("name"), x.get("type")) for x in operation.get("requestFields", [])]


def codes(operation):
    return [x.get("statusCode") for x in operation.get("responses", [])]

manifest = load_json(SNAPSHOT_DIR / "manifest.json")
require(manifest.get("schemaVersion") == 1 and manifest.get("phase") == "P09", "manifest identity drifted")
require(manifest.get("unit") == "P09::official-contract-convergence", "manifest unit must identify convergence")
services = manifest.get("services")
require(isinstance(services, list) and len(services) == 5, "manifest must contain exactly five services")
seen = set()
for item in services:
    api_id = item.get("apiId")
    require(api_id in EXPECTED and api_id not in seen, f"unexpected/duplicate API id {api_id}")
    seen.add(api_id)
    name, filename = EXPECTED[api_id]
    require(item.get("serviceName") == name, f"service name drift for API {api_id}")
    require(item.get("officialSource") == f"https://developer.api.cait.gov.kw/api/{api_id}", f"official source drift for API {api_id}")
    require(item.get("snapshot") == filename, f"snapshot filename drift for API {api_id}")
    require(item.get("captureStatus") == "PROVEN_UAT_CONTRACT", f"API {api_id} UAT contract is not reconciled")
require(seen == set(EXPECTED), "canonical API set drifted")

contracts = {}
for api_id, (name, filename) in EXPECTED.items():
    doc = load_json(SNAPSHOT_DIR / filename)
    contracts[api_id] = doc
    require(doc.get("schemaVersion") == 1 and doc.get("apiId") == api_id, f"snapshot identity drift for API {api_id}")
    require(doc.get("serviceName") == name, f"snapshot service-name drift for API {api_id}")
    require(doc.get("officialSource") == f"https://developer.api.cait.gov.kw/api/{api_id}", f"snapshot source drift for API {api_id}")
    require(doc.get("captureStatus") == "PROVEN_UAT_CONTRACT", f"API {api_id} must be PROVEN_UAT_CONTRACT")
    prod = doc.get("environments", {}).get("production", {})
    require(prod.get("status") == "DEFERRED_EXTERNAL", f"API {api_id} Production must remain deferred")
    require(prod.get("gatewayPrefix") == PRODUCTION_PREFIX, f"API {api_id} Production gateway prefix drifted")
    require(prod.get("baseUrl") is None, f"API {api_id} must not invent Production service URL")

api129 = contracts[129]
require(api129["environments"]["uat"]["baseUrl"] == MARRIAGE_BASE, "API129 UAT base drifted")
ops129 = {op["relativePath"]: op for op in api129["operations"]}
require(set(ops129) == {"/genToken", "/marriageCasesAPIGEE"}, "API129 paths drifted")
require(fields(ops129["/genToken"]) == [("username", "string"), ("password", "string")], "API129 token fields drifted")
require(codes(ops129["/genToken"]) == [200, 401], "API129 token statuses drifted")
require(fields(ops129["/marriageCasesAPIGEE"]) == [("civilId", "string")], "API129 target fields drifted")
require(codes(ops129["/marriageCasesAPIGEE"]) == [200, 400, 401, 404], "API129 target statuses drifted")
require([x["path"] for x in ops129["/marriageCasesAPIGEE"]["responseFields"]][:4] == ["data[0].CIVILID", "data[0].CONTRACT_DATE", "data[0].CONTRACT_NO", "data[0].CONTRACT_TYPE"], "API129 response mapping drifted")

api132 = contracts[132]
require(api132["environments"]["uat"]["baseUrl"] == MARRIAGE_BASE, "API132 UAT base drifted")
require(api132["tokenContract"]["relativePath"] == "/genToken" and api132["tokenContract"]["tokenResponsePath"] == "data", "API132 token contract drifted")
require(api132["operation"]["relativePath"] == "/isSingleBasicAPIGEE", "API132 target path drifted")
require(fields(api132["operation"]) == [("civilId", "string")], "API132 request fields drifted")
require(codes(api132["operation"]) == [200, 401, 404], "API132 documented statuses drifted")

api130 = contracts[130]
require(api130["environments"]["uat"]["baseUrl"] == MARRIAGE_BASE, "API130 UAT base drifted")
require(api130["operation"]["relativePath"] == "/marriageCoupleLastAPIGEE", "API130 path drifted")
require(fields(api130["operation"]) == [("male_civilId", "string"), ("female_civilId", "string")], "API130 exact request key casing drifted")
require(codes(api130["operation"]) == [200, 400, 401, 404], "API130 documented statuses drifted")

api196 = contracts[196]
require(api196["environments"]["uat"]["baseUrl"] == VERDICT_BASE and api196["environments"]["uat"]["targetType"] == "MOCK", "API196 UAT mock/base drifted")
require(api196["tokenContract"]["relativePath"] == "/token" and api196["tokenContract"]["requestContentType"] == "application/json", "API196 token request drifted")
require(api196["tokenContract"]["tokenResponsePath"] == "token", "API196 token response path drifted")
require(fields(api196["operation"]) == [("caseNo", "string"), ("type", "string")], "API196 request fields drifted")
require(api196["operation"]["relativePath"] == "/familyJudgmentText" and codes(api196["operation"]) == [200, 400, 401, 403, 404, 500], "API196 operation contract drifted")
require(api196["operation"]["responseFields"] == [{"path":"data.VRDESC","type":"string","sensitive":True}], "API196 result mapping drifted")

api134 = contracts[134]
require(api134["environments"]["uat"]["baseUrl"] == PROCURATION_BASE and api134["environments"]["uat"]["targetType"] == "MOCK", "API134 UAT mock/base drifted")
require(api134["tokenContract"]["relativePath"] == "/Authenticate/Token" and api134["tokenContract"]["requestContentType"] == "application/json", "API134 token endpoint drifted")
require(api134["tokenContract"]["tokenResponsePath"] == "token", "API134 token response path drifted")
require(api134["tokenContract"]["tokenLifetime"] == {"status":"PROVEN","seconds":21600}, "API134 6-hour token lifetime drifted")
require([(x.get("name"), x.get("canonicalSecretName")) for x in api134["tokenContract"]["requestFields"]] == [("UserName","username"),("Password","password"),("Geha","geha")], "API134 token credential mapping drifted")
require(fields(api134["operation"]) == [("CivilClient", "string"), ("CivilAgent", "string"), ("year", "string"), ("Number", "string")], "API134 request field casing drifted")
require(api134["operation"]["relativePath"] == "/Procuration/ProcurationStatus" and codes(api134["operation"]) == [200, 400, 401, 404], "API134 operation contract drifted")

raw = "\n".join(path.read_text(encoding="utf-8") for path in SNAPSHOT_DIR.glob("*.json"))
require(not re.search(r'"examples?"\s*:', raw, re.IGNORECASE), "example values are prohibited in sanitized snapshots")
require(not re.search(r'\b\d{12}\b', raw), "personal identifier-shaped value found")
require(not re.search(r'Authorization\s*:\s*Bearer\s+[A-Za-z0-9._~+/-]{16,}', raw, re.IGNORECASE), "Bearer credential-shaped material found")
require(not re.search(r'x-api-key\s*[:=]\s*[A-Za-z0-9._~+/-]{16,}', raw, re.IGNORECASE), "API-key credential-shaped material found")
require(not re.search(r'password\s*[:=]\s*[^\s\"`|]{8,}', raw, re.IGNORECASE), "password credential-shaped material found")

print("P09_CONTRACT_SNAPSHOT_VALIDATION=PASS")
print("P09_CANONICAL_SERVICES=5")
print("P09_UAT_CONTRACTS_RECONCILED=5")
print("P09_PRODUCTION_OPERATION_CONTRACTS=DEFERRED_EXTERNAL")
print("P09_API196_UAT_TARGET=MOCK")
print("P09_API134_UAT_TARGET=MOCK")
