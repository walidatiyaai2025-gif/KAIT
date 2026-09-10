# GSIP 0.1.1 Database Migration Notes

GSIP 0.1.1 introduces no Entity Framework database schema migration. Existing 0.1.0 databases remain compatible.

The MOJ API 129 UAT configuration change is applied through existing `CatalogServices`, `CatalogEnvironments`, `ServiceEnvironmentConfigs`, `AuthProfiles`, `AuthProfileBindings`, `AuthProfileSecrets`, and protected `SecretVaultEntries` structures. The API key must be entered through the application so Data Protection is used; it must not be inserted as plaintext SQL.

Before upgrading, retain the normal GSIP database and `App_Data` backups. The existing installer preservation and rollback rules remain applicable.
