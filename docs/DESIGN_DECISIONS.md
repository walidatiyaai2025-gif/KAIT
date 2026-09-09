# GSIP Design Decisions

## P01 — Application shell

### Mandatory source references

The v1 visual language is derived directly from all four repository-native SVG references in `docs/ui-baseline/`. P01 implements only the shared shell vocabulary visible across those references; feature-specific Dashboard, Service Execution, Permissions and Audit content remains owned by its legal phase.

### Design tokens retained

- Canvas: `#f4f8fc`
- Navigation: `#07345d`
- Header gradient family: `#0b4e78` → `#082f55`
- Primary action/nav blue: `#0b4f7d` / `#0b5f91`
- Cyan navigation accent: `#16a6d9`
- Gold institutional accent: `#c99a2e`
- Primary text: `#0b3560`
- Secondary text: `#355c7d`
- Borders: `#dbe6f0`
- White cards, restrained shadows, rounded controls, status badges and dense but legible hierarchy.

### Direction and layout

Arabic uses `ar-KW` and true `dir=rtl`; English uses `en` and `dir=ltr`. The sidebar mirrors between right and left while preserving the same information architecture. The language switch changes the culture query parameter on the current URL rather than sending the user to a different route.

### Government branding boundary

P01 does **not** invent or redraw an official Kuwait/government/MOJ seal. The shell uses an original neutral `G` product monogram rendered in CSS. Authorized official branding remains configurable in the later Setup/Branding work when an approved asset exists.

### Phase-boundary deviations

The reference screens contain live service, permission and audit data. P01 intentionally replaces those feature-specific areas with clearly labelled disabled workspace previews. This is not a visual simplification of the product; it prevents false implementation of P04/P05/P07/P11 functionality before those phases are legal. Header/sidebar/card/form/badge/spacing/color patterns are retained now, while exact feature screens converge in their owning phases and again at P13.

The Login page is a visual shell only. Its inputs and submit control are disabled and marked `shell-only`; P02 owns first-run Setup and P03 owns actual Identity/authentication.

### Responsive behavior

Desktop is the high-fidelity source. Tablet reduces four-column groups to two columns. Mobile converts the sidebar to a bottom navigation strip and stacks cards/forms without deleting the principal shell hierarchy.

## P07 — Service Execution secondary-text contrast

The canonical Service Execution reference remains `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg`; its navy/blue/gold hierarchy, header/sidebar structure, cards, forms, result workspace, Arabic RTL and English LTR behavior remain unchanged.

The inherited shell muted token `#7690aa` was too light for the 10.5–12px normal-weight secondary text used on the P07 Service Execution screen. It produced only about 3.10:1 contrast on the GSIP canvas (`#f4f8fc`) and 3.31:1 on white cards, below the 4.5:1 WCAG AA threshold for normal text.

P07 therefore applies a screen-scoped muted tone of `#58738d` to the execution hero, request/service workspace and result workspace. The resulting contrast is approximately 4.63:1 on the canvas, 4.76:1 on the light result surfaces and 4.94:1 on white cards. This is an accessibility-only deviation permitted by `docs/UI_DESIGN_PARITY_GATE.md`; it does not change layout, component hierarchy, responsive breakpoints, bidi behavior, institutional branding, or introduce a generic admin template.

## P13 — UI and accessibility convergence

### Dashboard least-privilege data boundary

The canonical Home reference is `docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg`. P13 converges the former P01 preview into a real catalogue-backed dashboard while retaining the reference hierarchy: government-services hero, four status cards, entity/search filters, available-services table, status badges, service actions, bilingual RTL/LTR behavior and the established navy/blue/gold shell.

The reference artwork includes operational request and alert totals. P13 intentionally does **not** aggregate those totals onto Home. Request history and audit/monitoring have narrower authorization boundaries than the authenticated metadata catalogue, and moving their totals into Home would broaden disclosure merely to match decorative reference values. Home therefore shows only least-privilege non-secret catalogue facts: active entities, active current services, active environments and executable build version. Request and audit information remains in its separately authorized surfaces. This is a security-only deviation permitted by `docs/UI_DESIGN_PARITY_GATE.md`; no synthetic production-looking KPI values are used.

### Mobile primary navigation

The inherited P01 mobile rule hid navigation items five and later. That became invalid once Requests, Audit, Permissions and Settings were real modules. P13 keeps the bottom navigation pattern but makes it horizontally reachable instead of deleting routes. All primary links remain in the accessibility tree and are keyboard focusable.

### Cross-phase screenshot reuse

P13 does not clone the established P04, P07 or P11 browser harnesses. Their workflows remain independent regression gates and capture the Permissions, Service Execution and Audit reference surfaces on the same exact candidate SHA. P13 adds the missing Home bilingual desktop/narrow browser evidence and strengthens P04 to checkout and retain artifacts for the exact pull-request head. This preserves independent acceptance while avoiding duplicate test implementations.
