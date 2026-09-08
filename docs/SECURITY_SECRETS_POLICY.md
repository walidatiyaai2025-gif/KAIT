# SECURITY & SECRETS POLICY

## Prohibited repository content

Never commit:

- CAIT/MOJ API keys or consumer secrets;
- bearer/JWT/access/refresh tokens;
- user passwords or database passwords;
- production/UAT private keys or PFX/P12 files containing private keys;
- connection strings containing credentials;
- real Civil IDs, personal names, case data or other real personal records in fixtures/screenshots;
- unredacted Authorization, Cookie or `x-api-key` headers;
- secret-bearing setup/export/diagnostic logs.

## Runtime storage

Implement secret storage through the P06 vault abstraction. Windows deployment should use ASP.NET Core Data Protection with appropriately protected key storage (for example DPAPI/certificate-backed protection according to deployment identity and operational requirements). Secrets must be replaceable/rotatable without source changes.

## Configuration

Configuration may contain non-secret endpoints, feature flags and secret *references*. Local secret files, environment overrides and IDE user secrets must remain ignored by Git.

## Logging/audit

Redact secrets before logs or audit persistence. Mask sensitive request/response fields according to service metadata. Audit the fact that a secret was created/tested/rotated/revoked, never the secret value.

## Tests

Tests use synthetic values only. Secret-leakage tests should scan application logs, exception paths, HTTP diagnostics and generated artifacts. Public fixtures must not resemble live credentials copied from screenshots or previous prototypes.

## Incident rule

If a real credential is discovered in Git history or evidence, treat it as compromised: stop propagation, remove it from current artifacts where possible, notify the owner to rotate/revoke it, and add regression protection. Do not repeat the credential in issue comments while documenting the incident.
