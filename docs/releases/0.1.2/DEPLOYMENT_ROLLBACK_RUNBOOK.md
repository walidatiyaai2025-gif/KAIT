# GSIP 0.1.2 Deployment and Rollback Runbook

Version: **0.1.2**

## Deploy

1. Back up the GSIP database and Data Protection key material.
2. Deploy the exact 0.1.2 candidate from the governed release artifact.
3. Start the application once and allow startup convergence to complete.
4. Sign in as System Administrator and open `/moj-uat`.
5. Enter the UAT `x-api-key` through the write-only field and save.
6. Confirm the page reports `TokenEndpoint`, x-api-key `READY`, and `READY_FOR_UAT_TOKEN_EXECUTION`.
7. Open the service test and execute only with an authorized UAT Civil ID.
8. Confirm `/genToken` succeeds and the service request completes through Bearer authentication.

## Rollback

1. Stop GSIP.
2. Restore the previous application binaries.
3. If a database rollback is required, restore the pre-deployment database backup together with its matching Data Protection key material.
4. Do not copy UAT credentials into Production during rollback.

0.1.2 has no schema migration. The startup convergence may add encrypted UAT placeholder secret entries and repair RBAC rows; restoring the pre-deployment database returns those data changes to the prior state.
