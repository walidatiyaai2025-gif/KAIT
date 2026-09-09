# P08 — MOJ Authentication Integration Closure Evidence

## Result

P08 implementation is **closure-ready** from exact integrated implementation baseline:

`67968a9230dae453b36f48549eda138d7b5401c7`

This record covers implementation, security acceptance, exact-main regression evidence and owner-last external evidence classification. The closure reconciliation itself is governance/evidence-only and introduces no P09 service payload implementation.

## Integrated work

- PR #43 / #44: fail-closed AuthProfile/SecretRef scope isolation and executable isolation matrix.
- PR #45: MOJ authentication runtime, recovered into the final convergence line.
- PR #47: authoritative convergence of metadata-driven token endpoint, API-key/Bearer composition, exact-scope cache/secret behavior, authorized redacted administration Test action, independent security acceptance and CI wiring.
- PR #48: independent acceptance line superseded after its legitimate delta was recovered into PR #47; no second runtime was created.
- PR #46: historical contract/evidence work was stale against the integrated runtime; its useful matrix/audit intent is recovered by the P08 closure reconciliation rather than merging stale findings.

## P08 exit criteria traceability

| Exit requirement | Exact integrated evidence | Result |
|---|---|---|
| Implement the authoritative MOJ authentication contract from repository-preserved official evidence. | Metadata-driven `x-api-key`, documented form-urlencoded token acquisition, documented `data` response shape and transient Bearer attachment are integrated. | PASS |
| Base URL, header name and paths are configuration/metadata driven rather than production hard-coded. | Runtime consumes governed environment/service metadata; token endpoint path is validated and participates in cache identity; synthetic non-default path acceptance is present. | PASS |
| Exact Service + Environment + AuthProfile isolation. | P08 Auth Scope Isolation rejects forged/stale AuthProfile and SecretRef, cross-service/cross-environment use, Production→UAT fallback, disabled/missing binding and stale/wrong-scope token reuse. | PASS |
| Negative/security acceptance is executable. | Independent security acceptance includes missing/invalid auth, malformed/empty token, HTTP/network/TLS/timeout/cancellation, refresh atomicity, bounded retry/no-auth-storm and secret/token leakage checks. | PASS |
| Token-generation Test action exists in administration and is protected. | `AuthProfileAuthenticationController.TestTokenGeneration` requires `ServiceSecrets.Manage`, validates anti-forgery, resolves exact scope and exposes pass/fail state only. | PASS |
| No credential/token disclosure. | Exact-main leakage scan and evidence check completed successfully. | PASS |
| Preserve closed P00–P07 baselines. | All exact-main push workflows on the integrated baseline completed successfully. | PASS |
| No P09+ implementation introduced by P08 closure. | Closure delta is documentation/governance/CI only. | PASS |

## Exact-main CI

Exact implementation baseline: `67968a9230dae453b36f48549eda138d7b5401c7`.

After PR #47 integration, **17/17 exact-main push workflows completed SUCCESS**, with failure=0, queued=0 and in-progress=0 after completion.

P08 Security Acceptance:

- Run: `34318032200` — SUCCESS.
- Artifact: `P08-Security-Acceptance-67968a9230dae453b36f48549eda138d7b5401c7`.
- Size: 2,314 bytes.
- SHA-256: `b39ea805fec25b5a45a35c2eb445074e64622f48a44dc7c7e3ccd6a0ea8b3d98`.
- Artifact was non-expired when closure evidence was reconciled.

The composed acceptance verifies:

- 13 independent authentication/security boundary cases;
- 9 MOJ runtime cases, including `valid-genToken-bearer`;
- P07 execution authorization/binding regression;
- P06 token-cache regression;
- P08 Auth Scope Isolation;
- execution-plan/governance validation;
- secret/token leakage scanning.

PR #47 exact-head verification completed **22/22 SUCCESS** before normal merge.

## Official-fact boundary and owner-last evidence at original closure

The repository preserved the official contract facts then available to implement the generic authentication mechanisms. One operation-level fact was not asserted at original closure: whether `ApiKeyAuth` was required specifically on `/genToken` in every target CAIT/MOJ environment.

Historical status at P08 closure: **DEFERRED_EXTERNAL — NOT PASS**.

That historical classification was correct for the evidence then available and is not rewritten retroactively.

## Closure decision

All autonomous P08 implementation, tests, negative/security acceptance, redaction/isolation controls, administration support, documentation and exact-main regression evidence were complete for the evidence available at closure. The remaining live operation-level applicability check was explicitly external under `docs/OWNER_LAST_EXECUTION_POLICY.md`.

P08 subsequently transitioned to **CLOSED** and P09 became **OPEN / READY**. No `FINAL_COMPLETE` claim is made.

## Post-closure external-contract repair — 2026-09-09

After closure, the owner supplied authenticated official CAIT Swagger evidence for Marriage Cases API 129 and an owner-executed successful UAT `/genToken` call. That new evidence resolves the former operation-level question for the **observed UAT Marriage contract only**:

- `/genToken`: `x-api-key` + form `username`/`password` -> response `data` token;
- Marriage target: same `x-api-key` + acquired Bearer token;
- supplied UAT Swagger base preserves `/WSWEB/WS/v1/marriage`;
- supplied target operation is `POST /marriageCasesAPIGEE`, form-urlencoded, with required `civilId` field.

Status for that observed UAT contract: **UAT_PROVEN**.

PR #50 repairs the closed P08 baseline by extending the existing canonical runtime and protected Test Authentication path. It does not create a second authentication engine and does not start unrelated P09 payload implementation. Synthetic acceptance uses `synthetic.invalid` only and validates exact Service + Environment + AuthProfile secret resolution, API-key generation in cache validity, fail-closed behavior, no Production→UAT fallback, bounded/no-storm behavior and no plaintext leakage.

Production remains separate: owner evidence proves only the Production gateway prefix `https://moj.api.cait.gov.kw`. No Production Marriage suffix, `/genToken` security applicability, target operation suffix or payload contract is inferred. Status for those details: **PRODUCTION_DEFERRED_EXTERNAL — NOT PASS**.

See `docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md` for exact repair evidence and the PR-head acceptance baseline. This additive section does not alter the historical closure SHA, workflow evidence or decision.
