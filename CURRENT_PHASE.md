# CURRENT_PHASE.md

## Canonical current phase

**P05 — Entity and Service metadata catalog**

Status: **OPEN / READY**

P04 is CLOSED from integrated exact-main evidence. The final P04 permissions administration and bilingual UI closure implementation was normally merged by PR #20 at exact `main` SHA `c3aa76a8d456c9b951b602001bcbe1023ae26dd9` after the role/permission core and negative authorization work had already been integrated. On that exact implementation SHA, all eight applicable workflows succeeded: Planning Integrity `34253075877`, P00 Build Baseline `34253075003`, P01 Architecture and UI Shell `34253075362`, P02 First-run Setup Wizard `34253075183`, P03 Web Security Headers `34253075119`, P03 Identity and Account Security `34253074987`, P04 RBAC Core `34253075074`, and P04 Permissions UI `34253075409`.

The exact-main P04 artifact is `P04-Permissions-UI-Evidence-c3aa76a8d456c9b951b602001bcbe1023ae26dd9` (476,233 bytes) with workflow digest `sha256:b5b6fa42c7dd114b302a2db3572c25130f01e2c045586a90bcaf90b97d290826`.

This P05 transition becomes authoritative only after this closure reconciliation is normally integrated to `main` and the resulting exact-main closed-phase regressions remain green. Do not begin P05 implementation from an unmerged transition branch.

## Legal work now

After this transition is integrated and exact-main verification is green, P05 only, plus any repair needed to preserve closed P00/P01/P02/P03/P04 baselines and repository controls.

P05 must implement the metadata-driven Entity and Service catalog before Secret Vault/auth-profile or generic execution work:

- Entities with Code, Arabic/English names, Logo, Active state and DisplayOrder;
- UAT/Production environment metadata including BaseUrl, proxy/TLS/timeout and health settings while preserving the binding rules in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md`;
- Services with Code, Arabic/English names and description, HTTP Method, RelativePath, ContentType, AuthProfile binding and Active state;
- ServiceFields with key, Arabic/English labels, type, required/regex/min/max/options/order, sensitivity and masking metadata;
- ResultMappings with source path, labels, type, formatter, sensitivity and order;
- secure administration CRUD with versioning for definitions already used so historical definitions are not silently rewritten;
- metadata import/export governed by a JSON schema;
- executable evidence that a complete sample Entity and Service can be created without a custom Controller/View for that service;
- build/test/repair/retest, normal PR/merge, evidence and exact-main verification before closure.

## Locked future work

P06–P17 remain locked. Do not implement Secret Vault/auth profiles, generic execution, MOJ authentication/services, history/audit, operational administration, installer or release acceptance before their phase is current.

The per-service/per-environment isolation contract in `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` remains binding. Every Service + Environment binding and AuthProfile remains isolated by default and no secret or token may cross service boundaries automatically.

## P04 closure evidence

- Final integrated implementation SHA: `c3aa76a8d456c9b951b602001bcbe1023ae26dd9`
- Final implementation PR: #20 — normally merged
- Planning Integrity: run `34253075877` — SUCCESS
- P00 Build Baseline regression: run `34253075003` — SUCCESS
- P01 Architecture and UI Shell regression: run `34253075362` — SUCCESS
- P02 First-run Setup Wizard regression: run `34253075183` — SUCCESS
- P03 Web Security Headers regression: run `34253075119` — SUCCESS
- P03 Identity and Account Security regression: run `34253074987` — SUCCESS
- P04 RBAC Core: run `34253075074` — SUCCESS
- P04 Permissions UI: run `34253075409` — SUCCESS
- Exact-main artifact: `P04-Permissions-UI-Evidence-c3aa76a8d456c9b951b602001bcbe1023ae26dd9`
- Artifact size: 476,233 bytes
- Artifact workflow digest: `sha256:b5b6fa42c7dd114b302a2db3572c25130f01e2c045586a90bcaf90b97d290826`
- Authorization evidence: seeded editable roles, permission catalog, RolePermissions, UserRoles, service permission matrix, Default Deny and server-side policy enforcement
- Administration/negative evidence: role creation and rename, user-role assignment/removal, forged identifiers denied and last enabled System Administrator lockout prevented
- UI evidence: high-fidelity Permissions & Role Management flow with Arabic RTL / English LTR browser verification, including role/user management, service matrix and truthful pending-approvals state
- Regression evidence: P00–P03 closed contracts remain green on the exact P04 implementation SHA
- Owner/external dependency: none deferred for P04

Detailed evidence is recorded in `docs/evidence/P04_RBAC_PERMISSIONS_UI.md`.

## P05 exit condition

P05 may be marked CLOSED only when the metadata-driven Entity/Environment/Service/ServiceField/ResultMapping catalog, secure versioned administration, JSON-schema import/export, service/environment isolation contract, sample no-custom-controller/view proof, CI/evidence and exact-main verification are complete and pushed without weakening P00–P04 or introducing P06+ scope.
