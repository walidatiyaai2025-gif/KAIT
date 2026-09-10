from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]

required_files = [
    ".github/workflows/p16-full-acceptance.yml",
    "scripts/verify-p16-backup-restore.ps1",
    "scripts/verify-p16-performance.ps1",
    "scripts/verify_p16_ui_parity.py",
    "scripts/build-p16-release-candidate.ps1",
    "docs/evidence/P16_FULL_ACCEPTANCE.md",
    "docs/releases/0.1.0/RELEASE_NOTES.md",
    "docs/releases/0.1.0/DATABASE_MIGRATION_NOTES.md",
    "docs/releases/0.1.0/DEPLOYMENT_ROLLBACK_RUNBOOK.md",
    "docs/releases/0.1.0/UAT_CHECKLIST.md",
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
criteria = (ROOT / "docs/FINAL_ACCEPTANCE_CRITERIA.md").read_text(encoding="utf-8")
parity_gate = (ROOT / "docs/UI_DESIGN_PARITY_GATE.md").read_text(encoding="utf-8")
workflow = (ROOT / ".github/workflows/p16-full-acceptance.yml").read_text(encoding="utf-8")
evidence = (ROOT / "docs/evidence/P16_FULL_ACCEPTANCE.md").read_text(encoding="utf-8")
props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")

for text, needle, description in [
    (phase, "P16 — Full automated acceptance on the exact release candidate", "canonical P16 phase authority"),
    (control, "P16", "P16 project-control boundary"),
    (criteria, "same-commit/same-artifact", "same-candidate final acceptance rule"),
    (parity_gate, "P16 must run full UI parity acceptance", "P16 UI parity rule"),
    (evidence, "P16::full-acceptance-release-candidate", "canonical P16 unit evidence"),
    (evidence, "DEFERRED_EXTERNAL_NOT_PASS", "external evidence NOT PASS preservation"),
    (evidence, "OWNER_LAST", "owner-last preservation"),
]:
    if needle not in text:
        raise SystemExit(f"Missing {description}: {needle}")

version_match = re.search(r"<VersionPrefix>([^<]+)</VersionPrefix>", props)
if not version_match or version_match.group(1).strip() != "0.1.0":
    raise SystemExit("P16 release candidate must remain bound to VersionPrefix 0.1.0.")

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
    ROOT / "docs/releases/0.1.0/RELEASE_NOTES.md",
    ROOT / "docs/releases/0.1.0/DATABASE_MIGRATION_NOTES.md",
    ROOT / "docs/releases/0.1.0/DEPLOYMENT_ROLLBACK_RUNBOOK.md",
    ROOT / "docs/releases/0.1.0/UAT_CHECKLIST.md",
]
for path in release_docs:
    text = path.read_text(encoding="utf-8")
    if "0.1.0" not in text:
        raise SystemExit(f"Release document is not version-bound: {path.relative_to(ROOT)}")
    if "VERIFIED_FINAL_COMPLETE" in text and "forbidden" not in text.lower():
        raise SystemExit(f"Release document contains an unsafe final-completion claim: {path.relative_to(ROOT)}")
    if "-----BEGIN PRIVATE KEY-----" in text or "-----BEGIN RSA PRIVATE KEY-----" in text:
        raise SystemExit(f"Release document contains private-key material: {path.relative_to(ROOT)}")

print("P16_ACCEPTANCE_CONTRACT=PASS")
print("P16_VERSION=0.1.0")
print("P16_SAME_CANDIDATE_RULE=ENFORCED")