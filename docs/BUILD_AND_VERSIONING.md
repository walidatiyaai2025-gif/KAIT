# GSIP Build, Runtime and Versioning Baseline

## Pinned production platform

- Runtime family: ASP.NET Core on **.NET 10 LTS**.
- Target framework: `net10.0`.
- SDK: `10.0.400`, pinned by `global.json` with previews disabled.
- Language: C# 14.
- Deployment target: Windows Server / IIS using the supported ASP.NET Core Hosting Bundle for the selected .NET 10 servicing level.
- Database target: Microsoft SQL Server 2022 or later, subject to the deployment environment validated during Setup/installation phases.

The SDK pin is intentional for deterministic CI. Servicing/runtime patches remain operational prerequisites and must stay within the supported .NET 10 LTS line. Changing the SDK major/minor, target framework or deployment platform requires an explicit repository decision and revalidation.

## Versioning

Initial executable product version: **0.1.0**.

Use semantic versioning. `Directory.Build.props` is the source for the application version until the release pipeline introduces a stricter release-version input. A release candidate is invalid if its source SHA, version, installer/package and hashes do not match.

## Artifact naming

P00 CI evidence is not a production release. Its name is:

`GSIP-P00-Baseline-{Version}-{ShortSha}.zip`

The planned production artifacts, created only in the installer/release phases, are:

- `GSIP-{Version}-win-x64.zip`
- `GSIP-{Version}-Setup-x64.exe`
- matching `.sha256` evidence files

No API/database credential, private key, secret-bearing local configuration or real personal data may be included in any artifact.

## Build contract

From repository root:

```text
dotnet restore GSIP.sln
dotnet build GSIP.sln --configuration Release --no-restore
dotnet run --project tests/GSIP.BaselineChecks/GSIP.BaselineChecks.csproj --configuration Release --no-build
```

The P00 workflow additionally publishes a framework-dependent baseline package and SHA-256 as CI evidence. It is explicitly not the final Windows/IIS installer.
