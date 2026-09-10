# GSIP 0.1.1 Deployment and Rollback Runbook

Deploy GSIP 0.1.1 with the existing governed Windows/IIS installer path. Preserve the current database, `App_Data/keys`, protected setup state, and the installation ownership manifest. Do not replace owner-held certificates, credentials, or Production configuration as part of this hotfix.

After upgrade, sign in as an authorized administrator, open `/moj-uat`, enter the current authorized API 129 UAT `x-api-key`, and enable the exact UAT configuration. Then use `/execute` with an approved test `civilId`. Retain only sanitized request/correlation evidence.

If rollback is required, stop new UAT execution, disable API 129 UAT from `/moj-uat`, restore the previous application package through the governed installer/rollback path, and retain the preserved database and protected state unless an independently authorized data rollback is required. Never copy UAT settings into Production.
