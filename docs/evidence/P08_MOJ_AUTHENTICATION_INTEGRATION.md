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

## Official-fact boundary and owner-last evidence

The repository preserves the official contract facts needed to implement the generic authentication mechanisms. One operation-level fact is not asserted: whether `ApiKeyAuth` is required specifically on `/genToken` in every target CAIT/MOJ environment.

Status: **DEFERRED_EXTERNAL — NOT PASS**.

Missing dependency: authenticated operation-level CAIT/MOJ documentation or authorized UAT configuration/credentials. Repository policy explicitly allows irreducible live-UAT evidence to be deferred after all safe deterministic work is complete; it must never be relabeled PASS without real evidence.

Safe substitute evidence already complete:

- official fixture/summary validation;
- synthetic/local contract tests;
- exact-scope secret and token isolation;
- malformed/unauthorized/failure-path acceptance;
- bounded retry/cancellation behavior;
- administration Test Authentication path;
- exact-main regressions and leakage scan.

Owner/operator action when authorized UAT evidence exists:

1. Enter the real secret material only through the protected AuthProfile/Secret Vault administration path.
2. Bind it to the exact MOJ Service + UAT Environment + AuthProfile; do not share or copy Production bindings.
3. Configure the operation-level auth metadata exactly from the authenticated official specification.
4. Run the protected **Test Authentication** action.
5. Expected result: successful token/authentication behavior for that exact binding, no fallback and no plaintext secret/token in UI/logs/evidence.
6. If the official operation contract differs from the configured metadata, keep the check failed/deferred and repair configuration/contract evidence; do not weaken isolation or add implicit fallback.

## Closure decision

All autonomous P08 implementation, tests, negative/security acceptance, redaction/isolation controls, administration support, documentation and exact-main regression evidence are complete. The remaining live operation-level applicability check is explicitly external and remains deferred under `docs/OWNER_LAST_EXECUTION_POLICY.md`.

Therefore P08 can transition to **CLOSED** through the governance-only closure PR if its exact-head closure gate and repository regression matrix are green. P09 may become **OPEN / READY** only after that transition is normally merged and the exact new main is reverified. No `FINAL_COMPLETE` claim is made.
