# GSIP 0.1.0 — Release Candidate Notes

Status: **P16 release candidate — not final P17 completion**

Government Services Integration Portal (GSIP) 0.1.0 is the first governed Windows Server / IIS release candidate. The candidate remains pinned to .NET SDK `10.0.400`, target framework `net10.0`, and version `0.1.0`.

## Included product scope

- protected first-run Setup Wizard before normal Login;
- ASP.NET Core Identity login/logout, password/account policy, lockout/session controls and MFA;
- server-side RBAC, Default Deny, service-level permissions and protected administration;
- metadata-driven entities, services, environments, fields and result mappings;
- Secret Vault/AuthProfile/SecretRef isolation, safe rotation/redaction and token-cache isolation;
- generic HTTP service execution with bounded timeout/resilience and correlation identifiers;
- governed MOJ authentication variants and repository-approved five-service contract fixtures;
- request history, masking, scoped access and exports;
- tamper-evident audit, monitoring, health and protected operational diagnostics;
- Arabic RTL / English LTR responsive UI with the four canonical design-parity baselines;
- professional Windows installer/package with install, upgrade, repair, default uninstall, reinstall and explicit owner-scoped purge behavior.

## P16 acceptance additions

P16 binds automated runtime/browser evidence, recovery rehearsal, performance smoke metrics and release package/hash identity to the same exact candidate commit. The dedicated acceptance workflow also rebuilds the versioned ZIP and Setup EXE from that candidate and records byte sizes plus SHA-256 values.

## External acceptance that is not automatically passed

Authorized real MOJ UAT smoke remains `DEFERRED_EXTERNAL_NOT_PASS` wherever owner credentials/data are unavailable. Unproven Production service details remain `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`. Actual target Windows Server/IIS deployment, Production HTTPS certificate selection/binding, DNS/network/proxy/firewall behavior, code/release signing and repository branch protection remain owner/environment evidence and are `OWNER_LAST / NOT PASS` until genuinely executed and read back.

No UAT fact is promoted to Production, no Production -> UAT fallback is permitted, and no credential, token, Civil ID, personal MOJ record, private key or Production certificate belongs in source control or CI evidence.

## Finality

This release note does not claim `VERIFIED_FINAL_COMPLETE`; that claim is forbidden before P17 verifies the exact final integrated commit, exact artifacts, full CI, acceptance evidence and remaining external classifications.
