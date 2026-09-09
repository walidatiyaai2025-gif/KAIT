# P02 SQL Authentication Regression — Repair Evidence

Status: **OPEN until exact-head and exact-main gates are green**

Regression unit: `P02-REGRESSION::setup-sql-auth-runtime`

## Owner reproduction baseline

From the same Windows host, network reachability to the SQL Server TCP endpoint succeeds and `sqlcmd` using SQL Authentication to `master` succeeds, while the pre-repair GSIP Setup Wizard returned only the generic `SQL_CONNECTION_FAILED` result.

No owner credential, API key, JWT, Civil ID, MOJ personal data, password, or raw secret-bearing connection string is recorded in this evidence.

## Canonical repair

The repair remains inside the existing P02 Setup Wizard and canonical `SetupService`; it does not introduce a second database runtime.

- The database POST now receives an explicit `authenticationMode` value (`windows` or `sql`) and applies it through `DatabaseAuthenticationBinding`.
- `sql` sets `UseWindowsAuthentication=false` and preserves the posted SQL username/password transiently for the canonical connection-string builder.
- `windows` sets `UseWindowsAuthentication=true` and clears SQL username/password immediately.
- Unknown authentication-mode values fail closed with `SQL_AUTH_MODE_INVALID`.
- `SetupService.TestDatabaseAsync` still builds the canonical connection string with initial catalog `master`.
- `SqlConnectionStringBuilder` still preserves the caller-selected `Encrypt` and `TrustServerCertificate` values. The repair does not auto-enable `TrustServerCertificate` and does not weaken TLS.
- SQL username/password are never written to logs. Existing draft/completion setup state remains protected with ASP.NET Core Data Protection; the database password is not rendered back into HTML.

## Sanitized diagnostics

`TestDatabaseAsync` now classifies SQL failures without returning `SqlException.Message`, raw connection strings, usernames, or passwords:

| Result code | Classification |
| --- | --- |
| `SQL_AUTHENTICATION_FAILED` | Login / SQL authentication rejection |
| `SQL_CONNECTION_TIMEOUT` | Connection timeout |
| `SQL_NETWORK_OR_INSTANCE_FAILED` | Network, port, server, or instance reachability |
| `SQL_TLS_CERTIFICATE_FAILED` | TLS / certificate validation |
| `SQL_CONFIGURATION_INVALID` | Invalid client connection configuration |
| `SQL_CONNECTION_UNKNOWN` | Unknown SQL error; only the numeric SQL error number is exposed |

For an explicit SQL Server TCP port the Wizard documents SqlClient endpoint syntax such as `tcp:server,1433`; it does not silently rewrite the owner-entered server value.

## Executable acceptance

### Existing Windows P02 gate

`.github/workflows/p02-setup.yml` continues to run:

1. solution restore/build;
2. P00 baseline contracts;
3. P01 architecture/design contracts;
4. P02 LocalDB setup/security regression checks;
5. first-run bilingual browser evidence.

`tests/GSIP.P02Checks` additionally verifies explicit mode binding, fail-closed invalid mode behavior, canonical `master` target markers, sanitized classifier coverage, protected-state non-disclosure, and wrong-SQL-credential classification.

### Dedicated SQL Authentication gate

The same P02 workflow now starts an ephemeral SQL Server 2022 container on a GitHub-hosted runner. The SQL administrator password is generated only at workflow runtime, immediately masked with the runner masking command, and never committed to the repository.

`tests/GSIP.P02SqlAuthChecks` verifies:

1. explicit `sql` binding remains SQL Authentication;
2. username/password reach the canonical `SqlConnectionStringBuilder` transiently;
3. `InitialCatalog=master`;
4. `IntegratedSecurity=false`;
5. `Encrypt=true` and `TrustServerCertificate=true` remain exactly as selected for the synthetic test environment;
6. a real SQL Authentication connection succeeds;
7. a deliberately wrong password returns the sanitized `SQL_AUTHENTICATION_FAILED` classification;
8. runtime credentials and server material are not present in the protected setup-state file;
9. switching explicitly to Windows Authentication clears the SQL credential fields.

## Owner-last same-host smoke action

After this repair is merged and exact-main CI is green, the owner should rerun the Setup Wizard from the same Windows host that already proved `sqlcmd` SQL Authentication to `master`:

1. enter the same SQL Server endpoint using valid SqlClient server syntax;
2. select **SQL Authentication — username + password** explicitly;
3. enter the SQL login without sharing it in GitHub/chat evidence;
4. leave `Encrypt` and `Trust Server Certificate` at the values required by the deployed SQL certificate policy;
5. select **Test Connection**;
6. record only PASS or the sanitized operation code if it still fails.

This owner-host smoke is operational confirmation only. It must not be marked PASS before it is actually performed, and no credential or raw connection string may be attached as evidence.
