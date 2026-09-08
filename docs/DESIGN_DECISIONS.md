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
