from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
checks = 0

FINAL_P13_HEAD = "1576764a16ea6ddfed735cb0824cb38de26c83d8"
INTEGRATED_P13_MAIN = "203cc28db714fae5c2c70e85adc9cc2306bb2107"


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    global checks
    if not condition:
        raise SystemExit(message)
    checks += 1


phase = read("CURRENT_PHASE.md")
phase_upper = phase.upper()
layout = read("src/GSIP.Web/Views/Shared/_Layout.cshtml")
p13_css = read("src/GSIP.Web/wwwroot/css/p13.css")
home = read("src/GSIP.Web/Views/Shell/Index.cshtml")
controller = read("src/GSIP.Web/Controllers/ShellController.cs")
model = read("src/GSIP.Web/Models/ShellViewModel.cs")
execution = read("src/GSIP.Web/Views/Execution/Index.cshtml")
metadata = read("src/GSIP.Web/Views/Metadata/Index.cshtml")
requests = read("src/GSIP.Web/Views/Requests/Index.cshtml")
operations = read("src/GSIP.Web/Views/Operations/Index.cshtml")
login = read("src/GSIP.Web/Views/Shell/Login.cshtml")
setup = read("src/GSIP.Web/Views/Setup/Wizard.cshtml")
p04_workflow = read(".github/workflows/p04-permissions-ui.yml")
p07_workflow = read(".github/workflows/p07-execution-ui.yml")
p11_workflow = read(".github/workflows/p11-audit-monitoring.yml")
p04_script = read("scripts/verify-p04-permissions-ui.ps1")
p07_script = read("scripts/verify-p07-execution-ui.ps1")
p11_script = read("scripts/verify-p11-audit-runtime-ui.ps1")
evidence = read("docs/evidence/P13_UI_ACCESSIBILITY_CONVERGENCE.md")
decisions = read("docs/DESIGN_DECISIONS.md")

p13_open = (
    "**P13 — ARABIC/ENGLISH UX AND ACCESSIBILITY CONVERGENCE**" in phase_upper
    and "STATUS: **OPEN / READY**" in phase_upper
)
p13_closed = (
    "P13" in phase_upper
    and "CLOSED" in phase_upper
    and FINAL_P13_HEAD in phase
    and INTEGRATED_P13_MAIN in phase
    and "P13 CLOSED" in evidence.upper()
    and FINAL_P13_HEAD in evidence
    and INTEGRATED_P13_MAIN in evidence
)
require(p13_open or p13_closed, "P13 is neither implementation-open nor preserved as an exact-evidence closed baseline.")
if p13_open:
    require("P14" in phase_upper and "LOCKED" in phase_upper, "P14 must remain locked during P13 implementation.")
else:
    require(any(f"**P{number}" in phase_upper for number in range(14, 18)), "A post-P13 canonical phase is not identified.")
