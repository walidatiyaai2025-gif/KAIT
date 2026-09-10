# GSIP 0.1.0 — Deployment and Rollback Runbook

Version: **0.1.0**  
Target: **Windows Server / IIS + Microsoft SQL Server**  
Package names: `GSIP-0.1.0-win-x64.zip`, `GSIP-0.1.0-Setup-x64.exe`

## Release identity gate

Deploy only artifacts produced by the accepted exact P16/P17 candidate. Before installation, record and verify:

- exact repository commit SHA;
- product version `0.1.0` and pinned .NET 10 toolchain;
- package and Setup EXE filenames;
- artifact byte sizes;
- SHA-256 for both files;
- CI run/artifact identity that produced them.

A hash mismatch, stale source SHA, dirty/unpushed source, failing exact-candidate CI or missing acceptance evidence blocks deployment.

## Pre-deployment owner actions

The deployment owner must provide/approve the real environment inputs that CI cannot manufacture:

- Windows Server/IIS target and administrative maintenance window;
- .NET 10 Hosting Bundle and IIS prerequisites;
- Microsoft SQL Server endpoint/database and least-privilege credentials entered only through the protected setup path;
- approved HTTPS certificate from Local Computer certificate storage, hostname/SNI binding and certificate lifecycle ownership;
- Production DNS/network/proxy/firewall routes;
- code/release-signing certificate material where organizational policy requires signing.

These remain `OWNER_LAST / NOT PASS` until genuinely executed and independently read back on the target environment.

## Pre-change recovery checkpoint

1. Take and verify an approved SQL Server backup of the exact GSIP database.
2. Back up runtime-owned `App_Data`, especially `keys` and `setup` protected state.
3. Record the current deployed package/version/hash and IIS site/application-pool/binding identity.
4. Confirm a previously accepted rollback package is available and hash-verified.
5. Stop or drain the IIS site/application pool as required by the maintenance plan.

Treat the SQL backup, App_Data backup and previous package as one rollback set. Data Protection key material is machine/context sensitive; do not transplant it to another host or identity without an approved Data Protection migration design.

## Installation / upgrade

1. Verify the 0.1.0 Setup EXE SHA-256 against the accepted CI release manifest.
2. Run the installer against the intended owner-controlled install root/site/app pool.
3. Use explicit HTTP/HTTPS binding values. For HTTPS, select the approved certificate and validate hostname/SNI behavior.
4. Preserve runtime-owned App_Data; do not use explicit purge for a normal upgrade/repair.
5. Complete the protected first-run Setup only when the environment is genuinely new; an existing completed setup must remain protected from accidental rerun.
6. Apply only the accepted repository EF Core migration set.
7. Start/recycle the intended IIS application pool and site.

## Post-deployment checks

- `/health/live` responds successfully through the intended route;
- protected Login renders in English LTR and Arabic RTL;
- existing setup state is preserved or, for a clean deployment, first-run Setup appears before normal Login;
- authorized administrator can reach protected operations without widening permissions;
- service/environment/auth bindings remain exact and no Production -> UAT fallback occurs;
- logs are sanitized and contain no secret/token/API-key plaintext;
- HTTPS certificate, hostname and SNI are independently read back from IIS when HTTPS is used.

Authorized real MOJ UAT smoke is performed only with owner-approved credentials/data and remains `DEFERRED_EXTERNAL_NOT_PASS` when unavailable. Production MOJ operation proof remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` until separately proven.

## Rollback trigger

Rollback rather than bypass the gate when any release-blocking condition remains after bounded repair, including startup failure, migration failure, broken protected setup/login path, security/authorization regression, unrecoverable critical visual drift, incorrect IIS binding/certificate identity or incompatible persistent-state behavior.

## Rollback procedure

1. Stop/drain the GSIP IIS application.
2. Preserve failure diagnostics without copying secrets or personal MOJ data into tickets/evidence.
3. If the database changed, restore the pre-change verified SQL Server backup according to `DATABASE_MIGRATION_NOTES.md`.
4. If protected App_Data state changed or was damaged, restore the matching pre-change App_Data backup.
5. Reinstall the previously accepted package whose version/hash belongs to that recovery set.
6. Restore the previous IIS binding/site/app-pool configuration if the failed deployment changed it.
7. Start the application and repeat health, Login, setup-state and authorization smoke checks.
8. Record rollback source SHA, artifact hash, database backup identity, App_Data backup identity and outcome.

Never repair a Production failure by silently pointing the service at UAT, by inventing service metadata, or by bypassing authentication/authorization.

## Completion boundary

This runbook is a supported procedure and P16 exercises its recoverability assumptions with synthetic LocalDB/App_Data evidence. Actual target-server deployment, real TLS/certificate behavior, signing and Production recovery timing remain owner/environment evidence. This document does not claim `VERIFIED_FINAL_COMPLETE`; that claim is forbidden before P17 exact final evidence exists.
