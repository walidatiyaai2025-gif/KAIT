# CURRENT_PHASE.md

## Canonical current phase

**P14 — Security hardening and resilience**

Status: **CLOSURE TRANSITION — P15 STAGED / IMPLEMENTATION LOCKED**

P13 is **CLOSED**. Its closure transition PR #72 was normally integrated at exact `main` `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`, and the resulting exact-main matrix completed **32/32 push workflows SUCCESS**.

P14 implementation was normally integrated by PR #73 from exact implementation head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` to exact implementation `main` `1f1def164d639c75d9cc26710a905cc491355a2b`. The PR head completed **37/37 governed workflows SUCCESS** before merge. The resulting exact implementation main completed **34/34 push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at closure review.

The P14 finding register contains no known cloud-actionable Critical or High vulnerability. All P14 cloud-actionable implementation work is terminal. This branch is therefore governance/evidence-only closure reconciliation; no P15 production implementation is authorized on this branch.

## P14 closed implementation boundary

P14 hardened the existing P00-P13 architecture without creating competing runtime engines. The integrated boundary includes:

- centralized restricted-session enforcement between authentication and authorization;
- independent RBAC fail-closed rejection of restricted MFA/forced-password principals;
- authenticated challenge rate limiting while preserving login lockout/IP throttling;
- executable anti-forgery coverage for mutating MVC actions;
- executable Razor output-encoding/XSS bypass checks and preserved CSP/security headers;
- bounded retry, Retry-After, timeout/cancellation, response-size and external-outage behavior;
- token-cache single-flight refresh/caller-cancellation safety;
- dependency vulnerability/deprecation evidence and a fail gate for known vulnerable NuGet packages;
- preservation of Service + Environment + AuthProfile + SecretRef isolation, secret redaction, audit integrity, request-history authorization and bilingual RTL/LTR behavior.

Detailed evidence: `docs/evidence/P14_SECURITY_HARDENING.md`.

## Legal work now

Canonical closure branch/lease: `worker/p14-closure-reconciliation`.

Legal work is limited to P14 closure materialization: reconcile `docs/TASK_LEDGER.md`, `docs/evidence/P14_SECURITY_HARDENING.md`, `CURRENT_PHASE.md`, `PROJECT_CONTROL.md`, tracker Issue #1, and exact closure CI evidence. Preserve all integrated P14 implementation and all historical deferred classifications.

## P14 exit condition

The implementation side of the P14 exit gate is satisfied on exact `main` `1f1def164d639c75d9cc26710a905cc491355a2b`: final implementation candidate green, no known Critical/High cloud-actionable finding, all governed exact-main workflows terminal green, and cloud-actionable implementation tasks terminal.

The remaining transition gate is procedural but still mandatory:

1. the closure-transition head must complete its governed PR workflow matrix successfully;
2. the closure transition must be normally integrated while based on current `main`;
3. every governed workflow on the resulting exact-new-main SHA must be terminal SUCCESS.

Only after all three are true is P14 formally **CLOSED** and P15 legally authorized as the sole canonical implementation phase.

## Staged next phase

**P15 — Professional Windows/IIS installer and packaging** is **STAGED / IMPLEMENTATION LOCKED**.

Its planned scope is the professional Windows/IIS installer, upgrade/repair/uninstall behavior, versioned artifact and SHA-256 evidence. No P15 implementation may merge before the P14 closure-transition exact-new-main gate above is green.

## Locked future work

P16–P17 remain **LOCKED** until their preceding phases close normally.

## Deferred boundaries

Historical owner/external classifications remain unchanged. P09 authorized live-UAT evidence that remains unavailable is still `DEFERRED_EXTERNAL_NOT_PASS`; unproven Production operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Production details are never inferred from UAT, and no Production→UAT fallback is allowed.

Main branch protection remains `OWNER_LAST / NOT PASS` while live read-back reports `main` unprotected and no repository ruleset configured. This repository-administration gap is not converted to PASS by P14 cloud evidence.
