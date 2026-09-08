# UI DESIGN PARITY GATE

The owner-approved reference designs are mandatory v1 baselines, not optional inspiration.

## Canonical reference files

- `docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg` — Home / Dashboard
- `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg` — Service Execution
- `docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg` — Permissions & Role Management
- `docs/ui-baseline/kuwait_government_audit_dashboard.svg` — Audit & Monitoring

These repository-native vector baselines preserve the approved information architecture, component hierarchy, navigation model, government navy/blue/gold visual identity, and screen density. `docs/ui-baseline/README.md` is the index.

## Mandatory behavior

Any worker touching these screens must:

1. inspect the current reference before implementation;
2. preserve the core layout, information hierarchy, visual density, header/sidebar structure, cards, tables, forms, badges, alerts and navigation model;
3. implement the navy/blue/gold government-portal identity at high fidelity;
4. support Arabic RTL and English LTR as first-class layouts, not mirrored afterthoughts;
5. keep numeric identifiers such as Civil ID readable in correct numeric direction;
6. keep accessibility, keyboard navigation, focus states and responsive behavior functional;
7. not replace the reference with a generic Bootstrap/admin dashboard theme;
8. not materially simplify or remove reference components to accelerate closure.

## Required evidence

Before closing any affected phase, capture browser screenshots from the exact candidate commit for both Arabic and English at the agreed desktop viewport and at least one narrower responsive viewport. Store evidence under a versioned evidence path or CI artifact.

The evidence must identify:

- exact commit SHA;
- route/screen;
- language/direction;
- viewport;
- test/build identifier;
- known intentional deviations and rationale.

## Acceptance method

Automated visual regression may be used, but is not sufficient by itself. Review must also confirm semantic layout parity: the expected major components exist in the expected hierarchy and no critical content/control has been removed.

A release candidate is blocked if there is a critical visual drift, missing major component, broken RTL/LTR layout, unreadable content, or a generic-template substitution.

## Allowed deviations

A deviation is allowed only when it improves accessibility, security, responsive behavior, or corrects an implementation impossibility while preserving the design intent. It must be documented in `docs/DESIGN_DECISIONS.md` with before/after evidence.

## Final gate

P16 must run full UI parity acceptance. P17 must verify the evidence still points to the same exact final commit/artifacts before final completion can be claimed.
