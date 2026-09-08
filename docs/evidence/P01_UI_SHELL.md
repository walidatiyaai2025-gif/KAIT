# P01 Architecture & Bilingual Shell Evidence

Status: **IMPLEMENTATION CANDIDATE — NOT YET CLOSED**

P01 remains OPEN until the implementation is integrated, required CI/browser evidence is green, and the exact resulting `main` SHA is rechecked.

## Candidate implementation

- Canonical layered projects: Web, Application, Domain, Infrastructure, Integrations and Contracts.
- Explicit project-reference direction with Domain/Contracts dependency-free.
- DI composition boundaries plus `PortalShellOptions` and an EF-compatible `IDataSession` persistence abstraction; concrete EF Core DbContext/migrations intentionally remain P02 work.
- Resource-based English/Arabic (`ar-KW`) localization with real LTR/RTL document direction.
- Current-route language switching that preserves path/query state.
- High-fidelity shared design system derived from all four mandatory SVG references: navy/blue/gold palette, gradient header, mirrored sidebar, cards, forms, alerts/badges and responsive hierarchy.
- Landing/dashboard foundation shell plus deliberately non-submitting Login shell only.
- No Setup, Identity, RBAC, metadata, secret-vault, service execution, MOJ, history/audit or installer feature is claimed.
- Dedicated P01 executable architecture/design boundary checks.
- Dedicated Windows browser verification captures English/Arabic Dashboard and Login screenshots and hashes them into an evidence manifest.

## Required closure evidence

- P01 branch/PR Release build succeeds with zero warnings/errors.
- P00 contracts remain green.
- P01 architecture/design checks pass.
- Runtime smoke verifies `/health/live`, English LTR, Arabic RTL and shell-only Login.
- Four browser screenshots are captured from the same head and uploaded with manifest hashes.
- Normal merge under repository policy.
- Exact-main P00 + P01 + Planning Integrity gates green on the integrated SHA.
- `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`, `PROJECT_CONTROL.md`, issue #1 and this evidence file reconciled from exact-main evidence before transition to P02.
