# P09 Independent Contract / Security Acceptance

Unit: `P09::independent-contract-security-acceptance`

Role: independent executable acceptance. This unit does not introduce a second production runtime and does not contact CAIT with real credentials from CI.

## Acceptance authority

The gate consumes the five sanitized authoritative P09 contract snapshots and the canonical MOJ metadata seed/runtime already implemented by the P09 convergence line. It rejects drift rather than inferring missing official details.

The executable gate is:

- `tests/GSIP.P09IndependentAcceptanceChecks/`
- `.github/workflows/p09-independent-acceptance.yml`

## Integrated matrix

The dedicated five-service executable verifies:

- exactly one MOJ entity and exactly five current MOJ services;
- exact official API identity/source and `PROVEN_UAT_CONTRACT` status for all five snapshots;
- a deterministic aggregate SHA-256 fingerprint of the authoritative manifest plus all five contract snapshots;
- exact UAT method, relative path and content type for every service;
- exact documented request fields, order and requiredness classification;
- no invented validation where the supplied official contract contains no validation rule;
- result mappings correspond exactly to documented success fields;
- operation authentication matches the official evidence boundary;
- the proven Marriage Cases composite API-key + Bearer requirement is preserved;
- unresolved operation-level API-key applicability for the other documented services is not invented;
- UAT and Production remain isolated with no Production-to-UAT fallback;
- five distinct UAT AuthProfiles are bound to exact Service + Environment scope;
- no implicit cross-service AuthProfile sharing and no seeded SecretRef/secret material;
- Production service-specific operation contracts remain disabled and `DEFERRED_EXTERNAL`;
- metadata seed idempotency and owner-preserved current metadata survive reruns/upgrades.

The same exact candidate CI additionally requires the already-independent regression suites for:

- P06 token-cache scope, staleness and refresh behavior;
- P07 generic execution, malformed/empty/oversize response handling, bounded retry/status classification, execution-security fail-closed behavior and sensitive-response masking;
- P08 authentication runtime, forged/stale AuthProfile and SecretRef rejection, cross-service/cross-environment isolation, missing/disabled binding rejection, wrong-scope token rejection and independent security/leakage acceptance;
- P09 token-contract variants including form token acquisition, JSON token acquisition, documented token field casing, documented TTL and fail-closed required API-key behavior.

## Evidence discipline

CI writes only sanitized acceptance metadata under `artifacts/p09-independent-acceptance-evidence/`:

- exact candidate SHA;
- authoritative snapshot fingerprint;
- pass/fail classifications for the autonomous gates;
- an aggregate evidence SHA-256 digest;
- explicit `SyntheticOnly=true` / `RealCredentials=false` classification.

The workflow rejects evidence containing bearer credentials, API-key values, credential assignments, 12-digit Civil-ID-like values or personal payload/result markers. Raw personal MOJ responses are never part of acceptance evidence.

## Live UAT boundary

No live UAT result is promoted by this autonomous gate. When owner-held credentials or approved test records are unavailable, live UAT remains:

`OWNER_LAST / DEFERRED_EXTERNAL`

That classification is not PASS and does not weaken the autonomous contract/security acceptance requirement.

## Integration rule

This unit is intentionally stacked on the legitimate P09 official-contract convergence while that prerequisite is still open. It must be reconciled to and verified against the exact new `main` after the convergence PR is integrated before this unit can be merged.

`UNPUSHED_WORK=NONE`
