# P16 Full Automated Acceptance Evidence

Status: **FORMALLY CLOSED / P17 ENTRY AUTHORIZED**

Canonical implementation unit: `P16::full-acceptance-release-candidate`  
Canonical implementation branch: `worker/p16-full-acceptance-continuation`  
Canonical implementation PR: #80  
Canonical closure unit: `P16::closure-reconciliation`  
Canonical closure branch: `worker/p16-closure-reconciliation`  
Canonical closure PR: #86

## Legal phase entry

P15 is formally CLOSED. P16 entry was materialized by PR #78 after its exact head `462a32f6a72e913b1c7ac2abe83aef98f0d97415` completed **37/37 governed pull-request workflows SUCCESS**. PR #78 integrated as exact main `3c995a3667d2f82637a6076ecb62c80326fd15d1`, which completed **34/34 governed push workflows SUCCESS** and legally authorized P16.

The earlier `worker/p16-full-acceptance` line is stale governance provenance with no acceptance implementation missing from the canonical continuation and must not be merged over the accepted line.

## P16 implementation acceptance

PR #80 final exact implementation head `e655dc4d0effb4f962d3e97e4a0230471238ba55` completed **38/38 governed pull-request workflows SUCCESS** and was normally integrated as exact main `75ef2f322a53f1e2ed0bfdf8fa3e4ccece5edd85`. That exact main completed **35/35 governed push workflows SUCCESS**.

The original 0.1.0 evidence remains valid historical implementation provenance. Later regressions required patch releases before formal P16 closure, so it is not the final accepted P16 product candidate.

## Historical 0.1.1 recovery provenance

The owner-observed API129 UAT configuration gap led to the 0.1.1 release line. Stale PR #82 was closed without merge; PR #83 was replayed from then-current main, reached final exact head `fd2ca1491db0a614ac93292a2dcfe8c656018d09`, completed **39/39 governed pull-request workflows SUCCESS**, and integrated as exact main `252e3422dd31bc9eb58b64c1019800f5b50b6e51`. That main completed **36/36 governed push workflows SUCCESS** and P16 run `34450118608` completed all four P16 legs SUCCESS.

The 0.1.1 evidence is historical provenance only. A later live repository audit proved that its API129 helper had reduced the authoritative token-exchange contract to an API-key shortcut, so P16 could not close from that candidate despite its mechanical CI success.

## Post-0.1.1 exact-main regression recovery

The repository-authoritative API129 contract requires exact-scope `x-api-key`, `username`, and `password` for `POST /genToken`, reads the transient token from response field `data`, then executes `/marriageCasesAPIGEE` with the same `x-api-key` plus `Authorization: Bearer`.

Existing PR #85 / branch `hotfix/0.1.2-moj-token-flow` was recovered instead of creating duplicate work. Initial PR #85 content was not accepted as-is because it made username/password optional and attempted broad cross-service MOJ authentication convergence. The same branch was repaired to:

- require write-only API129 UAT `x-api-key + username + password`;
- retain protected Secret Vault storage/rotation and exact Service + UAT Environment + AuthProfile ownership;
- remove optional empty-credential placeholder behavior;
- remove the broad startup MOJ authentication convergence service;
- preserve other MOJ services' evidence-bound authentication semantics;
- preserve P04 Default Deny and avoid automatic per-service privilege expansion;
- leave Production independent, disabled/fail-closed until its own official evidence exists, with no Production -> UAT fallback.

PR #85 final corrected head `0e767d4f355e403b70602a7645cd7ffba25b3b43` completed **40/40 governed pull-request workflows SUCCESS**, failure=0, queued=0 and in-progress=0 at terminal verification. It was normally merged using an expected-head guard as exact accepted product main:

`8863b4b8151ebb08e98fdb225ea5418c5297eb5c`.

The resulting exact-main matrix completed **36/36 governed push workflows SUCCESS** with no terminal non-success result.

## Same-candidate P16 acceptance on accepted product source

Exact-main P16 run: `34456867989`  
Exact source SHA: `8863b4b8151ebb08e98fdb225ea5418c5297eb5c`  
Version: `0.1.2`  
Target runtime: `win-x64`  
Framework: `net10.0` / pinned SDK `10.0.400`

