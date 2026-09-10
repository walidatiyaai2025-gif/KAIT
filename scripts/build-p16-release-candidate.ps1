$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root
$outputDirectory = 'artifacts/p16-release-candidate'
$outputRoot = Join-Path $root $outputDirectory
$candidateSha = (git rev-parse HEAD).Trim()
if ([string]::IsNullOrWhiteSpace($candidateSha)) { throw 'Could not resolve exact P16 release-candidate SHA.' }
if ($env:CANDIDATE_SHA -and $candidateSha -ne $env:CANDIDATE_SHA) {
    throw "P16 release candidate mismatch: expected=$env:CANDIDATE_SHA actual=$candidateSha"
}
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$expectedVersion = [string]$props.Project.PropertyGroup.VersionPrefix
if ([string]::IsNullOrWhiteSpace($expectedVersion)) { throw 'VersionPrefix is missing from Directory.Build.props.' }

python scripts/verify_p15_installer.py
if ($LASTEXITCODE -ne 0) { throw 'P15 installer source contract failed on the P16 candidate.' }
python scripts/verify_p16_acceptance_contract.py
if ($LASTEXITCODE -ne 0) { throw 'P16 acceptance source contract failed.' }

# P15 packaging now derives SourceSha from the checked-out HEAD and treats
# CANDIDATE_SHA as an assertion, so P16 consumes the canonical provenance path
# directly instead of rewriting GitHub environment identity.
./scripts/build-p15-package.ps1 -OutputDirectory $outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'P16 release-candidate package build failed.' }

$manifestPath = Join-Path $outputRoot 'manifest.json'
if (-not (Test-Path $manifestPath)) { throw 'P16 release-candidate package manifest is missing.' }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ([string]$manifest.SourceSha -ne $candidateSha) {
    throw "P16 release manifest source mismatch: expected=$candidateSha actual=$($manifest.SourceSha)"
}
if ([string]$manifest.Version -ne $expectedVersion) { throw "Unexpected P16 candidate version: expected=$expectedVersion actual=$($manifest.Version)" }

$packagePath = Join-Path $outputRoot ([string]$manifest.Package)
$installerPath = Join-Path $outputRoot ([string]$manifest.Installer)
foreach ($entry in @(
    @{ Path = $packagePath; Expected = [string]$manifest.PackageSha256; Kind = 'package' },
    @{ Path = $installerPath; Expected = [string]$manifest.InstallerSha256; Kind = 'installer' }
)) {
    if (-not (Test-Path $entry.Path)) { throw "Missing P16 $($entry.Kind) artifact: $($entry.Path)" }
    $actual = (Get-FileHash $entry.Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.Expected.ToLowerInvariant()) { throw "P16 $($entry.Kind) SHA-256 mismatch." }
}

$releaseManifest = [ordered]@{
    CandidateSha = $candidateSha
    Unit = 'P16::full-acceptance-release-candidate'
    Product = [string]$manifest.Product
    Version = [string]$manifest.Version
    Framework = [string]$manifest.Framework
    Runtime = [string]$manifest.Runtime
    Package = [string]$manifest.Package
    PackageBytes = (Get-Item $packagePath).Length
    PackageSha256 = [string]$manifest.PackageSha256
    Installer = [string]$manifest.Installer
    InstallerBytes = (Get-Item $installerPath).Length
    InstallerSha256 = [string]$manifest.InstallerSha256
    SourceIdentity = 'EXACT_CHECKED_OUT_CANDIDATE'
    ArtifactIdentity = 'SAME_CANDIDATE'
    AcceptanceState = 'CANDIDATE_NOT_FINAL_P17'
    ExternalUat = 'DEFERRED_EXTERNAL_NOT_PASS_WHERE_UNAVAILABLE'
    ProductionEvidence = 'PRODUCTION_DEFERRED_EXTERNAL_NOT_PASS_WHERE_UNAVAILABLE'
}
$releaseManifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $outputRoot 'p16-release-manifest.json') -Encoding utf8

Write-Host "P16_RELEASE_CANDIDATE_BUILD=PASS"
Write-Host "P16_RELEASE_CANDIDATE_SHA=$candidateSha"
Write-Host "P16_RELEASE_CANDIDATE_VERSION=$expectedVersion"
Write-Host "P16_PACKAGE=$($manifest.Package) SHA256=$($manifest.PackageSha256)"
Write-Host "P16_INSTALLER=$($manifest.Installer) SHA256=$($manifest.InstallerSha256)"
