from pathlib import Path

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
convergence = read("src/GSIP.Infrastructure/Metadata/MojTokenFlowConvergenceService.cs")
di = read("src/GSIP.Infrastructure/DependencyInjection.cs")
layout = read("src/GSIP.Web/Views/Shared/_Layout.cshtml")
baseline = read("tests/GSIP.BaselineChecks/Program.cs")
versioning = read("docs/BUILD_AND_VERSIONING.md")
p15 = read("scripts/verify_p15_installer.py")
p16 = read("scripts/verify_p16_acceptance_contract.py")
p16_build = read("scripts/build-p16-release-candidate.ps1")

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

require('[Route("moj-uat")]' in controller, "MOJ UAT configuration route is missing.")
require("GsipPermissions.ServiceSecretsManage" in controller, "Secret-management authorization is missing.")
require("GsipPermissions.ServicesManage" in controller, "Service-management authorization is missing.")
require("AuthProfileType.TokenEndpoint" in controller, "TokenEndpoint profile restoration is missing.")
require('private const string TokenPath = "/genToken";' in controller, "Exact /genToken path is missing.")
require('private const string ApiKeySecretName = "x-api-key";' in controller, "Exact x-api-key slot is missing.")
require('"X-GSIP-TokenApiKeyRequired"] = true' in controller, "API key is not required by API129 token metadata.")
require('"X-GSIP-TokenUsernameRequired"] = false' in controller, "UAT optional username metadata is missing.")
require('"X-GSIP-TokenPasswordRequired"] = false' in controller, "UAT optional password metadata is missing.")
require("CreateActiveAsync" in controller and "SetSecretReferenceAsync" in controller, "Protected secret storage is missing.")
require("SecretRotationSafety.RotateAsync" in controller, "Atomic API-key rotation is missing.")
require("CryptographicOperations.ZeroMemory(clearBytes)" in controller, "Cleartext API-key memory zeroing is missing.")
require("SetEnabledAsync(profile.Id, false" in controller, "Fail-closed reconfiguration is missing.")
require("SetEnabledAsync(profile.Id, true" in controller, "Profile enablement after successful configuration is missing.")
require("CatalogEnvironmentCodes.ProductionId" not in controller, "UAT hotfix must not mutate Production configuration.")
require("profile.Secrets.Count != 3" in controller, "API129 token profile must converge to username/password/x-api-key.")

require("UsernameRequiredKey" in token_contract and "PasswordRequiredKey" in token_contract, "Optional credential metadata keys are missing.")
require("EmptyCredentialSentinel" in token_contract, "Protected empty-UAT credential sentinel is missing.")
require("return string.Empty;" in token_contract, "Optional UAT credentials are not emitted as empty wire values.")
require("ApiKeyRequiredKey" in token_contract, "Token contract lost API-key support.")

require("MojMetadataSeedService.CanonicalServiceCodes" in convergence, "All canonical MOJ services are not covered by convergence.")
require("ApiKeyRequiredKey] = true" in convergence, "MOJ UAT convergence does not require x-api-key.")
require("UsernameRequiredKey] = false" in convergence, "MOJ UAT convergence does not allow empty username.")
require("PasswordRequiredKey] = false" in convergence, "MOJ UAT convergence does not allow empty password.")
require("AuthProfileType.TokenEndpoint" in convergence, "MOJ profiles are not converged to TokenEndpoint.")
require("EnsureSystemAdministratorExecutionPermissionsAsync" in convergence, "System Administrator execution entitlement repair is missing.")
require("MojTokenFlowConvergenceService" in di, "MOJ token-flow convergence is not registered at startup.")

require('type="password"' in view and 'name="apiKey"' in view, "Write-only API-key input is missing.")
require("/genToken" in view and "Bearer" in view, "UI does not describe the token exchange sequence.")
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

for forbidden in (
    "Bearer eyJ",
    "-----BEGIN PRIVATE KEY-----",
):
    require(forbidden not in controller and forbidden not in view, f"Forbidden credential-like literal found: {forbidden}")

print("HOTFIX_0_1_2_ACCEPTANCE=PASS")
