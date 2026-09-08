# P00 Baseline Evidence

Status: **CLOSED**

## Integrated closure state

- Repository: `walidatiyaai2025-gif/KAIT`
- Integration PR: #2 — `P00: establish production build/runtime baseline`
- Exact integrated implementation SHA: `8156c46ce8ce366424d955a6194d677c2da5055f`
- Pinned SDK: `.NET SDK 10.0.400`
- Target framework: `net10.0`
- Initial version: `0.1.0`
- Owner-only/external P00 dependency: **none**

## Implemented controls

- .NET 10 LTS target with SDK `10.0.400` pinned in `global.json`; previews disabled.
- `net10.0`, C# 14, nullable enabled, deterministic build and warnings-as-errors baseline.
- Initial semantic version `0.1.0` and auditable artifact naming contract.
- Buildable ASP.NET Core web baseline with liveness health endpoint only; no future-phase business feature was claimed.
- Executable repository contract checks for pinned SDK/version/solution/security baseline.
- Windows-hosted CI for SDK verification, restore, Release build, P00 checks, publish, ZIP packaging, SHA-256 generation and artifact upload.
- Mandatory per-service/per-environment Go-Live configuration isolation contract recorded for later implementation phases.
- Stale plan references to nonexistent PNG UI baselines were reconciled to the canonical SVG files.

## Exact-main CI evidence

On exact `main` SHA `8156c46ce8ce366424d955a6194d677c2da5055f`:

- Planning Integrity run `34210899288`: **SUCCESS**.
- P00 Build Baseline run `34210899267`: **SUCCESS**.
- SDK selection: **10.0.400 verified**.
- Restore: **PASS**.
- Release build: **PASS**.
- P00 executable contract checks: **PASS**.
- Publish: **PASS**.
- Package/hash step: **PASS**.
- Artifact upload: **PASS**.

## Exact-main artifact evidence

- Artifact: `GSIP-P00-Baseline-0.1.0-8156c46ce8ce.zip`
- Artifact record size: `90818` bytes
- GitHub artifact digest: `sha256:455be98b93727c20646193da3682f92a20bbc9d373881ea6b3b4f443d967a731`
- Workflow run: `34210899267`
- Workflow head SHA: `8156c46ce8ce366424d955a6194d677c2da5055f`

The artifact is P00 CI evidence, not a production release installer. Production Windows/IIS installer acceptance belongs to later phases.

## Closure conclusion

P00 satisfies its live-state discovery, platform/toolchain decision, repository structure, versioning, build/test/CI baseline, security baseline, service-isolation contract and exact-main verification requirements. No owner-only item is being treated as PASS without evidence. P01 may become current only after this closure reconciliation is merged and the resulting exact-main CI remains green.
