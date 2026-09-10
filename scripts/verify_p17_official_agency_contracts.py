#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SEED = ROOT / "src/GSIP.Infrastructure/Metadata/OfficialAgencyMetadataSeedService.cs"
TOKEN = ROOT / "src/GSIP.Infrastructure/Execution/TokenEndpointContractMetadata.cs"
BOOT = ROOT / "src/GSIP.Infrastructure/Metadata/GreenGovernmentCatalogBootstrapService.cs"
MOH_DOC = ROOT / "docs/MOH_UAT_CONTRACT_MATRIX.md"
CSC_DOC = ROOT / "docs/CSC_UAT_CONTRACT_MATRIX.md"

for path in (SEED, TOKEN, BOOT, MOH_DOC, CSC_DOC):
    if not path.is_file():
        raise SystemExit(f"missing required P17 official-contract artifact: {path.relative_to(ROOT)}")

seed = SEED.read_text(encoding="utf-8")
token = TOKEN.read_text(encoding="utf-8")
boot = BOOT.read_text(encoding="utf-8")
docs = MOH_DOC.read_text(encoding="utf-8") + "\n" + CSC_DOC.read_text(encoding="utf-8")

required_seed_fragments = [
    '"MOH_CERTIFICATE_INFORMATION"',
    '"MOH_DEATH_CERTIFICATE_CIVIL_ID"',
    '"MOH_DEATH_CERTIFICATE_PASSPORT"',
    '"MOH_MEDICAL_LICENSE_INSTITUTION"',
    '"CSC_EMPLOYEE_DATA"',
    '"CSC_EMPLOYEE_FINANCIAL_DATA"',
    '"CSC_EMPLOYEE_SALARY_DETAILS"',
    '"https://moh-uat.api-non-prod.cait.gov.kw/prmapi/moj/v1"',
    '"https://moh-uat.api-non-prod.cait.gov.kw/api/v1"',
    '"https://moh-uat.api-non-prod.cait.gov.kw/licenseservice/api/v1"',
    '"https://csc-uat.api-non-prod.cait.gov.kw"',
    '"https://csc-uat.api-non-prod.cait.gov.kw/csc"',
    '"/certificate"',
    '"/inquiry/civil-id"',
    '"/inquiry/passport"',
    '"/institutions"',
    '"/Mvcsc/EmployeeProfile/empAllData"',
    '"/Mvcsc/v1/EmployeeProfile/financial"',
    '"/v1/creditBank/empSalDets"',
    '"/csc/token/generate"',
    '"/token/generate"',
    '"response.token"',
    '"accessToken"',
    '"data"',
    'AuthType = AuthProfileType.TokenEndpoint',
    'IsEnabled = false',
    'Active = false',
    'LastTestStatus = "DEFERRED_EXTERNAL_PRODUCTION_CONTRACT"',
    'BaseUrl = string.Empty',
]
for fragment in required_seed_fragments:
    if fragment not in seed:
        raise SystemExit(f"official contract seed drift: missing {fragment}")

required_token_fragments = [
    'GehaValueTypeKey = "X-GSIP-TokenGehaValueType"',
    '"integer"',
    'long.TryParse',
    'ParseGehaJsonValue',
]
for fragment in required_token_fragments:
    if fragment not in token:
        raise SystemExit(f"typed token auxiliary credential support drift: missing {fragment}")

if "OfficialAgencyMetadataSeedService" not in boot or boot.find("GreenGovernmentCatalogSeedService") > boot.find("OfficialAgencyMetadataSeedService"):
    raise SystemExit("official contract overlay must run after the green catalog seed")

# The owner-supplied Swagger contained example credential material. Contracts may record
# field names and schemas, but repository artifacts must never embed credential values.
suspicious_literals = [
    '"password": "Test',
    '"username": "Test"',
    'Bearer eyJ',
]
for literal in suspicious_literals:
    if literal in seed or literal in docs:
        raise SystemExit("credential-like example material must not be committed")

if "Production remains" not in docs and "Production remains" not in CSC_DOC.read_text(encoding="utf-8"):
    raise SystemExit("Production fail-closed documentation is missing")

print("P17 official MOH/CSC contract seed verification: PASS")
