# OWNER-LAST EXECUTION POLICY

The system is intended to be completed autonomously as far as the available repository/cloud/build environment permits.

## Do not stop early

A worker must not stop merely because one genuine external dependency is unavailable, such as:

- live CAIT/MOJ UAT credentials;
- an authorized real Civil ID/test record;
- production network reachability;
- a target Windows Server/IIS host;
- a production TLS certificate/private key;
- an owner-only physical deployment action.

Before deferral, finish every independent portion: contracts, metadata model, mocks/fixtures, negative tests, integration abstraction, secret-storage path, setup UI, installer scripts, CI, health diagnostics, evidence templates, documentation and recovery behavior.

## Deferred is not PASS

A genuinely external check must be marked `DEFERRED_EXTERNAL` or equivalent with:

- exact missing dependency;
- why it cannot be simulated safely;
- all automated substitute evidence already completed;
- exact command/screen/action needed later;
- expected result and failure handling.

Never convert a deferred external check to PASS without real evidence.

## Credentials

Do not ask the owner to paste live credentials into source code, GitHub issues, PRs, screenshots, test fixtures or chat-controlled files. The product must provide its own secure setup/admin mechanism for runtime secret entry.

## Release behavior

If all source/build/test/installer/security/UI-parity gates pass but an irreducible external acceptance remains, deliver the strongest release candidate possible and identify the remaining check precisely. Do not call the product FINAL COMPLETE until the repository's final acceptance criteria actually permit that claim.

## Canonical owner-last / external execution records

The items below remain **NOT PASS** until their stated real-world evidence exists. Their presence never blocks independent cloud-actionable work.

### 1. `DEFERRED_EXTERNAL_NOT_PASS` — authorized credential-bearing UAT smoke

Missing dependency: owner-controlled UAT credentials/SecretRefs plus approved non-destructive test data for the target government service.

Exact owner action:

1. Open the deployed GSIP instance and sign in with the authorized administrative account.
2. Open `/auth-profiles` and create/select the service-specific **UAT** AuthProfile.
3. Enter each credential only through the protected Secret Vault/AuthProfile UI; do not paste a credential into Git, issue comments, PR comments, screenshots, browser devtools captures, shell history or test fixtures.
4. Open `/metadata` and verify the target service is bound to the intended **UAT** environment and to that exact AuthProfile/SecretRef set.
5. Confirm there is no Production binding, Production fallback or shared UAT→Production credential reference for that execution path.
6. Open `/execute`, choose the exact service and **UAT** environment, supply only the approved non-destructive test input, and execute one request.
7. Retain sanitized evidence only.

Evidence checklist required before PASS:

- exact application version and source SHA;
- service code and `Environment=UAT`;
- AuthProfile identifier/name without secret value;
- SecretRef identifier/name without secret value;
- UTC timestamp;
- sanitized RequestId and CorrelationId;
- governed success/failure outcome and HTTP status where exposed by GSIP;
- proof that Production fallback did not occur;
- no credential, token, Civil ID or personal payload present in evidence.

Failure handling: leave `DEFERRED_EXTERNAL_NOT_PASS`; do not weaken auth, environment isolation, certificate validation or request validation to force a pass.

### 2. `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS` — authoritative Production contract and reachability

Missing dependency: authoritative Production endpoint/auth/operation evidence, owner-approved Production reachability/credentials and explicit authorization for a non-destructive Production acceptance request.

Exact owner action:

1. Obtain the authoritative Production endpoint, authentication and operation contract from the provider/authorized agency source.
2. In `/metadata`, configure a distinct **Production** environment record from that authoritative evidence only. Never copy or infer Production settings from UAT.
3. In `/auth-profiles`, provision a Production-specific AuthProfile/SecretRef through protected administration only.
4. Verify the Production AuthProfile is not the UAT AuthProfile and no UAT SecretRef is referenced by Production.
5. With explicit authorization, run the minimum non-destructive Production acceptance request from `/execute` using `Environment=Production`.
6. Retain sanitized evidence only.

Evidence checklist required before PASS:

- authoritative Production contract/source reference and approval record;
- exact application version and source SHA;
- Production base endpoint identity with secrets/query payload removed;
- distinct Production AuthProfile/SecretRef identifiers without values;
- UTC timestamp, sanitized RequestId and CorrelationId;
- result/status of the approved non-destructive request;
- explicit proof of no Production→UAT fallback and no credential sharing;
- no live credential/token/personal identifier in retained evidence.

Failure handling: keep Production disabled/fail-closed and retain `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`; never fall back to UAT.

### 3. `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS` — target Windows Server / IIS / TLS acceptance

Missing dependency: the owner-controlled target Windows Server/IIS host, approved certificate/private key, DNS/network/firewall context and deployment authorization.

Exact owner action:

