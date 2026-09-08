# Service Environment & Go-Live Configuration Contract

This contract is mandatory for GSIP implementation. It prevents cross-service endpoint, credential and token reuse.

## Isolation is the default

A government Entity is only an organizational grouping. **Never assume that two Services under the same Entity share an endpoint, API key, token credentials, TLS settings, proxy, timeout or authentication behavior.** Sharing is allowed only when an administrator explicitly links the services to the same Shared AuthProfile.

The canonical ownership chain is:

`Entity -> Service -> ServiceEnvironmentConfig (UAT | Production) -> AuthProfile -> SecretRef(s)`

## ServiceEnvironmentConfig

Every Service has an independent record for every configured Environment. At minimum it must support:

- `ServiceId` and `EnvironmentId`;
- `BaseUrl` / Production Endpoint Prefix (FQDN);
- `RelativePath`;
- HTTP Method;
- ContentType;
- non-secret headers and configurable header names;
- Timeout;
- TLS / certificate options;
- Proxy settings when applicable;
- Health/Test settings;
- Active flag;
- `LastTestedAt` and `LastTestStatus`;
- linked `AuthProfileId`.

UAT and Production configuration are independent. Activation of one environment must not mutate the other.

## Authentication profiles and secrets

An AuthProfile may contain configuration for API-key headers, token endpoints, username/password token flows, bearer audience/scope or other supported authentication types. Secret values are never stored in source, JSON configuration, logs or audit payloads.

Typical Go-Live metadata such as App Name, Consumer Key identifier, IP allowlist metadata and Production Endpoint Prefix may be represented in configuration. Consumer Key secret, Consumer Secret, API keys, passwords, token secrets and private key material must be persisted only through the Secret Vault as `SecretRef` values.

Each Service + Environment uses independent SecretRefs by default. The system must never clone or propagate a secret to another service automatically. A Shared AuthProfile is an explicit administrator action and must be visible/auditable.

## Token cache boundary

Every token cache key must include at least:

`ServiceId + EnvironmentId + AuthProfileId`

and must also include any audience/scope or other auth parameter that changes token validity. A token obtained for Service A must never be eligible for Service B unless both services intentionally resolve to the same explicitly shared profile and every effective cache-key dimension matches.

## Administration and Setup UX

Configuration UX must expose:

`Entity -> Service -> Environments -> UAT | Production`

For each Service + Environment, authorized administrators need:

- Edit;
- Test Connection;
- Test Authentication;
- Activate / Disable;
- Rotate Secret.

Setup Wizard may create entity/service/environment placeholders, but real Go-Live credentials are entered and tested through Setup/Admin UI without source changes.

## Mandatory isolation tests

Automated acceptance must prove:

1. Service A cannot resolve Service B's endpoint.
2. Service A cannot resolve Service B's SecretRef.
3. Service A cannot reuse Service B's cached token.
4. Rotating a secret for one service does not affect another service.
5. The only exception is an explicitly configured Shared AuthProfile, and that sharing remains auditable.
6. UAT and Production bindings for the same service remain isolated.

This contract applies to metadata, vault/authentication, execution engine, MOJ configuration, administration/diagnostics, Setup and final acceptance phases.
