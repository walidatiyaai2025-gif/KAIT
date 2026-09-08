# P04 RBAC and Permissions UI — Closure Evidence

## Closure identity

- Phase: P04 — RBAC and service-level permissions
- Final integrated implementation SHA: `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`
- Final implementation PR: #20 — normally merged
- Closure decision source: exact integrated `main` implementation plus exact-main CI and artifact evidence
- Owner/external dependency deferred: none

## Exact-main verification

All applicable workflows succeeded on the same exact implementation SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`:

- Planning Integrity — run `34253075877` — SUCCESS
- P00 Build Baseline — run `34253075003` — SUCCESS
- P01 Architecture and UI Shell — run `34253075362` — SUCCESS
- P02 First-run Setup Wizard — run `34253075183` — SUCCESS
- P03 Web Security Headers — run `34253075119` — SUCCESS
- P03 Identity and Account Security — run `34253074987` — SUCCESS
- P04 RBAC Core — run `34253075074` — SUCCESS
- P04 Permissions UI — run `34253075409` — SUCCESS

The P04 Permissions UI workflow completed Release build, SQL LocalDB startup, RBAC contract checks, executable role-administration/IDOR checks, protected bilingual browser verification, execution-plan/governance validation and evidence upload successfully.

## Exact-main artifact

- Name: `P04-Permissions-UI-Evidence-c3aa76a8d456c9b951b602001bcbe1023ae26dd9`
- Artifact ID: `10066813357`
- Size: 476,233 bytes
- Workflow SHA-256 digest: `sha256:b5b6fa42c7dd114b302a2db3572c25130f01e2c045586a90bcaf90b97d290826`
- Produced from exact integrated `main` implementation SHA above

## Implemented authorization boundary

P04 establishes the authorization boundary required before metadata administration:

- editable seeded roles: System Administrator, Integration Manager, Service Operator, Auditor and Read Only;
- canonical permission catalog and RolePermissions persistence;
- UserRoles assignment persistence and administration;
- per-service role permission matrix;
- Default Deny behavior for newly created role/permission combinations until explicitly granted;
- server-side authorization enforcement rather than UI-only hiding;
- protected role administration including creation and persisted renaming of seeded roles;
- user-role assignment and removal;
- protection against disabling/removing the last enabled System Administrator;
- permissions administration surfaces constrained by the closed P03 authentication/account-security boundary.

## Positive and negative evidence

Executable P04 checks and browser verification prove the closure-audit gaps are resolved:

- role creation begins with Default Deny — PASS;
- persisted seed-role rename — PASS;
- UserRoles assign/remove — PASS;
- unknown/forged role, user and service identifiers are denied — PASS;
- last enabled System Administrator lockout is denied — PASS;
- service permission matrix behavior remains governed server-side — PASS;
- Pending Approvals is represented truthfully as zero when no governed approval source exists — PASS;
- Arabic RTL and English LTR Permissions management browser flows pass at responsive desktop/mobile coverage;
- closed P00–P03 workflows remain green on the same exact implementation SHA.

## Secrets and data handling

No live secret, production credential or real personal data is included in the P04 implementation/evidence. P04 does not implement P05 metadata administration or P06 Secret Vault/auth-profile functionality. Future service/environment/auth/secret binding remains governed by `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` and the phase gates.

## Closure conclusion

The P04 exit condition is satisfied on exact integrated implementation SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`: role/permission model, Default Deny, per-service permission controls, server-side authorization, permissions UI parity, negative authorization/IDOR coverage, CI, artifact evidence and exact-main regression verification are complete and pushed with no owner-only evidence deferred.

The phase transition to P05 becomes authoritative only after the closure-reconciliation change containing this evidence, `CURRENT_PHASE.md`, `PROJECT_CONTROL.md` and `docs/TASK_LEDGER.md` is normally integrated to `main` and the resulting exact-main closed-phase regressions remain green.
