#!/usr/bin/env python3
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / "execution" / "GSIP_Full_Execution.json"
REQUIRED_UI = [
    ROOT / "docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg",
    ROOT / "docs/ui-baseline/bilingual_kuwait_government_service_portal.svg",
    ROOT / "docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg",
    ROOT / "docs/ui-baseline/kuwait_government_audit_dashboard.svg",
]
REQUIRED_DOCS = [
    ROOT / "AGENTS.md",
    ROOT / "PROJECT_CONTROL.md",
    ROOT / "CURRENT_PHASE.md",
    ROOT / "docs/TASK_LEDGER.md",
    ROOT / "docs/UI_DESIGN_PARITY_GATE.md",
    ROOT / "docs/OWNER_LAST_EXECUTION_POLICY.md",
    ROOT / "docs/SECURITY_SECRETS_POLICY.md",
    ROOT / "docs/FINAL_ACCEPTANCE_CRITERIA.md",
    ROOT / "docs/plans/GSIP_Complete_Implementation_Plan_AR.md",
    ROOT / "docs/moj-api-reference/INDEX.md",
]

def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)

if not PLAN.exists():
    fail(f"missing plan: {PLAN.relative_to(ROOT)}")

data = json.loads(PLAN.read_text(encoding="utf-8"))
if data.get("SchemaVersion") != 1:
    fail("SchemaVersion must be 1")
if data.get("Loop") is not False:
    fail("Loop must be false")
if data.get("DefaultDelaySeconds") != 40:
    fail("DefaultDelaySeconds must be 40")
messages = data.get("Messages")
if not isinstance(messages, list) or len(messages) != 18:
    fail("Messages must contain exactly 18 P00-P17 entries")
for index, message in enumerate(messages):
    expected = f"P{index:02d}"
    label = str(message.get("Label", ""))
    if not label.startswith(expected + " ") and not label.startswith(expected + " -"):
        fail(f"message {index} must start with {expected}: {label!r}")
    if message.get("DelaySeconds") != 40:
        fail(f"{expected} DelaySeconds must be 40")
    if message.get("Enabled") is not True:
        fail(f"{expected} must remain enabled")
    if not isinstance(message.get("Text"), str) or not message["Text"].strip():
        fail(f"{expected} Text is empty")

missing = [p.relative_to(ROOT).as_posix() for p in REQUIRED_DOCS + REQUIRED_UI if not p.exists()]
if missing:
    fail("missing required files: " + ", ".join(missing))

print("Execution plan valid: 18 phases, all delays = 40 seconds, required governance/UI/MOJ reference files present.")
