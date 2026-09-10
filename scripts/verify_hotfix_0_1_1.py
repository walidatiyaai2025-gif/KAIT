from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(message)


props = read("Directory.Build.props")
controller = read("src/GSIP.Web/Controllers/MojUatConfigurationController.cs")
view = read("src/GSIP.Web/Views/MojUatConfiguration/Index.cshtml")
token_contract = read("src/GSIP.Infrastructure/Execution/TokenEndpointContractMetadata.cs")
di = read("src/GSIP.Infrastructure/DependencyInjection.cs")
layout = read("src/GSIP.Web/Views/Shared/_Layout.cshtml")
baseline = read("tests/GSIP.BaselineChecks/Program.cs")
versioning = read("docs/BUILD_AND_VERSIONING.md")
p15 = read("scripts/verify_p15_installer.py")
p16 = read("scripts/verify_p16_acceptance_contract.py")
p16_build = read("scripts/build-p16-release-candidate.ps1")
official = json.loads(read("docs/moj-api-reference/p09/api-129-marriage-cases.contract.json"))

require("<VersionPrefix>0.1.2</VersionPrefix>" in props, "Hotfix version is not 0.1.2.")
require(
    "Current semantic version must be a stable x.y.z version at or above the initial 0.1.0 baseline." in baseline,
    "P00 patch-version compatibility repair is missing.",
)
require(
    'versioningDocument.Contains("Initial executable product version: **0.1.0**."' in baseline,
    "P00 no longer proves the immutable initial-version record.",
)
require(
    "Initial executable product version: **0.1.0**." in versioning,
    "Initial 0.1.0 version provenance was lost.",
)

operations = official["operations"]
token_operation = next(item for item in operations if item["relativePath"] == "/genToken")
target_operation = next(item for item in operations if item["relativePath"] == "/marriageCasesAPIGEE")
request_fields = {item["name"]: item for item in token_operation["requestFields"]}
require(request_fields["username"]["required"] is True, "Official API129 username is not required.")
require(request_fields["password"]["required"] is True, "Official API129 password is not required.")
require(
    {item["headerName"].lower() for item in token_operation["authentication"]} == {"x-api-key"},
    "Official API129 token authentication drifted.",
)
require(
    {item["headerName"].lower() for item in target_operation["authentication"]} == {"x-api-key", "authorization"},
    "Official API129 target composite authentication drifted.",
)

require('[Route("moj-uat")]' in controller, "MOJ UAT configuration route is missing.")
require("GsipPermissions.ServiceSecretsManage" in controller, "Secret-management authorization is missing.")
require("GsipPermissions.ServicesManage" in controller, "Service-management authorization is missing.")
require("AuthProfileType.TokenEndpoint" in controller, "TokenEndpoint profile restoration is missing.")
require('private const string TokenPath = "/genToken";' in controller, "Exact /genToken path is missing.")
require('private const string ApiKeySecretName = "x-api-key";' in controller, "Exact x-api-key slot is missing.")
require('private const string UsernameSecretName = "username";' in controller, "Required username slot is missing.")
require('private const string PasswordSecretName = "password";' in controller, "Required password slot is missing.")
require("string username" in controller and "string password" in controller, "API129 UAT admin does not require username/password input.")
require('"X-GSIP-TokenApiKeyRequired"] = true' in controller, "API129 token metadata does not require x-api-key.")
require("X-GSIP-TokenUsernameRequired" not in controller, "API129 must not override required username as optional.")
require("X-GSIP-TokenPasswordRequired" not in controller, "API129 must not override required password as optional.")
require("EmptyCredentialSentinel" not in controller, "API129 must not synthesize empty required credentials.")
require("CreateActiveAsync" in controller and "SetSecretReferenceAsync" in controller, "Protected secret storage is missing.")
require("SecretRotationSafety.RotateAsync" in controller, "Atomic secret rotation is missing.")
require("CryptographicOperations.ZeroMemory(clearBytes)" in controller, "Cleartext secret memory zeroing is missing.")
require("SetEnabledAsync(profile.Id, false" in controller, "Fail-closed reconfiguration is missing.")
require("SetEnabledAsync(profile.Id, true" in controller, "Profile enablement after successful configuration is missing.")
require("CatalogEnvironmentCodes.ProductionId" not in controller, "UAT hotfix must not mutate Production configuration.")
require("profile.Secrets.Count != 3" in controller, "API129 token profile must require exactly username/password/x-api-key.")
require("BeginTransactionAsync(IsolationLevel.Serializable" in controller, "Legacy auth transition lost serializable protection.")
require("transitionTransaction.RollbackAsync(CancellationToken.None)" in controller, "Legacy auth transition lost rollback protection.")

require("UsernameRequiredKey" not in token_contract, "Generic token runtime must not introduce undocumented optional username semantics.")
require("PasswordRequiredKey" not in token_contract, "Generic token runtime must not introduce undocumented optional password semantics.")
require("EmptyCredentialSentinel" not in token_contract, "Generic token runtime must not synthesize empty credentials.")
require("ApiKeyRequiredKey" in token_contract, "Token contract lost evidence-bound API-key support.")
require("MojTokenFlowConvergenceService" not in di, "Broad startup MOJ auth convergence must not be registered.")
require(not (ROOT / "src/GSIP.Infrastructure/Metadata/MojTokenFlowConvergenceService.cs").exists(), "Broad startup MOJ auth convergence file must be removed.")

for input_name in ("apiKey", "username", "password"):
    require(f'name="{input_name}"' in view, f"Write-only {input_name} input is missing.")
require("/genToken" in view and "Bearer" in view, "UI does not describe the token exchange sequence.")
require("may be empty" not in view.lower() and "يمكن أن تكون فارغة" not in view, "UI incorrectly permits empty API129 credentials.")
require("Open service test" in view and "/execute?entityId=" in view, "Service execution handoff is missing.")
require("MOJ UAT" in layout and "/moj-uat" in layout, "MOJ UAT navigation entry is missing.")
require("version >= (0, 1, 0)" in p15, "P15 patch-version compatibility repair is missing.")
require("current_version" in p16 and "docs/releases/{current_version}" in p16, "P16 current-version release binding is missing.")
require("$expectedVersion" in p16_build and "manifest.Version -ne $expectedVersion" in p16_build, "P16 package version binding is missing.")

for release_file in (
    "docs/releases/0.1.2/RELEASE_NOTES.md",
    "docs/releases/0.1.2/DATABASE_MIGRATION_NOTES.md",
    "docs/releases/0.1.2/DEPLOYMENT_ROLLBACK_RUNBOOK.md",
    "docs/releases/0.1.2/UAT_CHECKLIST.md",
):
    text = read(release_file)
    require("0.1.2" in text, f"Hotfix release document is not version-bound: {release_file}")
    require("may be empty" not in text.lower(), f"Release document incorrectly permits empty API129 credentials: {release_file}")

for forbidden in (
    "Bearer eyJ",
    "-----BEGIN PRIVATE KEY-----",
):
    require(forbidden not in controller and forbidden not in view, f"Forbidden credential-like literal found: {forbidden}")

print("HOTFIX_0_1_2_ACCEPTANCE=PASS")
