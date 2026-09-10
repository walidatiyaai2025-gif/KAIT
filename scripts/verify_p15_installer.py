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
build = read("scripts/build-p15-package.ps1")
workflow = read(".github/workflows/p15-installer-packaging.yml")
program = read("src/GSIP.Web/Program.cs")
runtime_db = read("src/GSIP.Infrastructure/Identity/RuntimeDatabaseConnection.cs")
phase = read("CURRENT_PHASE.md")
props = read("Directory.Build.props")

require("<TargetFramework>net10.0-windows</TargetFramework>" in project, "setup executable is not pinned to the Windows .NET 10 target")
require("<SelfContained>true</SelfContained>" in project and "<PublishSingleFile>true</PublishSingleFile>" in project, "setup executable is not self-contained single-file")
require("GSIP.Payload.zip" in project and "GSIP.Payload.zip" in setup, "versioned application payload is not embedded into setup")
require("App_Data" in setup and "BackupReplaceableApplicationFiles" in setup and "RemoveReplaceableApplicationFiles" in setup, "mutable App_Data preservation boundary is missing")
require("--purge-state" in setup and "action != SetupAction.Uninstall" in setup, "state purge is not explicit uninstall-only behavior")
require("RollBackApplicationFiles" in setup and "backupDirectory" in setup, "repair/upgrade rollback path is missing")
require("completed.protected" in runtime_db and 'Path.Combine(environment.ContentRootPath, "App_Data", "setup", "completed.protected")' in runtime_db, "protected database setup state path changed unexpectedly")
require('Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")' in program and "ProtectKeysWithDpapi" in program, "Data Protection key persistence/DPAPI protection is not preserved")
require("aspnetcorev2" in setup.lower() and "Hosting Bundle" in setup, "IIS Hosting Bundle prerequisite is not enforced")
require("appcmd.exe" in setup.lower() and "ApplicationPoolIdentity" in setup and "/managedRuntimeVersion:" in setup, "IIS application-pool deployment contract is incomplete")
require("IIS AppPool" in setup and "(OI)(CI)M" in setup and "icacls.exe" in setup, "App_Data does not receive exact app-pool write access")
require("GSIP-$version-win-x64.zip" in build and "GSIP-$version-Setup-x64.exe" in build, "versioned artifact names do not match build contract")
require("Get-FileHash" in build and "SHA256" in build and ".sha256" in build, "SHA-256 artifact evidence is missing")
require("completed.protected" in build and "App_Data" in build and "forbiddenExtensions" in build, "package secret/state exclusion gate is missing")
require("CANDIDATE_SHA" in workflow and "ref: ${{ env.CANDIDATE_SHA }}" in workflow and "git rev-parse HEAD" in workflow, "P15 workflow is not bound to exact candidate SHA")
require("install --skip-iis" in workflow and "repair --skip-iis" in workflow and "uninstall --skip-iis" in workflow, "isolated install/repair/uninstall lifecycle acceptance is incomplete")
require("completed.protected" in workflow and "App_Data/keys" in workflow, "lifecycle acceptance does not prove setup state and Data Protection key preservation")
require("--purge-state" in workflow, "explicit destructive purge path is not acceptance-tested")
require("P14" in phase and "CLOSED" in phase.upper() and "P15" in phase, "canonical phase authority does not preserve P14 closure and identify P15")
require(re.search(r"<VersionPrefix>0\.1\.0</VersionPrefix>", props) is not None, "initial executable version drifted from 0.1.0")
require("DEFERRED_EXTERNAL_NOT_PASS" in phase and "PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS" in phase, "historical external NOT-PASS classifications were lost")
require("OWNER_LAST / NOT PASS" in phase, "repository-administration NOT-PASS boundary was lost")

print(f"P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks={checks}")
