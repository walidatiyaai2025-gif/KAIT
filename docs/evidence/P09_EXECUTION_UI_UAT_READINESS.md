# P09 Execution UI and OWNER-OPERATED UAT Readiness

Unit: `P09::execution-ui-and-uat-readiness`

This evidence is additive to the authoritative P09 contract snapshots and independent security acceptance. It does not promote any unavailable live UAT result to PASS.

## Autonomous UI readiness

The existing P07 Service Execution UI remains the only execution UI. P09 adds no service-specific controller/view and no P13 redesign.

Acceptance contract:

- MOJ remains the first Entity and is selectable when the principal has `Services.Execute` on at least one MOJ service.
- Exactly the five canonical P09 services are available according to service-level permissions: Marriage Cases, Is Single Basic, Marriage Couple Last Case, Family Judgment Text and Procuration Status.
- Request controls come only from canonical `ServiceFields`, ordered by `DisplayOrder`; no field-name heuristic is allowed.
- A UAT configuration whose official method/path is known may be displayed in contract-ready/view-only state before owner-held credentials activate it. The Run action is disabled in the UI and rejected server-side until the exact configuration is Active. This does not enable Production and does not bypass the execution engine.
- Production rows with an unresolved operation method/path remain hidden/fail-closed. No Production-to-UAT fallback is permitted.
- Validation/outcome text continues through English/Arabic resources and P07 RTL/LTR browser regression.
- sensitive submitted values are not redisplayed; sensitive structured values keep masked presentation; raw responses remain behind the canonical masking engine.
- request metadata shows RequestId, CorrelationId, secret-safe `EndpointAlias`, status, duration and attempt count. It does not show BaseUrl, credentials, API keys or bearer material.
- unauthorized services remain absent from selectors and cannot be executed by forged identifiers.

Automated browser evidence uses the existing P07 local fake endpoint and synthetic-only data. It must never contain owner credentials, real Civil IDs or personal MOJ results.

## Canonical request fields shown by the generic form

| Service | Official P09 request fields |
|---|---|
| Marriage Cases | `civilId` |
| Is Single Basic | `civilId` |
| Marriage Couple Last Case | `male_civilId`, `female_civilId` |
| Family Judgment Text | `caseNo`, `type` |
| Procuration Status | `CivilClient`, `CivilAgent`, `year`, `Number` |

Requiredness/validation is taken exactly from the stored official contract snapshots. P09 does not invent regex, length or numeric rules when the official evidence does not specify them.

## OWNER-OPERATED UAT SMOKE — DEFERRED_EXTERNAL until actually performed

Preconditions for every service:

1. Use only an owner-approved UAT account and approved non-destructive test record.
2. Enter credentials through the GSIP Secret/AuthProfile administration UI only. Do not put credentials, tokens or test identifiers in Git, issue comments, screenshots, copied logs or this document.
3. Confirm the selected Entity is MOJ, the selected service is the intended service and the selected environment is UAT.
4. Confirm Production remains disabled and has no fallback to the UAT operation.
5. After the smoke, record only sanitized status/outcome, exact build SHA, RequestId/CorrelationId, endpoint alias and duration. Do not preserve a raw credential/header/token or unmasked personal result.

### Marriage Cases

Officially proven UAT sequence:

1. Configure the exact service-scoped UAT AuthProfile with the owner-held gateway key plus token username/password through write-only secret inputs.
2. Run Test Authentication: gateway API-key + username/password -> `/genToken`; accept the token only from the documented `data` response member.
3. Open Service Execution -> MOJ -> Marriage Cases -> UAT.
4. Enter one approved UAT Civil ID in the generated `civilId` field without recording it in evidence.
5. Execute the target using gateway API-key + acquired Bearer -> `/marriageCasesAPIGEE`.
6. Expected: a documented response classification (including the documented success/error statuses) is rendered safely; endpoint alias/status/duration are visible; credentials/token are absent; sensitive values remain masked.
7. Any mismatch fails the smoke. Do not retry by changing auth semantics outside official evidence.

### Is Single Basic

1. Configure the exact service-scoped UAT token credentials through the vault-backed AuthProfile.
2. Obtain the token through the documented `/genToken` contract.
3. Open MOJ -> Is Single Basic -> UAT and enter an approved test value in `civilId`.
4. Execute `/isSingleBasicAPIGEE` using only authentication applicability proven by its own official contract.
5. The operation-level API-key applicability is not inherited from Marriage Cases. If the authorized UAT gateway requires additional applicability not present in the preserved official evidence, stop and record `DEFERRED_EXTERNAL_CONTRACT_GAP`; do not guess.
6. Verify safe documented success/error rendering, alias/status/duration and no secret/personal evidence leakage.

### Marriage Couple Last Case

1. Configure the exact service-scoped UAT token credentials through the vault-backed AuthProfile.
2. Obtain the token through the documented `/genToken` contract.
3. Open MOJ -> Marriage Couple Last Case -> UAT and enter owner-approved values in `male_civilId` and `female_civilId` without recording them.
4. Execute `/marriageCoupleLastAPIGEE` using only auth applicability proven for API130.
5. Do not inherit API129 operation-level API-key applicability. An unresolved gateway requirement remains external evidence, not a reason to invent configuration.
6. Verify documented response handling, masking and secret-safe request metadata.

### Family Judgment Text

1. Configure the exact API196 UAT AuthProfile with owner-held token credentials through write-only secret inputs.
2. Obtain a token from documented `/token` using JSON `username`/`password`; consume only the documented `token` response member.
3. Open MOJ -> Family Judgment Text -> UAT and enter approved values in generated `caseNo` and `type` fields.
4. Execute `/familyJudgmentText` with its documented Bearer requirement and only any additional auth applicability separately proven for API196.
5. CAIT identifies the supplied UAT try-out target as a mock; therefore structural UAT behavior may be verified, but real backend-data validation remains `DEFERRED_EXTERNAL` unless an authorized non-mock target is supplied.
6. Verify safe outcome/masking and endpoint alias/status/duration.

### Procuration Status

1. Configure the exact API134 UAT AuthProfile with owner-held credentials through write-only secret inputs.
2. Obtain a token from `/Authenticate/Token` using JSON fields `UserName`, `Password`, `Geha`; consume the documented `token` response member. The preserved contract documents a six-hour token lifetime; cache safety rules still apply.
3. Open MOJ -> Procuration Status -> UAT and enter approved values in `CivilClient`, `CivilAgent`, `year` and `Number` without recording them.
4. Execute `/Procuration/ProcurationStatus` using its own documented auth applicability only.
5. CAIT identifies the supplied UAT try-out target as a mock; real backend-data validation remains `DEFERRED_EXTERNAL` unless an authorized non-mock target is supplied.
6. Verify documented response handling, masking and secret-safe alias/status/duration.

## Current classification

`OWNER_LIVE_UAT=DEFERRED_EXTERNAL_NOT_PASS`

Reason: the repository/CI does not contain and must not contain owner-held UAT credentials or approved personal test identifiers. Autonomous contract, runtime, isolation, security and browser readiness must still pass before P09 closure; the live smoke does not become PASS until it is actually performed with authorized external inputs.