All required P16 jobs completed SUCCESS:

- `static-contract`;
- `release-candidate-package`;
- `runtime-ui-recovery`;
- aggregate `p16-gate`.

The same exact candidate completed:

- solution restore/build and P16 source-contract validation;
- preservation of P14 outbound/security and P15 installer contracts;
- exact-candidate Permissions browser/runtime acceptance;
- exact-candidate Service Execution browser/runtime acceptance;
- exact-candidate Audit browser/runtime acceptance;
- exact-candidate Dashboard browser/runtime acceptance;
- Arabic RTL and English LTR desktop+narrow UI parity evidence across the four canonical reference screens;
- SQL Server LocalDB backup, restore verification and destructive-loss/restore rehearsal;
- synthetic App_Data Data Protection-key and protected setup-state preservation rehearsal;
- bounded localhost health/login performance smoke;
- execution-plan integrity;
- exact-candidate Windows package and Setup EXE generation plus SHA-256 verification.

## Accepted P16 release-candidate identity

Source SHA: `8863b4b8151ebb08e98fdb225ea5418c5297eb5c`  
Version: `0.1.2`  
Target runtime: `win-x64`  
Framework: `net10.0` / pinned SDK `10.0.400`

- `GSIP-0.1.2-win-x64.zip` — SHA-256 `b4971d31b11f3c1c6a6afba9026afd6865b7c006063eb59ffbdc9e2182efd426`;
- `GSIP-0.1.2-Setup-x64.exe` — SHA-256 `7a5de1c18bf9b84bd052d855d089f51ffdae175d40c872b574a68892fba99ff0`;
- Actions artifact `10143946003` — `P16-Release-Candidate-8863b4b8151ebb08e98fdb225ea5418c5297eb5c`, size 62,185,768 bytes, archive digest `sha256:b7c8f175b1669323917bbfa6b557076ce8b2d2c611af7009b13f83e4282429d6`;
- Actions artifact `10144204144` — `P16-Runtime-UI-Recovery-8863b4b8151ebb08e98fdb225ea5418c5297eb5c`, size 3,482,259 bytes, archive digest `sha256:4b5564603f8c00c5d07357dd206647f0f4dabfa5b04cd8fcc931bc1ab5a4965d`.

The earlier 0.1.0 and 0.1.1 P16 artifacts remain historical evidence only and must not be substituted for this accepted 0.1.2 product candidate.

## P16 closure transition and exact-new-main verification

Closure PR #86 final exact head `eeb03879d4b31b04bf333c8b5d021983ace8afe9` completed **39/39 governed pull-request workflows SUCCESS**. The PR was normally integrated using an expected-head guard as exact closure-transition main:

`18efd6c71a35505105d44df673d1d75fde293a2e`.

The resulting exact-new-main completed **36/36 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at terminal verification. Exact-main P16 run `34460153367` completed all required jobs SUCCESS:

- `static-contract` — job `102815806284`;
- `release-candidate-package` — job `102815806486`;
- `runtime-ui-recovery` — job `102815806420`;
- aggregate `p16-gate` — job `102817934093`.

That closure-main run independently repeated the exact-candidate build/contract checks, Permissions/Service Execution/Audit/Dashboard browser acceptance, SQL and protected-state backup/restore rehearsal, bounded local performance baseline, bilingual four-screen UI parity, execution-plan integrity and package generation.

Closure-main package outputs from run `34460153367` were:

- `GSIP-0.1.2-win-x64.zip` — SHA-256 `5dcc6ada31ac88e262d2ec25ca22c59ff146ef524662af6f51414b62ec6109b7`;
- `GSIP-0.1.2-Setup-x64.exe` — SHA-256 `51dd073ec139233074bcfe9bc5eff124ff402a98c8bc42c0b708c46b03940845`;
- Actions artifact `10145307991` — `P16-Release-Candidate-18efd6c71a35505105d44df673d1d75fde293a2e`, size 62,186,849 bytes, archive digest `sha256:1440707e9e485d1a61e13808e7c2843bef187ae648f8cec1d5d99ab5bcd7c3c8`;
- Actions artifact `10145429037` — `P16-Runtime-UI-Recovery-18efd6c71a35505105d44df673d1d75fde293a2e`, size 3,481,682 bytes, archive digest `sha256:45fa583d513629a42417bd1db09197ac4615967ac1238213dbbf1620efbe43ac`.

