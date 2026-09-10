# GSIP 0.1.1 Hotfix — MOJ UAT configuration

## Scope

This hotfix closes the owner-observed demo/UAT configuration gap for API 129 (`MARRIAGECASES`) without weakening Production isolation or storing credential material in source/configuration text.

## Corrected behavior

- Adds an administrator-only `/moj-uat` workflow for API 129 UAT.
- The workflow accepts only the owner-held `x-api-key` as a write-only secret.
- It converts the exact API 129 UAT AuthProfile to `ApiKeyHeader` for the current test configuration.
- It configures the exact UAT target as `POST /marriageCasesAPIGEE` with `application/x-www-form-urlencoded` and removes TokenEndpoint metadata from that active runtime configuration.
- It preserves the existing `civilId` request field and hands off to the canonical `/execute` runtime for the actual service call.
- Secret plaintext is never redisplayed, is stored through `ISecretVault`, and replacements use the existing atomic rotation path.
- The AuthProfile is disabled while being reconfigured and is enabled only after secret persistence succeeds.
- Production is not mutated, inferred, enabled, or populated by this hotfix.
- The page includes an explicit UAT disable action.
- P15/P16 package and acceptance contracts are made patch-version aware without lowering the accepted 0.1.0 baseline.

## Security boundaries

No API key, username, password, bearer token, Civil ID, or other owner-held sensitive value is committed. The hotfix does not add Production fallback. Existing RBAC remains mandatory: the configuration screen requires both `ServiceSecrets.Manage` and `Services.Manage`.

## Version

`Directory.Build.props` advances from `0.1.0` to `0.1.1`. Packaging therefore produces `GSIP-0.1.1-win-x64.zip` and `GSIP-0.1.1-Setup-x64.exe` through the existing governed packaging pipeline. Version-specific 0.1.1 release, migration, rollback, and UAT handoff documents are included.

## Acceptance

`.github/workflows/hotfix-0.1.1.yml` builds the full solution and runs `scripts/verify_hotfix_0_1_1.py`, plus the preserved P15 and P16 source-contract gates. Existing repository workflows remain authoritative and must also be green on the exact hotfix head before merge.

Live MOJ UAT success remains owner-executed evidence because the repository contains no credentials or personal test records. A successful build/configuration gate is not represented as a live external UAT PASS.
