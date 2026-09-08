# P01 Architecture & Bilingual Shell Evidence

Status: **CLOSED — EXACT-MAIN VERIFIED**

P01 implementation is integrated on exact `main` SHA `34c665b12e910b6dfcb735f4978be6649e03d5c3` through PR #4 and all required exact-main gates succeeded.

## Implemented scope

- Canonical layered projects: Web, Application, Domain, Infrastructure, Integrations and Contracts.
- Explicit project-reference direction with Domain/Contracts dependency-free.
- DI composition boundaries plus `PortalShellOptions` and an EF-compatible `IDataSession` persistence abstraction; concrete EF Core DbContext/migrations remain P02 work.
- Resource-based English/Arabic (`ar-KW`) localization with true LTR/RTL document direction.
- Deterministic shared-resource lookup using `GSIP.Web.Resources.ShellResource` and request `CurrentUICulture`.
- Current-route language switching that preserves path/query state.
- High-fidelity shared design system derived from mandatory repository SVG references: navy/blue/gold palette, gradient header, mirrored sidebar, cards, forms, alerts/badges and responsive hierarchy.
- Landing/dashboard foundation shell plus deliberately non-submitting Login shell only.
- Visible semantic version `v0.1.0`; Razor model expressions are verified not to leak as literal markup.
- Dedicated P01 executable architecture/design boundary checks.
- Windows runtime/browser verification for `/health/live`, English LTR, Arabic `ar-KW` RTL, disabled shell-only Login and browser evidence capture.
- No Setup, Identity, RBAC, metadata, secret-vault, service execution, MOJ, history/audit or installer feature is claimed by P01.

## Exact-main CI evidence

- Integrated implementation SHA: `34c665b12e910b6dfcb735f4978be6649e03d5c3`
- PR: #4 — normally merged
- Planning Integrity run `34213985141`: **SUCCESS**
- P00 Build Baseline regression run `34213985591`: **SUCCESS**
- P01 Architecture and UI Shell run `34213985167`: **SUCCESS**
  - Release build: PASS
  - P00 contracts: PASS
  - P01 architecture/design checks: PASS
  - Runtime bilingual/browser verification: PASS
  - UI evidence upload: PASS

## Exact-main browser artifact

Artifact: `P01-UI-Evidence-34c665b12e910b6dfcb735f4978be6649e03d5c3`

Workflow artifact digest:

`sha256:11492dc4afa4d2257f410540cf757a40be91926e89a61611896bc3f30b06d09c`

Screenshot manifest:

| Capture | Bytes | SHA-256 |
|---|---:|---|
| `p01-dashboard-en.png` | 164372 | `df167bfc5ecec653ee44bd355e58def4827094f22bd66f085435468dde6d6084` |
| `p01-dashboard-ar.png` | 142376 | `d6496bd6f8e7e02a73b1240a2fe29881e993bd111208558e10418370fdd14520` |
| `p01-login-en.png` | 135022 | `cebab2a2149607cc2f727b0b3ea41c593dbb88624d43d6f260cf2a4ad8016b13` |
| `p01-login-ar.png` | 125966 | `ce7870756fba692b80c01fa8fde11457f119b05f2549886f7e09c96197c921ed` |

The artifact also contains rendered English/Arabic HTML and server stdout/stderr diagnostics. The Arabic rendered page was verified as `lang="ar"`, `dir="rtl"`, culture `ar-KW`, and localized content; English is `ltr`. No live data, real credentials or government seal were introduced.

## Closure decision

All autonomous P01 requirements are satisfied on integrated exact-main evidence. No owner-only or external acceptance is deferred. P01 is therefore CLOSED and P02 may become canonical only through the normal phase-transition merge and exact-main verification.
