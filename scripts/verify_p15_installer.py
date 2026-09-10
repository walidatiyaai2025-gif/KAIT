#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
checks = 0


def read(path: str) -> str:
    p = ROOT / path
    if not p.is_file():
        raise SystemExit(f"P15 installer acceptance failed: missing {path}")
    return p.read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    global checks
    if not condition:
        raise SystemExit(f"P15 installer acceptance failed: {message}")
    checks += 1


setup = read("installer/GSIP.Setup/Program.cs")
project = read("installer/GSIP.Setup/GSIP.Setup.csproj")
entry = read("installer/GSIP.Setup/WizardEntryPoint.cs")
wizard = read("installer/GSIP.Setup/InstallerWizard.cs")
bindings = read("installer/GSIP.Setup/IisBindingConfigurator.cs")
install_log = read("installer/GSIP.Setup/SanitizedInstallLog.cs")
build = read("scripts/build-p15-package.ps1")
workflow = read(".github/workflows/p15-installer-packaging.yml")
runbook = read("docs/P15_INSTALLER_RUNBOOK.md")
program = read("src/GSIP.Web/Program.cs")
runtime_db = read("src/GSIP.Infrastructure/Identity/RuntimeDatabaseConnection.cs")
phase = read("CURRENT_PHASE.md")
ledger = read("docs/TASK_LEDGER.md")
project_control = read("PROJECT_CONTROL.md")
props = read("Directory.Build.props")

require("<TargetFramework>net10.0-windows</TargetFramework>" in project, "setup executable is not pinned to the Windows .NET 10 target")
require("<SelfContained>true</SelfContained>" in project and "<PublishSingleFile>true</PublishSingleFile>" in project, "setup executable is not self-contained single-file")
require("<UseWindowsForms>true</UseWindowsForms>" in project and "GSIP.Setup.WizardEntryPoint" in project, "professional Windows wizard is not the setup entry point")
require("GSIP.Payload.zip" in project and "GSIP.Payload.zip" in setup, "versioned application payload is not embedded into setup")
require("App_Data" in setup and "BackupReplaceableApplicationFiles" in setup and "RemoveReplaceableApplicationFiles" in setup, "mutable App_Data preservation boundary is missing")
require("--purge-state" in setup and "action != SetupAction.Uninstall" in setup, "state purge is not explicit uninstall-only behavior")
require("RollBackApplicationFiles" in setup and "backupDirectory" in setup, "repair/upgrade rollback path is missing")
require("ReadInstallOwnership" in setup and "RequireOwnershipMatch" in setup and "InstallManifestName" in setup, "install ownership marker enforcement is missing")
require("Install root is not empty and is not owned by GSIP" in setup and "Repair requires a valid GSIP ownership manifest" in setup, "unowned-root and repair fail-closed guards are missing")
require("Existing IIS site is not owned" in setup and "Existing IIS application pool is not owned" in setup, "existing IIS resource ownership collision guards are missing")
require("Uninstall requires a valid GSIP ownership manifest" in setup and "ownership manifest preserved" in setup, "destructive uninstall ownership proof or marker preservation is missing")
require("originalManifestText" in setup and "WriteInstallManifestText" in setup and "File.Move(temporaryPath, manifestPath, overwrite: true)" in setup, "ownership manifest write/rollback is not atomic across failed maintenance")
require("completed.protected" in runtime_db and 'Path.Combine(environment.ContentRootPath, "App_Data", "setup", "completed.protected")' in runtime_db, "protected database setup state path changed unexpectedly")
require('Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")' in program and "ProtectKeysWithDpapi" in program, "Data Protection key persistence/DPAPI protection is not preserved")
require("aspnetcorev2" in setup.lower() and "Hosting Bundle" in setup, "IIS Hosting Bundle prerequisite is not enforced")
require("appcmd.exe" in setup.lower() and "ApplicationPoolIdentity" in setup and "/managedRuntimeVersion:" in setup, "IIS application-pool deployment contract is incomplete")
require("IIS AppPool" in setup and "(OI)(CI)M" in setup and "icacls.exe" in setup, "App_Data does not receive exact app-pool write access")