require('<html lang="@language" dir="@direction"' in layout, "Shared shell lost first-class lang/dir rendering.")
require('class="skip-link" href="#main-content"' in layout, "Skip-to-content link is missing.")
require('id="main-content"' in layout and 'tabindex="-1"' in layout, "Main landmark is not keyboard focusable after skip navigation.")
require('data-culture="@(isRtl ? "en" : "ar-KW")"' in layout, "Language switch no longer preserves bilingual culture switching.")
require(any(f'<strong>P{number}</strong>' in layout for number in range(13, 18)), "Shared phase marker regressed below the P13 baseline.")
require('href="~/css/p13.css"' in layout, "P13 convergence stylesheet is not loaded by the shared shell.")
require(":focus-visible" in p13_css, "Shared P13 keyboard focus treatment is missing.")
require(".nav-item:nth-child(n+5){display:flex}" in p13_css, "Mobile navigation does not restore all primary routes.")
require("overflow-x:auto" in p13_css and "min-width:max-content" in p13_css, "Mobile navigation is not horizontally reachable on narrow screens.")
require('data-testid="p13-home-dashboard"' in home and 'data-phase="@Model.Phase"' in home, "P13 Home runtime marker is missing.")
require('role="search"' in home and 'for="dashboard-entity"' in home and 'for="dashboard-search"' in home, "Home catalogue filters do not have semantic search/labels.")
require('data-testid="dashboard-entity-card"' in home and 'class="dashboard-entity-grid"' in home, "Home no longer preserves the reference entity-card hierarchy.")
require('data-testid="dashboard-service-card"' in home and 'class="dashboard-service-grid"' in home, "Home no longer preserves the reference service-card hierarchy.")
require('class="dashboard-service-meta"' in home and '<dl' in home and '<dt>' in home and '<dd' in home, "Home service cards lack semantic metadata structure.")
require('class="dashboard-service-table"' not in home, "Generic services table reintroduced a material drift from the Home reference cards.")
require('dir="ltr">@service.EntityCode' in home and 'dir="ltr">@service.ServiceCode' in home, "Home technical identifiers do not preserve LTR direction.")
require("Future phase" not in home and "Available after setup" not in home and "disabled" not in home, "Stale P01 disabled preview remains on Home.")
require("IMetadataCatalogService metadataCatalog" in controller, "Home is not wired to the canonical metadata catalogue.")
require("GetSnapshotAsync(cancellationToken)" in controller, "Home does not obtain live non-secret catalogue data.")
require('"P13"' in controller, "Home model does not report the P13-established dashboard baseline.")
require("service.Active && service.IsCurrent" in controller, "Home does not fail closed to active current service definitions.")
require("Take(50)" in controller, "Home catalogue rendering is not bounded.")
require("ShellServiceCardViewModel" in model and "ActiveEnvironmentCount" in model, "Home view model lacks catalogue-backed service state.")
require('data-testid="execution-history-link"' in execution and 'href="/requests?culture=@culture&amp;serviceCode=' in execution, "Service Execution History tab is not connected to protected P10 request history.")
require('title="@L["HistoryDeferred"]"' not in execution, "Stale deferred History behavior remains on Service Execution.")
require("@Html.AntiForgeryToken()" in metadata, "Metadata mutation surface lost antiforgery protection.")
require('aria-label="@Model.Text("History filters"' in requests and '<th>' in requests, "Request history semantic filtering/table contract is missing.")
require('data-testid="operations-dashboard"' in operations and 'aria-label="@(ar ? "مكونات صحة النظام"' in operations, "Operations semantic dashboard contract is missing.")
require('role="alert"' in login and 'autocomplete="username"' in login and 'autocomplete="current-password"' in login, "Login accessibility/security field semantics are missing.")
require('role="status"' in setup and 'aria-label="Setup progress"' in setup, "Setup status/progress accessibility semantics are missing.")

baselines = {
    "docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg": "Government Services Integration Portal",
    "docs/ui-baseline/bilingual_kuwait_government_service_portal.svg": "Service Execution",
    "docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg": "Permission",
    "docs/ui-baseline/kuwait_government_audit_dashboard.svg": "Audit",
}
for path, marker in baselines.items():
    text = read(path)
    require(marker.lower() in text.lower(), f"Required P13 visual baseline is missing semantic marker: {path}")

require("CANDIDATE_SHA" in p04_workflow and "ref: ${{ env.CANDIDATE_SHA }}" in p04_workflow, "P04 evidence workflow is not bound to the exact PR candidate SHA.")
require("if: ${{ always() }}" in p04_workflow and "P04-Permissions-UI-Evidence-${{ env.CANDIDATE_SHA }}" in p04_workflow, "P04 candidate screenshot artifacts are not retained.")
require("CANDIDATE_SHA" in p07_workflow and "P07-Execution-UI-Evidence-${{ env.CANDIDATE_SHA }}" in p07_workflow, "P07 exact-candidate screenshot evidence is not retained.")
require("CANDIDATE_SHA" in p11_workflow and "P11-Audit-LocalDB-Runtime-${{ env.CANDIDATE_SHA }}" in p11_workflow, "P11 exact-candidate screenshot evidence is not retained.")
require("screenshot" in p04_script.lower(), "Permissions browser acceptance no longer captures screenshots.")
require("screenshot" in p07_script.lower(), "Service Execution browser acceptance no longer captures screenshots.")
require("screenshot" in p11_script.lower(), "Audit browser acceptance no longer captures screenshots.")
require(
    ("OPEN-PHASE CANDIDATE" in evidence and "NOT P13 CLOSURE" in evidence) or p13_closed,
    "P13 evidence is neither a guarded open candidate nor exact integrated closure evidence.",
)
require("DEFERRED_EXTERNAL_NOT_PASS" in evidence and "PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS" in evidence, "P13 evidence incorrectly loses external NOT-PASS classifications.")
require("least-privilege" in decisions.lower() and "P13" in decisions, "P13 least-privilege design deviation is not documented.")
require("entity summary cards" in decisions.lower() and "service cards" in decisions.lower(), "P13 Home reference card hierarchy is not documented as retained.")

print(f"P13_UI_ACCESSIBILITY_STATIC_ACCEPTANCE=PASS checks={checks}")
