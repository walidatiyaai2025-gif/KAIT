# CURRENT_PHASE.md

## Canonical current phase

**P08 — MOJ authentication integration**

Status: **OPEN / READY**

P07 — Generic service execution engine is **CLOSED** from exact integrated implementation baseline `9535fa158441160ab7c7d204863776e38e560a33` after normal integration of the P07 execution authorization/binding, upgrade-persistence acceptance, Service Execution UI parity, generic runtime and final UI/runtime convergence lines through PRs #33, #34, #36, #37 and #38. PR #39 supplied intermediate progress reconciliation before final integration.

On exact implementation `main` SHA `9535fa158441160ab7c7d204863776e38e560a33`, all **16/16 exact-main workflow runs succeeded**, with no failed, queued or in-progress run after completion.

P07 exact-main closure evidence includes:

- P07 Generic Execution Runtime run `34305403834` — **SUCCESS**; artifact `P07-Generic-Execution-Runtime-9535fa158441160ab7c7d204863776e38e560a33`, 809 bytes, digest `sha256:322395583f95a1f8fd1cacab0975ca0f4b3c0c971de1a86fa44185ed3f57a8dc`.
- P07 Service Execution UI run `34305403861` — **SUCCESS**; artifact `P07-Execution-UI-Evidence-9535fa158441160ab7c7d204863776e38e560a33`, 372,853 bytes, digest `sha256:5d9b9fd470d6f953fb120391c42130a01412a9b4e8a6288399170cf8399b8f78`.
- P07 Upgrade Persistence Acceptance run `34305403851` — **SUCCESS**; artifact `p07-upgrade-persistence-evidence-9535fa158441160ab7c7d204863776e38e560a33`, 647 bytes, digest `sha256:5ec9fd3f01925d62cf47d375be1bf591b27dd2eebf9c3848ea448e01c00482ef`.

P07 acceptance verifies exact authorized `Service + Environment + AuthProfile` resolution without fallback, metadata-driven field generation and validation, RequestId/CorrelationId propagation, `IHttpClientFactory` outbound execution with bounded resilience and POST retry only when explicitly `SafeToRetry`, structured ResultMappings, bounded response handling, secret-safe request/response diagnostics, response/error handling across required HTTP/network/TLS classes, sensitive raw-response masking, bilingual Arabic RTL / English LTR responsive Service Execution UI, accessibility/visual evidence, fake-endpoint acceptance, upgrade/data preservation and preservation of closed P00–P06 security/isolation contracts. No owner-only or external P07 evidence is deferred.

The P07 final closure reconciliation is governance/evidence only and introduces **no P08 implementation**.

## Legal work now

P08 only, plus any repair needed to preserve closed P00–P07 baselines and repository controls.

P08 scope is MOJ authentication integration based only on official documentation/fixtures: x-api-key, `/genToken` and Bearer-token behavior where applicable. Do not assume credentials or authentication bindings are shared between MOJ services. Continue exact Service + Environment + AuthProfile isolation and fail closed on unknown/forged/cross-scope configuration.

## Locked future work

P09–P17 remain locked. P09 service-specific MOJ request/response implementation must not begin until P08 is formally CLOSED.

## P07 closure evidence

Detailed closure evidence is recorded in `docs/evidence/P07_GENERIC_EXECUTION_ENGINE.md`.

- Implementation baseline: `9535fa158441160ab7c7d204863776e38e560a33`
- Exact-main workflows: **16/16 SUCCESS**
- Open P07 PRs at closure reconciliation start: **NONE**
- Owner/external dependency deferred for P07: **NONE**
- P08 implementation introduced by P07 closure reconciliation: **NONE**

## P08 exit condition

P08 may be marked CLOSED only when the authoritative MOJ authentication contract is implemented from official evidence, x-api-key/token/Bearer handling is isolated to the exact Service + Environment + AuthProfile binding, credentials/tokens cannot cross service or environment boundaries, negative/security acceptance is executable, P00–P07 regressions remain green, and exact-main evidence supports closure without introducing P09+ scope.
