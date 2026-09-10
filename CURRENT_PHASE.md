# CURRENT_PHASE.md

## Canonical current phase

**P16 — Full automated acceptance and release candidate**

Status: **OPEN / ACTIVE**

P15 is formally **CLOSED**. Its implementation PR #75 final exact head `1c1af59fac0489d645e5323a9c6c97f8a76bc5a7` completed **37/37 governed pull-request workflows SUCCESS** and integrated as exact implementation main `d60318da5b19d06df20864083f5a4a78b9792e88`, which completed **34/34 governed push workflows SUCCESS**. Closure transition PR #77 exact head `7ad7f07b760f3102bd2776cdfe4e35d2fb427765` completed **37/37 governed pull-request workflows SUCCESS** and normally integrated as exact main `48267786e9f0d21934dee339eed32688b6e2d488`; that resulting exact-new-main completed **34/34 governed push workflows SUCCESS**, with failure=0, queued=0 and in-progress=0 at formal P15 closure verification.

P14 remains **CLOSED**. Its implementation PR #73 exact head `3eca150b67663ec3e5c2d5ea918d4b75cfd9b2de` integrated as implementation main `1f1def164d639c75d9cc26710a905cc491355a2b`; closure PR #74 exact head `58022cc53c75a986cca3cb119671c213ca1fdf4d` integrated as exact main `f5dd87de7aa3f8e709d04cf8804f53c10c938633`.

P13 remains **CLOSED** from preserved exact provenance: final implementation head `1576764a16ea6ddfed735cb0824cb38de26c83d8`, integrated implementation main `203cc28db714fae5c2c70e85adc9cc2306bb2107`, followed by closure-transition main `9f76f6593123a86c1ef999ba5ec5b7b9338a53de`.

P16 is therefore the sole canonical implementation/acceptance phase. P17 remains **LOCKED** until P16 closes normally.

## Active P16 acceptance

Canonical unit: `P16::full-acceptance-release-candidate`.
Canonical branch: `worker/p16-full-acceptance`.
Canonical base: exact closed-P15 main `48267786e9f0d21934dee339eed32688b6e2d488`.

P16 must aggregate and independently re-prove the exact candidate without replacing the established P00-P15 engines. Required cloud-actionable acceptance includes:

- clean first-run Setup -> administrator login/MFA/account-security regression -> operator/user authorization and Default Deny;
- metadata-driven MOJ five-service contract/mocks/fixtures, exact Service + Environment + AuthProfile/SecretRef isolation and no Production -> UAT fallback;
- service execution, mapped result/raw response/history, request scopes and permissioned exports;
- secret rotation/cache/redaction and health/diagnostics authorization;
- audit append-only/tamper/retention/concurrency/monitoring acceptance;
- validated SQL Server + runtime-owned `App_Data` backup/restore rehearsal using synthetic data only;
- unit/integration/E2E/security/localization and bounded performance-smoke evidence;
- full UI Design Parity acceptance for Home, Service Execution, Permissions and Audit in English LTR and Arabic RTL at desktop and narrow responsive viewports, with exact-candidate screenshots and semantic hierarchy checks;
- P15 installer/package regression, exact release-candidate version/source/hash identity, release notes, migration notes, deployment/rollback runbook and UAT checklist.

## P15 package baseline preserved by P16

P15 exact integrated package run `34440072557` completed `P15_INSTALLER_STATIC_ACCEPTANCE=PASS checks=45`, package build with 0 warnings / 0 errors, ownership/destructive-operation negatives, clean install, explicit synthetic `0.0.9` upgrade, repair, default uninstall, reinstall, explicit owner-scoped purge, protected-state preservation, sanitized logging, execution-plan integrity and SHA-256 verification.

Exact integrated P15 package identities tied to source `d60318da5b19d06df20864083f5a4a78b9792e88`:

- `GSIP-0.1.0-win-x64.zip` — SHA-256 `5c25b2df2970d2a52ce570ae1419630b8b22c244b092cea38516975e2ec86e4b`;
- `GSIP-0.1.0-Setup-x64.exe` — SHA-256 `971dfe4a50ce7c8a834bc20ec18c5ed490881808a4a5b9e037cf38fb1e044c03`;
- Actions artifact `10137632143` — archive digest `sha256:20aea809e37f4ca716ebe6b6b0c587cbbf80f5224e93d890ec18835c1d6a6846`, size 62,159,500 bytes.

P16 may generate a newer exact-candidate package/hash set from its own source commit; if it does, that newer candidate identity supersedes this P15 baseline for release-candidate evidence. P17 must later verify final source/artifacts still match.

## Owner-last / external boundaries — NOT PASS

P16 must continue independently when these are unavailable; none may be promoted to PASS without real owner/external evidence.

- `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS`: actual owner Windows Server/IIS installation, approved Production certificate selection/binding, DNS/proxy/firewall/network behavior and target-host health remain owner-controlled. Owner acceptance: install the exact approved candidate on the target, explicitly choose the approved Local Computer/Personal certificate, verify IIS site/app-pool/host/port/SNI/ACL, protected `/setup` flow and application health, and retain only sanitized evidence.
- `OWNER_LAST_SIGNING_NOT_PASS`: any required code/release signing needs owner-controlled signing identity/certificate. Owner acceptance: sign through the approved private-key process, validate signature/trust on the target policy context, then recompute and record the post-signing SHA-256 without exposing private-key material.
- `DEFERRED_EXTERNAL_NOT_PASS`: authorized live MOJ UAT credentials and approved non-destructive test records remain external where unavailable. Owner acceptance: enter secrets only through protected AuthProfile/Secret Vault administration, verify exact UAT ServiceEnvironmentConfig scope, run one authorized non-destructive smoke operation for each canonical service, and record only sanitized RequestId/CorrelationId/timestamp/outcome evidence.
- `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`: official Production operation/authentication evidence, reachability, authorized credentials and test authorization remain external. Owner acceptance: obtain official Production contract/security evidence, configure Production independently and run the minimum authorized non-destructive acceptance. UAT behavior must never be inferred or copied into Production.
- `OWNER_LAST / NOT PASS` repository administration: latest live read-back before P16 entry showed `main protected=false`, required status-check enforcement off and repository rulesets empty. Owner acceptance: an authorized repository administrator applies the approved main protection/ruleset policy and an independent read-back proves it. Documentation or intent is not PASS.

## P16 exit condition

P16 may close only when all lawful cloud-actionable acceptance is terminal and all of the following are true on one exact candidate:

1. P16 implementation/evidence is pushed and represented by one exact PR head;
2. all governed pull-request workflows, including dedicated P16 acceptance, are terminal SUCCESS on that exact head;
3. no cloud-actionable Critical/High security, correctness, recovery, localization or critical UI-parity blocker remains;
4. the four canonical reference screens have exact-candidate Arabic/English desktop+narrow screenshot evidence and semantic parity acceptance;
5. backup/restore rehearsal, performance smoke and release-candidate documentation are validated;
6. all owner/external items above remain explicitly NOT PASS unless genuine evidence exists;
7. the P16 PR is normally integrated from current main;
8. every governed workflow on the resulting exact-new-main SHA is terminal SUCCESS;
9. closure evidence/ledger/project control are reconciled before P17 authorization.

## Locked future work

**P17 — Final convergence and release closure** is **LOCKED** until P16 is formally closed. Only P17 may make a final-completion claim, and only when final acceptance permits it.
