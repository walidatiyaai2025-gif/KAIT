# GSIP 0.1.2 Database Migration Notes

Version: **0.1.2**

No EF schema migration is required.

The hotfix performs no broad startup rewrite of MOJ authentication metadata. Canonical P09 metadata remains the authority for each Service + Environment pair.

For API 129 UAT only, the administrator can reconcile the 0.1.1 API-key-only profile through `/moj-uat`:

- the exact UAT owner binding is validated before any change;
- the profile is disabled before transition and converted to `TokenEndpoint` only for the known API129/UAT scope;
- known obsolete bearer/token slots are retired inside a serializable, rollback-capable transaction;
- the proven `/genToken` metadata is restored with required `x-api-key`, `username`, and `password` semantics;
- the three required secrets are created or rotated through the protected Secret Vault and the profile is enabled only after all required slots exist;
- incompatible or ambiguous authentication scopes remain disabled rather than being guessed or rewritten.

Production configuration is not changed. No placeholder username/password rows are created, and no authentication assumptions are propagated to other MOJ services.
