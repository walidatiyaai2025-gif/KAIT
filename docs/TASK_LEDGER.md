# TASK_LEDGER.md — GSIP Canonical Phase Ledger

This ledger is evidence-driven. Never mark a phase CLOSED because a prompt says it should be closed. Update status only from live implementation/tests/CI/evidence on the exact integrated commit.

| Phase | Status | Scope / closure focus |
|---|---|---|
| P00 | OPEN | Live-state baseline, platform/toolchain pinning, repo structure, version policy, build/test/CI baseline, control-doc reconciliation |
| P01 | LOCKED | Solution architecture and bilingual shell; mandatory design-system baseline |
| P02 | LOCKED | Complete first-run Setup Wizard before Login, SQL Server test/create/use/migrations/admin/security/review/finish; create service/environment placeholders only, never source-coded live credentials |
| P03 | LOCKED | Identity, MFA, sessions, lockout/rate limiting, account security and auth audit |
| P04 | LOCKED | RBAC, server-side authorization, per-service permissions, high-fidelity permissions UI |
| P05 | LOCKED | Metadata-driven entities/services/fields/result mappings plus independent `ServiceEnvironmentConfig` per Service + UAT/Production environment; admin/versioning/import-export |
| P06 | LOCKED | Secret vault, isolated authentication profiles/SecretRefs, explicit Shared AuthProfile support, rotation/redaction, cross-service-safe token cache keys |
| P07 | LOCKED | Generic service execution engine resolving the exact Service + Environment + AuthProfile binding; high-fidelity dynamic Service Execution UI |
| P08 | LOCKED | MOJ x-api-key + `/genToken` + Bearer flow based on official docs/fixtures without assuming credentials are shared between MOJ services |
| P09 | LOCKED | MOJ entity + five services, exact official request/response contracts, independent endpoint/auth bindings, tests and authorized UAT smoke if available |
| P10 | LOCKED | Request/result history, masking, scoped access and permissioned exports |
| P11 | LOCKED | Tamper-evident audit trail, monitoring, high-fidelity Audit dashboard and evidence |
| P12 | LOCKED | Admin operations for Entity -> Service -> Environments -> UAT/Production, Edit/Test Connection/Test Authentication/Activate/Disable/Rotate Secret, health/diagnostics and operational alerts |
| P13 | LOCKED | Full Arabic/English RTL/LTR UX convergence + UI DESIGN PARITY GATE for all four reference screens |
| P14 | LOCKED | Security hardening, resilience, authorization/IDOR/CSRF/XSS/rate-limit/fuzz/dependency checks including cross-service endpoint/secret/token isolation |
| P15 | LOCKED | Professional Windows/IIS installer, upgrade/repair/uninstall path, versioned artifact + SHA-256 |
| P16 | LOCKED | Full automated acceptance on exact release candidate including setup→login→RBAC→MOJ→history→audit→installer + UI parity + service/environment isolation |
| P17 | LOCKED | Final convergence, exact-main regression repair, stale integration recovery, ledger/evidence reconciliation and final release closure |

## Cross-phase gates

The following apply to every phase:

- **LIVE STATE FIRST:** inspect main/PRs/branches/CI/claims before new work.
- **NO DUPLICATION:** recover legitimate existing work before creating overlapping work.
- **NO FUTURE PHASE WORK:** current-phase closure controls progression.
- **TEST/REPAIR/RETEST:** failed required tests or exact-main regressions have priority.
- **NO SECRET LEAKAGE:** secrets and personal data must not enter Git/logs/evidence.
- **SERVICE ISOLATION:** endpoint/auth/secret/token configuration defaults to independent Service + Environment bindings; sharing requires an explicit Shared AuthProfile.
- **UI PARITY:** any phase touching one of the four reference screens must preserve the mandatory baseline and capture evidence.
- **OWNER-LAST:** finish all autonomous portions before deferring an external/owner-only check.
- **PUSHED EVIDENCE:** no phase closure from unpushed local work.
- **EXACT-MAIN RECHECK:** after integration, verify the exact new main SHA.

## Initial MOJ scope

1. Marriage Cases Service
2. Is Single Basic Service
3. Marriage Couple Last Case Service
4. Family Judgment Text Service
5. Procuration Status Service

Request/response/auth details must come from official CAIT/MOJ documentation and the references under `docs/moj-api-reference/`; unknown fields must not be invented.
