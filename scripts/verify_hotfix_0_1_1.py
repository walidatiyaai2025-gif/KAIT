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
layout = read("src/GSIP.Web/Views/Shared/_Layout.cshtml")
p15 = read("scripts/verify_p15_installer.py")
p16 = read("scripts/verify_p16_acceptance_contract.py")
p16_build = read("scripts/build-p16-release-candidate.ps1")

require("<VersionPrefix>0.1.1</VersionPrefix>" in props, "Hotfix version is not 0.1.1.")
require('[Route("moj-uat")]' in controller, "MOJ UAT configuration route is missing.")
require("GsipPermissions.ServiceSecretsManage" in controller, "Secret-management authorization is missing.")
require("GsipPermissions.ServicesManage" in controller, "Service-management authorization is missing.")
require("AuthProfileType.ApiKeyHeader" in controller, "API-key-only profile conversion is missing.")
require('private const string ApiKeySecretName = "x-api-key";' in controller, "Exact x-api-key slot is missing.")
require('config.NonSecretHeadersJson = "{}";' in controller, "TokenEndpoint metadata is not removed for API-key-only UAT execution.")
require("CreateActiveAsync" in controller and "SetSecretReferenceAsync" in controller, "Protected initial secret storage is missing.")
require("SecretRotationSafety.RotateAsync" in controller, "Atomic API-key rotation is missing.")
require("CryptographicOperations.ZeroMemory(clearBytes)" in controller, "Cleartext API-key memory zeroing is missing.")
require("SetEnabledAsync(profile.Id, false" in controller, "Fail-closed reconfiguration is missing.")
require("SetEnabledAsync(profile.Id, true" in controller, "Profile enablement after successful configuration is missing.")
require("CatalogEnvironmentCodes.ProductionId" not in controller, "Hotfix must not mutate Production configuration.")
require('type="password"' in view and 'name="apiKey"' in view, "Write-only API-key input is missing.")
require("Open service test" in view and "/execute?entityId=" in view, "Service execution handoff is missing.")
require("MOJ UAT" in layout and "/moj-uat" in layout, "MOJ UAT navigation entry is missing.")
require("version >= (0, 1, 0)" in p15, "P15 patch-version compatibility repair is missing.")
require("current_version" in p16 and "docs/releases/{current_version}" in p16, "P16 current-version release binding is missing.")
require("$expectedVersion" in p16_build and "manifest.Version -ne $expectedVersion" in p16_build, "P16 package version binding is missing.")

for release_file in (
    "docs/releases/0.1.1/RELEASE_NOTES.md",
    "docs/releases/0.1.1/DATABASE_MIGRATION_NOTES.md",
    "docs/releases/0.1.1/DEPLOYMENT_ROLLBACK_RUNBOOK.md",
    "docs/releases/0.1.1/UAT_CHECKLIST.md",
):
    text = read(release_file)
    require("0.1.1" in text, f"Hotfix release document is not version-bound: {release_file}")

for forbidden in (
    "Bearer eyJ",
    "PasswordHash =",
    "-----BEGIN PRIVATE KEY-----",
):
    require(forbidden not in controller and forbidden not in view, f"Forbidden credential-like literal found: {forbidden}")

print("HOTFIX_0_1_1_ACCEPTANCE=PASS")