These closure-main artifacts are exact verification outputs for the governance-only closure commit. P17 must independently converge and bind its own final same-commit/same-artifact identity; P16 evidence does not pre-authorize a P17 final-completion claim.

## UI parity evidence model

Canonical references remain:

1. `docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg` — Dashboard;
2. `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg` — Service Execution;
3. `docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg` — Permissions & Role Management;
4. `docs/ui-baseline/kuwait_government_audit_dashboard.svg` — Audit & Monitoring.

P16 reuses existing candidate-bound runtime/browser verifiers rather than duplicating feature implementations. Exact closure-main runtime/UI job `102815806420` passed Permissions, Service Execution, Audit, Dashboard, backup/restore, bounded performance and four-screen bilingual parity on exact main `18efd6c71a35505105d44df673d1d75fde293a2e`.

## Recovery and performance boundaries

The cloud recovery rehearsal uses synthetic SQL Server LocalDB and synthetic App_Data state. It does not manufacture Production credentials, certificates, personal data, deployment topology or owner-environment evidence.

The performance result is a bounded localhost smoke baseline, not Production capacity/SLA certification. Production load, network latency, proxy/firewall behavior and concurrency sizing remain environment-specific acceptance.

## Security and secrets boundary

P16 and the 0.1.2 repair preserve the closed authorization, service/environment isolation, SecretRef/AuthProfile/token isolation, redaction, TLS and no-secret-in-evidence controls. No live API key, username/password, bearer token, private key, Civil ID or personal MOJ record is committed in this evidence.

P04 Default Deny remains explicit: both global and service-scoped authorization are required where the permission model requires them; the recovery does not silently grant System Administrator service access.

No known cloud-actionable Critical/High defect remains from the exact corrected PR-head and exact integrated-main governed acceptance matrices.

## Deferred / owner-last boundaries — NOT PASS

The following remain explicitly NOT PASS where evidence is unavailable:

- authorized real MOJ UAT smoke using owner-controlled credentials/test data: `DEFERRED_EXTERNAL_NOT_PASS`;
- unproven MOJ Production operation details: `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`; Production -> UAT fallback remains forbidden;
- actual target Windows Server/IIS installation, owner-approved Production HTTPS certificate selection/binding, Production DNS/network/proxy/firewall behavior and Production performance/capacity evidence: `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS` / owner external NOT PASS;
- owner-controlled release/code signing where deployment policy requires it: `OWNER_LAST_SIGNING_NOT_PASS`;
- repository main branch protection / required-check enforcement: `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS`; live read-back on exact closure main `18efd6c71a35505105d44df673d1d75fde293a2e` reports `protected=false` and required status-check enforcement off.

These boundaries do not invalidate completed independent cloud acceptance and are not converted to PASS by CI.

## P16 closure gate — proven

All repository-defined P16 closure-transition conditions are proven:

- [x] closure PR #86 final exact head `eeb03879d4b31b04bf333c8b5d021983ace8afe9` completed **39/39 governed pull-request workflows SUCCESS**;
- [x] PR #86 normally integrated from exact current `main` using an expected-head guard;
- [x] resulting exact-new-main `18efd6c71a35505105d44df673d1d75fde293a2e` completed **36/36 governed push workflows SUCCESS**, failure=0, queued=0 and in-progress=0;
- [x] Issue #1 records the exact reconciliation head, merge SHA, exact-new-main workflow count and preserved deferred boundaries.

P16 is therefore formally **CLOSED** and P17 may be the sole canonical phase. `VERIFIED_FINAL_COMPLETE` remains forbidden until P17 final same-commit/same-artifact evidence actually satisfies `docs/FINAL_ACCEPTANCE_CRITERIA.md`.