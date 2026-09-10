# GSIP 0.1.0 — Database Migration Notes

Version: **0.1.0**  
Database target: **Microsoft SQL Server**  
Migration mechanism: **Entity Framework Core migrations through the protected GSIP setup/runtime path**

## Pre-migration requirements

Before applying 0.1.0 to a persistent environment:

1. identify the exact GSIP database and confirm the intended environment; never infer Production configuration from UAT;
2. take a SQL Server backup using the organization's approved encrypted/retained backup location and verify that the backup is readable;
3. preserve the GSIP mutable application state under `App_Data`, especially Data Protection keys and protected first-run setup state;
4. record the currently deployed product version, package SHA-256, database backup identity and App_Data backup identity;
5. stop or drain the IIS application before a rollback-sensitive migration when required by the deployment change window.

P16 mechanically rehearses the database part with SQL Server LocalDB: migrations create the candidate schema, synthetic state is written, `BACKUP DATABASE` and `RESTORE VERIFYONLY` are executed, the database is removed, `RESTORE DATABASE` is executed, and the synthetic state is verified after restore. It separately rehearses App_Data backup/delete/restore with SHA-256 preservation. This is synthetic cloud evidence, not Production backup evidence.

## Upgrade behavior

The 0.1.0 application uses the repository's canonical EF Core migration set. A deployment must run only migrations present in the accepted release candidate. Manual ad-hoc schema edits are outside the supported 0.1.0 deployment procedure.

The installer preserves runtime-owned `App_Data` across install, upgrade, repair and default uninstall. Database credentials are not embedded in the package; SQL Server configuration remains part of the protected setup/environment configuration boundary.

## Rollback rule

Do not assume every EF migration has a safe destructive `Down` path for populated Production data. If a deployment must be rolled back after a schema-changing migration, the supported conservative procedure is:

- stop the application;
- restore the pre-change SQL Server backup to the approved target;
- restore the matching protected App_Data state when that state was changed as part of the failed deployment;
- redeploy the previously approved application package whose source/version/hash matches that database snapshot;
- run protected health/login checks before reopening traffic.

Application binaries, database state and Data Protection/setup state must be treated as one recovery set. Mixing a newer database with an older package, or restoring Data Protection keys from an unrelated environment, is not an accepted rollback.

## Owner/environment evidence

Production backup storage, retention, encryption, SQL permissions, maintenance window, restore target, DPAPI machine context and disaster-recovery timings are environment-specific. They remain `OWNER_LAST / NOT PASS` until validated on the owner-controlled Windows/IIS/SQL environment. P16 automation does not convert those items to PASS.
