# GSIP 0.1.2 Deployment and Rollback Runbook

Version: **0.1.2**

## Deploy

1. Back up the GSIP database and Data Protection key material.
2. Deploy the exact governed 0.1.2 candidate.
3. Start the application and confirm normal database/metadata bootstrap completes; this hotfix does not run a broad MOJ authentication convergence rewrite.
4. Sign in as an authorized administrator and open `/moj-uat`.
5. Enter the official UAT `x-api-key`, `username`, and `password` through the write-only fields and save.
6. Confirm the page reports `TokenEndpoint` and `READY_FOR_UAT_TOKEN_EXECUTION` only after all three credential slots are present.
7. Open the service test and execute only with an authorized UAT Civil ID.
8. Confirm `/genToken` receives `x-api-key` plus username/password, returns the token through `data`, and the service request uses the same `x-api-key` plus `Authorization: Bearer`.
9. Do not treat repository-only checks as live-UAT PASS; record authorized external UAT evidence separately.

## Rollback

1. Stop GSIP.
2. Restore the previous application binaries.
3. If a database rollback is required, restore the pre-deployment database backup together with its matching Data Protection key material.
4. Do not copy UAT credentials, URLs, or token metadata into Production during rollback.

0.1.2 has no schema migration and creates no synthetic empty username/password placeholders. Any API129/UAT credential rotations performed after deployment are data changes protected by the Secret Vault; restoring the matched pre-deployment database and Data Protection keys returns that state to the prior snapshot.
