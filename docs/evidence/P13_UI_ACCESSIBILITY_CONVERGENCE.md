# P13 UI / Accessibility Convergence Evidence

**State: P13 CLOSED — exact integrated evidence**

This document records P13 implementation closure from exact GitHub integration and CI evidence. It is not owner acceptance, Production acceptance, P14 implementation authorization, or `VERIFIED_FINAL_COMPLETE`.

## Authority

- Canonical base before P13 implementation: `42136cf59202c90f20f204e520c644acc69a88ca`.
- Final P13 implementation head: `1576764a16ea6ddfed735cb0824cb38de26c83d8`.
- Normal integration: PR #71.
- Exact integrated P13 main: `203cc28db714fae5c2c70e85adc9cc2306bb2107`.
- Exact-head PR gate: **35/35 SUCCESS** after one same-SHA transient LocalDB job retry; no source/test/acceptance change was made for that retry.
- Exact-new-main push gate: **32/32 SUCCESS**, failure=0, queued=0, in-progress=0.
- Exact-main P13 workflow: `34421612012` — SUCCESS.
- Canonical UI parity contract: `docs/UI_DESIGN_PARITY_GATE.md`.
- Repository-native reference surfaces:
  - `docs/ui-baseline/bilingual_kuwait_government_services_dashboard.svg`
  - `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg`
  - `docs/ui-baseline/bilingual_kuwait_government_permissions_dashboard.svg`
  - `docs/ui-baseline/kuwait_government_audit_dashboard.svg`

## Integrated scope

P13 converged the stale P01 Home preview into a real catalogue-backed bilingual government-services dashboard and preserved the established navy/blue/gold shell. Manual semantic review recovered a material candidate drift before integration: a generic services table was replaced by the approved entity-summary-card and service-card hierarchy, and executable static assertions now prevent that drift from returning.

Home reads only active non-secret metadata catalogue facts. It intentionally does not aggregate Request or Audit totals because those surfaces have narrower authorization boundaries; this least-privilege deviation is documented in `docs/DESIGN_DECISIONS.md` and does not substitute synthetic production-looking data.

P13 also restores all primary routes on narrow/mobile navigation, adds shared keyboard focus and skip-navigation treatment, preserves Arabic RTL / English LTR behavior and LTR technical identifiers, and keeps all four approved feature-screen hierarchies independently testable.

Service Execution no longer leaves its History tab in the stale P07 deferred state. The integrated P13 baseline links History to the protected P10 `/requests` surface filtered by the selected service; P10 authorization remains authoritative and the navigation link grants no additional permission.

Closed-P12/P13 verification was made phase-monotonic so legal phase progression does not create false regressions merely because a prior phase is no longer the current UI marker. Security and functional assertions themselves remain enforced.

## Exact-candidate visual/runtime evidence

All required visual lines executed against final candidate `1576764a16ea6ddfed735cb0824cb38de26c83d8`:

- Home / Dashboard — P13 workflow `34420766116`:
  - `P13-Home-UI-1576764a16ea6ddfed735cb0824cb38de26c83d8`
  - artifact `10130841130`
  - digest `sha256:020b95580cdd3f700e7859d13fc0a343cbfe4f3ffd3713259e02c68f34f341be`
  - `P13-UI-Accessibility-Static-1576764a16ea6ddfed735cb0824cb38de26c83d8`
  - artifact `10130822518`
  - digest `sha256:0837d8dcc6710b1add8618c9afa080510acbe6bd677408d451e372bb8c341db5`
- Permissions — P04 workflow `34420766193`:
  - `P04-Permissions-UI-Evidence-1576764a16ea6ddfed735cb0824cb38de26c83d8`
  - artifact `10130851071`
  - digest `sha256:f8ba518a5d0f0cb1233ecd339e04c6fe2ae470573211c8dba2cd174d30b01a8d`
- Service Execution — P07 workflow `34420766234`:
  - `P07-Execution-UI-Evidence-1576764a16ea6ddfed735cb0824cb38de26c83d8`
  - artifact `10130894801`
  - digest `sha256:962aca8bd4cfe2b99a51580023e1d771659151b16b27ad967cb668ae198e0aaf`
- Audit / Monitoring — P11 workflow `34420766149`:
  - `P11-Audit-LocalDB-Runtime-1576764a16ea6ddfed735cb0824cb38de26c83d8`
  - artifact `10130889052`
  - digest `sha256:b150bc4808e836da3ffae8e3d46a375688afebbd34bb00cf0c6c939b8216fe69`
  - `P11-Audit-Security-1576764a16ea6ddfed735cb0824cb38de26c83d8`
  - artifact `10130799339`
  - digest `sha256:1e8c90cfae3b627d8b129f0d210e0a4e00a10235d041f3551cee7b0afce063b0`

## Exact-main P13 evidence

Exact integrated main `203cc28db714fae5c2c70e85adc9cc2306bb2107` completed **32/32 push workflows SUCCESS**. The exact-main P13 run `34421612012` produced:

- `P13-Home-UI-203cc28db714fae5c2c70e85adc9cc2306bb2107`
  - artifact `10131131126`
  - digest `sha256:19b8a382a59cc049374578d1fc8f865439bc16a523268f74ef98175aeb68fe2e`
- `P13-UI-Accessibility-Static-203cc28db714fae5c2c70e85adc9cc2306bb2107`
  - artifact `10131148598`
  - digest `sha256:062421c5545952d26d42b1a91b8865ffbd4626ef7f3f9d8041504362a3d4e329`

No exact-main regression remained after integration.

## Same-SHA transient CI history

On final candidate `1576764a16ea6ddfed735cb0824cb38de26c83d8`, P08 Authentication Convergence run `34420766224` initially failed while creating its LocalDB test database with SQL execution timeout error `-2`, before the affected isolation assertions ran. The independent P08 Auth Scope Isolation line was already green on the same candidate. The failed job was rerun against the **same exact SHA** without any source, test, workflow, or acceptance change; the rerun completed SUCCESS across LocalDB, P08 suites, independent security acceptance, governance and plaintext scanning. The historical failed attempt is not treated as PASS evidence; the successful same-SHA rerun is the terminal governed evidence.

## Security and privacy

- Home exposes only active non-secret catalogue metadata available to the authenticated shell.
- Links to Service Execution or Request History do not grant execution/history permissions; canonical server-side authorization remains authoritative.
- Runtime/browser evidence uses synthetic local data only.
- Passwords, API keys, bearer tokens, secret values, real Civil IDs and personal MOJ payloads are excluded from committed/runtime evidence.
- Historical P09 authorized live-UAT evidence remains `DEFERRED_EXTERNAL_NOT_PASS`.
- Unproven Production operation evidence remains `PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS`.
- Main branch protection remains `OWNER_LAST / NOT PASS`: live repository read-back shows `main` unprotected and repository rulesets absent. No current connector administration permission can lawfully convert this to PASS.

## Closure and transition rule

P13 implementation is CLOSED from exact integrated evidence at `203cc28db714fae5c2c70e85adc9cc2306bb2107`.

The current `worker/p13-closure-reconciliation` branch is governance/evidence-only. P14 implementation remains forbidden from this branch. P14 becomes implementation-authorized only after this closure transition is normally integrated and every governed workflow on the resulting exact new `main` SHA is terminal SUCCESS.

P15-P17 remain locked. No owner-only or external dependency is converted to PASS, no installer/release claim is made, and this is not a final-completion claim.