require('Text = "Back"' in wizard and '"Next"' in wizard and '"Finish"' in wizard, "wizard does not expose Back/Next/Finish navigation")
require("Prerequisites" in wizard and "Install path" in wizard and "Application pool" in wizard, "wizard core deployment pages are incomplete")
require("IIS binding and HTTPS certificate" in wizard and "GetEligibleCertificates" in wizard, "wizard does not expose IIS binding and HTTPS certificate selection")
require("LocalMachine" in bindings and "StoreName.My" in bindings and "HasPrivateKey" in bindings, "HTTPS certificate selection is not constrained to usable Local Computer certificates")
require("netsh.exe" in bindings and "sslcert" in bindings and "certhash=" in bindings, "HTTPS certificate binding mechanism is missing")
require("SetHttpsSslFlags" in bindings and "sslFlags" in bindings and "hostnameport=" in bindings, "host-specific HTTPS does not explicitly enable IIS SNI")
require("VerifyBinding" in wizard and "VerifyBinding" in bindings, "wizard does not verify the requested IIS binding after deployment")
require("Launch First-Run Setup" in wizard and '"/setup"' in bindings and "UseShellExecute = true" in wizard, "wizard does not launch protected First-Run Setup")
require("--wizard" in entry and 'Verb = "runas"' in entry, "wizard startup/elevation path is incomplete")
require("--log-path" in entry and "SanitizedInstallLog" in entry, "setup entry point does not provide sanitized install logging")
require("[REDACTED]" in install_log and "authorization" in install_log.lower() and "x-api-key" in install_log.lower(), "installer-log redaction does not cover sensitive authentication material")

require("GSIP-$version-win-x64.zip" in build and "GSIP-$version-Setup-x64.exe" in build, "versioned artifact names do not match build contract")
require("Get-FileHash" in build and "SHA256" in build and ".sha256" in build, "SHA-256 artifact evidence is missing")
require("completed.protected" in build and "App_Data" in build and "forbiddenExtensions" in build, "package secret/state exclusion gate is missing")
require("CANDIDATE_SHA" in workflow and "ref: ${{ env.CANDIDATE_SHA }}" in workflow and "git rev-parse HEAD" in workflow, "P15 workflow is not bound to exact candidate SHA")
require("install --skip-iis" in workflow and "repair --skip-iis" in workflow and "uninstall --skip-iis" in workflow, "isolated install/repair/uninstall lifecycle acceptance is incomplete")
require("P15_UNOWNED_ROOT_PROTECTION=PASS" in workflow and "Invoke-SetupExpectFailure" in workflow, "unowned-root destructive negative acceptance is missing")
require("P15_OWNERSHIP_SCOPE_ISOLATION=PASS" in workflow and "P15_OWNERSHIP_MARKER_PRESERVED=PASS" in workflow, "ownership identity/marker lifecycle acceptance is missing")
require("completed.protected" in workflow and "App_Data/keys" in workflow, "lifecycle acceptance does not prove setup state and Data Protection key preservation")
require("--purge-state" in workflow, "explicit destructive purge path is not acceptance-tested")
require("0.0.9" in workflow and "P15_EXPLICIT_UPGRADE_FROM_PREVIOUS_VERSION=PASS" in workflow, "explicit previous-version upgrade acceptance is missing")
require("P15_SANITIZED_INSTALL_LOG=PASS" in workflow and "--log-path" in workflow, "sanitized installer-log acceptance is missing")
require("ownership manifest" in runbook.lower() and "SNI" in runbook, "installer runbook does not document ownership and host-specific HTTPS safety")
require("Next/Back/Finish" in runbook and "HTTPS certificate" in runbook and "First-Run Setup" in runbook, "installer runbook does not document the professional wizard contract")

legacy_p15_authority = "P14" in phase and "CLOSED" in phase.upper() and "P15" in phase
post_p15_authority = (
    "**P17 — Final convergence and release closure**" in phase
    and "Status: **OPEN / READY**" in phase
    and "P00-P16 are formally **CLOSED**" in phase
    and "P16 is formally **CLOSED**" in phase
    and "| P15 | CLOSED |" in ledger
    and "| P16 | CLOSED |" in ledger
    and "| P17 | OPEN / READY |" in ledger
    and "P00-P16 are formally CLOSED" in project_control
    and "P17" in project_control
    and "OPEN / READY" in project_control
)
require(legacy_p15_authority or post_p15_authority, "canonical phase authority does not preserve the closed P15 baseline through the governed current phase")
version_match = re.search(r"<VersionPrefix>(\d+)\.(\d+)\.(\d+)</VersionPrefix>", props)
require(version_match is not None, "executable version is missing or is not semantic x.y.z")
version = tuple(int(part) for part in version_match.groups()) if version_match else (0, 0, 0)
require(version >= (0, 1, 0), "executable version regressed below the accepted 0.1.0 baseline")
require("DEFERRED_EXTERNAL_NOT_PASS" in phase and "PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS" in phase, "historical external NOT-PASS classifications were lost")
require(
    "OWNER_LAST / NOT PASS" in phase or "OWNER_LAST_BRANCH_PROTECTION_NOT_PASS" in phase,
    "repository-administration NOT-PASS boundary was lost",
)

print(f"P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks={checks}")
print(f"P15_VERSION_BASELINE_PRESERVED={'.'.join(str(part) for part in version)}")
