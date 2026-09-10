# GSIP 0.1.0 — Controlled UAT Checklist

Version: **0.1.0**  
Purpose: owner-controlled, non-destructive acceptance after cloud P16 evidence is green.

## Safety prerequisites

- Use only an explicitly authorized UAT environment and approved test identities/data.
- Enter API keys, usernames, passwords and other secrets only through the protected GSIP Secret Vault/AuthProfile administration. Never place them in GitHub, chat logs, screenshots, workflow variables committed to the repository or evidence files.
- Confirm the selected Service + Environment + AuthProfile binding says **UAT**. Production -> UAT fallback is forbidden.
- Do not use a real Civil ID or personal MOJ record unless the owner has explicit authorization for that exact non-destructive UAT test. Prefer approved synthetic/test identities whenever supported.
- Capture sanitized outcome evidence only: request/correlation identifiers, service/environment name, HTTP/result classification and timestamps. Do not capture bearer tokens, API-key plaintext, secret values or personal payloads.

If authorized credentials/data are unavailable, keep this boundary `DEFERRED_EXTERNAL_NOT_PASS`. Lack of external access does not invalidate independent cloud acceptance and must not be hidden by fabricated PASS evidence.

## Five-service UAT smoke

For each configured UAT service below, perform at most the minimum approved non-destructive smoke request needed to confirm the contract:

1. Marriage Cases Service
2. Is Single Basic Service
3. Marriage Couple Last Case Service
4. Family Judgment Text Service
5. Procuration Status Service

For every service verify:

- the exact UAT base URL/relative path matches current approved MOJ/CAIT evidence;
- the exact required authentication profile resolves and no sibling-service or Production credential is reused implicitly;
- missing/invalid auth fails closed;
- the request executes through the generic service execution engine;
- result mapping, masking, history and correlation identifiers are present;
- the request is visible only within the permitted request-history scope;
- audit evidence is created without secret leakage;
- no automatic fallback to another environment occurs after failure.

## Authentication-specific checks

Where the approved contract requires composite MOJ authentication:

- the configured API-key reference resolves only in the intended UAT scope;
- `/genToken` uses the approved UAT credentials through protected secret references;
- the response token is transient/cache-governed and never displayed or persisted as plaintext evidence;
- the target call receives only the authentication mechanisms required by the approved contract;
- stale/wrong-scope token material is rejected rather than reused across service/environment boundaries.

## UAT evidence record

For each executed smoke record only:

- exact application source/version and deployed artifact SHA-256;
- date/time and authorized operator;
- service and **UAT** environment;
- request/correlation identifier;
- sanitized success/failure classification;
- confirmation that no credential/personal payload was captured;
- any contract mismatch or external blocker.

A service that cannot be exercised because credentials, endpoint access, test data or owner authorization are unavailable remains `DEFERRED_EXTERNAL_NOT_PASS`; it is not a product PASS and it is not a reason to invent schema/configuration.

## Production separation

Do not reuse UAT evidence as Production proof. Every unproven Production operation remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` until official Production endpoint/authentication/contract evidence and owner authorization are available. No Production -> UAT fallback is permitted.

## Final boundary

UAT completion does not by itself establish `VERIFIED_FINAL_COMPLETE`; that claim is forbidden until P17 verifies the exact final integrated commit, release artifacts, CI, cloud acceptance and all remaining external/owner classifications.
