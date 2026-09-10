from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
version_match = re.search(r"<VersionPrefix>(\d+\.\d+\.\d+)</VersionPrefix>", props)
if not version_match:
    raise SystemExit("P16 release candidate VersionPrefix is missing or invalid.")
current_version = version_match.group(1)
release_root = f"docs/releases/{current_version}"

required_files = [
    ".github/workflows/p16-full-acceptance.yml",
    "scripts/verify-p16-backup-restore.ps1",
    "scripts/verify-p16-performance.ps1",
    "scripts/verify_p16_ui_parity.py",
    "scripts/build-p16-release-candidate.ps1",
    "docs/evidence/P16_FULL_ACCEPTANCE.md",
    f"{release_root}/RELEASE_NOTES.md",
    f"{release_root}/DATABASE_MIGRATION_NOTES.md",
    f"{release_root}/DEPLOYMENT_ROLLBACK_RUNBOOK.md",
    f"{release_root}/UAT_CHECKLIST.md",
    "docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg",
    "docs/ui-baseline/bilingual_kuwait_government_service_portal.svg",
    "docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg",
    "docs/ui-baseline/kuwait_government_audit_dashboard.svg",
]

missing = [path for path in required_files if not (ROOT / path).is_file()]
if missing:
    raise SystemExit("P16 required files are missing: " + ", ".join(missing))

phase = (ROOT / "CURRENT_PHASE.md").read_text(encoding="utf-8")
control = (ROOT / "PROJECT_CONTROL.md").read_text(encoding="utf-8")
ledger = (ROOT / "docs/TASK_LEDGER.md").read_text(encoding="utf-8")
criteria = (ROOT / "docs/FINAL_ACCEPTANCE_CRITERIA.md").read_text(encoding="utf-8")
parity_gate = (ROOT / "docs/UI_DESIGN_PARITY_GATE.md").read_text(encoding="utf-8")
workflow = (ROOT / ".github/workflows/p16-full-acceptance.yml").read_text(encoding="utf-8")
evidence = (ROOT / "docs/evidence/P16_FULL_ACCEPTANCE.md").read_text(encoding="utf-8")
release_builder = (ROOT / "scripts/build-p16-release-candidate.ps1").read_text(encoding="utf-8")

p16_current_authority = "P16 — Full automated acceptance on the exact release candidate" in phase
p17_open_authority = (
    "P17 — Final convergence and release closure" in phase
    and "Status: **OPEN / READY**" in phase
    and "P00-P16 are formally **CLOSED**" in phase
    and "P16 is formally **CLOSED**" in phase
    and "| P16 | CLOSED |" in ledger
    and "| P17 | OPEN / READY |" in ledger
    and "P00-P16 are formally CLOSED" in control
    and "P17" in control
)
p17_closed_authority = (
    "P17 — Final convergence and release closure" in phase
    and "Status: **CLOSED**" in phase
    and "P16 is formally **CLOSED**" in phase
    and "| P16 | CLOSED |" in ledger
    and "| P17 | CLOSED |" in ledger
    and "P17" in control
    and (
        "P17 is formally **CLOSED**" in control
        or "P00-P17 are formally CLOSED" in control
    )
)
if not (p16_current_authority or p17_open_authority or p17_closed_authority):
    raise SystemExit("Missing canonical P16 authority or governed P17 OPEN/CLOSED post-P16 authority")

for text, needle, description in [
    (control, "P16", "P16 project-control boundary"),
    (criteria, "same-commit/same-artifact", "same-candidate final acceptance rule"),
    (parity_gate, "P16 must run full UI parity acceptance", "P16 UI parity rule"),
    (evidence, "P16::full-acceptance-release-candidate", "canonical P16 unit evidence"),
    (evidence, "DEFERRED_EXTERNAL_NOT_PASS", "external evidence NOT PASS preservation"),
    (evidence, "OWNER_LAST", "owner-last preservation"),
]:
    if needle not in text:
        raise SystemExit(f"Missing {description}: {needle}")

if "AcceptanceState = 'EXACT_CANDIDATE_EVIDENCE'" not in release_builder:
    raise SystemExit("P16 release manifest must use neutral exact-candidate evidence state.")
if "CANDIDATE_NOT_FINAL_P17" in release_builder:
    raise SystemExit("P16 release manifest still contains stale pre-final P17 state.")

version_parts = tuple(int(part) for part in current_version.split("."))
if version_parts < (0, 1, 0):
    raise SystemExit("P16 release candidate version regressed below the accepted 0.1.0 baseline.")

workflow_tokens = [
    "CANDIDATE_SHA",
    "verify-p16-backup-restore.ps1",
    "verify-p16-performance.ps1",
    "verify_p16_ui_parity.py",
    "build-p16-release-candidate.ps1",
    "P16_FULL_AUTOMATED_ACCEPTANCE=PASS",
]
for token in workflow_tokens:
    if token not in workflow:
        raise SystemExit(f"P16 workflow is missing required acceptance token: {token}")

release_docs = [
    ROOT / release_root / "RELEASE_NOTES.md",
    ROOT / release_root / "DATABASE_MIGRATION_NOTES.md",
    ROOT / release_root / "DEPLOYMENT_ROLLBACK_RUNBOOK.md",
    ROOT / release_root / "UAT_CHECKLIST.md",
]
for path in release_docs:
    text = path.read_text(encoding="utf-8")
    if current_version not in text:
        raise SystemExit(f"Release document is not version-bound to {current_version}: {path.relative_to(ROOT)}")
    if "VERIFIED_FINAL_COMPLETE" in text and "forbidden" not in text.lower() and "must not" not in text.lower():
        raise SystemExit(f"Release document contains an unsafe final-completion claim: {path.relative_to(ROOT)}")
    if "-----BEGIN PRIVATE KEY-----" in text or "-----BEGIN RSA PRIVATE KEY-----" in text:
        raise SystemExit(f"Release document contains private-key material: {path.relative_to(ROOT)}")

print("P16_ACCEPTANCE_CONTRACT=PASS")
print(f"P16_VERSION={current_version}")
print("P16_SAME_CANDIDATE_RULE=ENFORCED")
print("P16_P17_OPEN_CLOSED_AUTHORITY=SUPPORTED")
print("P16_RELEASE_MANIFEST_FINALITY=NEUTRAL")