1. Use the **exact final accepted** `GSIP-<version>-Setup-x64.exe` whose SHA-256 is recorded in the final same-SHA acceptance evidence.
2. On the authorized Windows Server, verify the installer hash before execution:

```powershell
Get-FileHash -Algorithm SHA256 .\GSIP-<version>-Setup-x64.exe
```

3. Run the installer elevated, select the approved install root/site/app-pool settings, choose HTTPS, and select the approved `Local Computer\Personal` certificate with an accessible private key.
4. Configure the approved host name/SNI/port. Do not disable certificate validation or downgrade an approved HTTPS Production binding to HTTP.
5. Complete First-Run Setup using the protected setup flow; do not place credentials in command-line arguments or install logs.
6. Verify the IIS site/app-pool/binding and application health using the deployed instance.
7. Exercise repair/upgrade/default uninstall/reinstall only under the approved lifecycle acceptance plan and verify `App_Data` state preservation where required.

Evidence checklist required before PASS:

- target host identifier sanitized to the approved evidence level;
- exact installer filename and SHA-256 matching final repository evidence;
- IIS site name, app-pool name/identity and install root;
- HTTPS binding protocol/port/host/SNI;
- certificate thumbprint or approved certificate identifier, never private-key material;
- successful application/health response after deployment;
- state-preservation result for the required lifecycle operation;
- UTC timestamp and operator/approval reference;
- sanitized installer log with no secret/token/personal data.

Failure handling: retain `OWNER_LAST_TARGET_IIS_TLS_NOT_PASS`; do not overwrite unowned application state, purge `App_Data` without explicit approval, or weaken TLS/security controls.

### 4. `OWNER_LAST_SIGNING_NOT_PASS` — owner-controlled code signing

Missing dependency: owner-controlled signing certificate/private key and the organization's approved signing/timestamp policy.

Exact owner action:

1. Start from the exact final accepted installer/package bytes and recorded SHA-256.
2. Sign outside Git and outside repository CI using the owner-controlled certificate/key and the organization's approved SHA-256/timestamp policy. Example form when Windows SignTool and an approved certificate thumbprint/timestamp URL are mandated by that policy:

```powershell
signtool sign /fd SHA256 /sha1 <APPROVED_CERT_THUMBPRINT> /tr <APPROVED_RFC3161_TIMESTAMP_URL> /td SHA256 .\GSIP-<version>-Setup-x64.exe
signtool verify /pa /all /v .\GSIP-<version>-Setup-x64.exe
```

3. Record the signed artifact as a distinct handoff artifact; do not pretend its byte hash is the unsigned artifact hash.

Evidence checklist required before PASS:

- unsigned accepted artifact filename/SHA-256;
- signed artifact filename/SHA-256;
- signer subject/certificate thumbprint and validity period, with no private key export;
- successful signature verification output;
- timestamp authority/result when required;
- UTC signing/verification timestamp and owner approval reference.

Failure handling: retain `OWNER_LAST_SIGNING_NOT_PASS`; never commit/export the private key or mark an unsigned artifact signed.

### 5. `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS` — main branch repository administration

Missing dependency: GitHub repository administration permission. A managed application connection without administration permission cannot truthfully prove or apply this owner-only control.

Exact owner action:

1. In GitHub repository **Settings → Rules → Rulesets** (or the equivalent branch-protection UI), create/apply the production `main` policy targeting `main`.
2. Require pull requests before merge.
3. Require the repository's provider-bound governed status checks and require branches to be up to date before merge where supported.
4. Require conversation resolution before merge where supported.
5. Enforce the rule for administrators where supported by the selected GitHub rule type/policy.
6. Disable force pushes and branch deletion.
7. Save the rule, then independently read back the effective `main` policy rather than treating the save action as proof.

Evidence checklist required before PASS:

- repository `walidatiyaai2025-gif/KAIT` and target `main`;
- effective rule/ruleset identifier;
- PR-before-merge requirement enabled;
- exact provider-bound required-check list read back from GitHub;
- strict/up-to-date requirement read back where supported;
- conversation-resolution and administrator-enforcement state read back where supported;
- force-push disabled;
- deletion disabled;
- UTC read-back timestamp and sanitized API/UI evidence.

Failure handling: retain `OWNER_LAST_BRANCH_PROTECTION_NOT_PASS`. Repository/cloud engineering may continue on branches and PRs, but must continue using exact-head validation and `expected_head_sha` merge protection whenever a lawful merge is performed.

## Execution priority rule

Owner-only and external dependencies never terminate the project-wide sweep while any lawful cloud-actionable work remains. Workers must continue integration recovery, CI repair, tests, security checks, documentation/evidence reconciliation and exact-main validation independently. Only when cloud-actionable work is exhausted may execution stop with owner/external items still explicitly **NOT PASS** and with `UNPUSHED_WORK=NONE`.
