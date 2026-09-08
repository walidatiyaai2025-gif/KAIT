# GSIP UI Baseline

These four vector files are the repository-native visual baselines derived from the owner-approved screen designs. They define the mandatory information architecture, visual identity, component density, navigation model, and high-level proportions for v1.

- `bilingual_kuwait_government_services_dashboard.svg` — Dashboard/Home
- `bilingual_kuwait_government_service_portal.svg` — Service Execution
- `bilingual_kuwait_government_permissions_dashboard.svg` — Permissions & Role Management
- `kuwait_government_audit_dashboard.svg` — Audit & Monitoring

Use them together with `../UI_DESIGN_PARITY_GATE.md`. The implementation may improve typography, accessibility, real data density and responsive behavior, but must not replace the layout with a generic admin template or remove major components.

Color intent: dark navy header/sidebar, government blue interactive surfaces, restrained gold accents, white/light-blue workspace, green success, red failure, amber warning, purple/high-privilege accents.

Arabic RTL and English LTR must both be implemented and evidenced. Desktop is the highest-fidelity reference; responsive layouts rearrange components without deleting functionality.
