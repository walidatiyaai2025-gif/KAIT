param(
    [string]$OutputDirectory = "artifacts/p15"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

[xml]$props = Get-Content (Join-Path $repoRoot 'Directory.Build.props')
$version = [string]$props.Project.PropertyGroup.VersionPrefix
if ([string]::IsNullOrWhiteSpace($version)) { throw 'VersionPrefix is missing from Directory.Build.props.' }

$outputRoot = Join-Path $repoRoot $OutputDirectory
$publishDir = Join-Path $outputRoot 'publish'
$setupPublishDir = Join-Path $outputRoot 'setup-publish'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
Remove-Item $publishDir,$setupPublishDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDir,$setupPublishDir | Out-Null

Write-Host "P15_PACKAGE_VERSION=$version"
dotnet restore GSIP.sln
dotnet build GSIP.sln -c Release --no-restore

dotnet publish src/GSIP.Web/GSIP.Web.csproj -c Release -r win-x64 --self-contained false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'GSIP.Web publish failed.' }

# Mutable setup/Data Protection state is runtime-owned and must never be baked into a package.
$publishedAppData = Join-Path $publishDir 'App_Data'
if (Test-Path $publishedAppData) { Remove-Item $publishedAppData -Recurse -Force }

$forbiddenNames = @('completed.protected','appsettings.Production.json','appsettings.UAT.json')
$forbiddenExtensions = @('.pfx','.p12','.pem','.key','.snk')
$unsafeFiles = Get-ChildItem $publishDir -Recurse -File | Where-Object {
    $forbiddenNames -contains $_.Name -or $forbiddenExtensions -contains $_.Extension.ToLowerInvariant()
}
if ($unsafeFiles) { throw ('Secret-bearing or private-key files are forbidden in P15 package: ' + (($unsafeFiles.FullName) -join ', ')) }

$textFiles = Get-ChildItem $publishDir -Recurse -File | Where-Object { $_.Extension -in @('.json','.xml','.config','.txt','.ini','.ps1','.cmd') }
$forbiddenTextPatterns = @('-----BEGIN PRIVATE KEY-----','-----BEGIN RSA PRIVATE KEY-----','-----BEGIN EC PRIVATE KEY-----')
foreach ($file in $textFiles) {
    $text = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    foreach ($pattern in $forbiddenTextPatterns) {
        if ($text -and $text.Contains($pattern, [System.StringComparison]::Ordinal)) { throw "Private key material found in package text file: $($file.FullName)" }
    }
}

$zipName = "GSIP-$version-win-x64.zip"
$zipPath = Join-Path $outputRoot $zipName
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

dotnet publish installer/GSIP.Setup/GSIP.Setup.csproj -c Release -r win-x64 --self-contained true -o $setupPublishDir "-p:GsipPayloadZip=$zipPath"
if ($LASTEXITCODE -ne 0) { throw 'GSIP.Setup publish failed.' }

$builtSetup = Join-Path $setupPublishDir 'GSIP.Setup.exe'
if (-not (Test-Path $builtSetup)) { throw "Installer executable not found: $builtSetup" }
$setupName = "GSIP-$version-Setup-x64.exe"
$setupPath = Join-Path $outputRoot $setupName
Copy-Item $builtSetup $setupPath -Force

function Write-Sha256([string]$Path) {
    $hash = (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $leaf = Split-Path $Path -Leaf
    "$hash  $leaf" | Set-Content -Path "$Path.sha256" -Encoding ascii -NoNewline
    return $hash
}

$zipHash = Write-Sha256 $zipPath
$setupHash = Write-Sha256 $setupPath
$sourceSha = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { (git rev-parse HEAD).Trim() }
$manifest = [ordered]@{
    Product = 'Government Services Integration Portal'
    Version = $version
    Runtime = 'win-x64'
    SourceSha = $sourceSha
    Framework = 'net10.0'
    Deployment = 'Windows Server / IIS'
    HostingModel = 'Framework-dependent ASP.NET Core application; .NET 10 Hosting Bundle prerequisite'
    MutableState = 'app/App_Data preserved across install/repair/upgrade/default uninstall'
    Package = $zipName
    PackageSha256 = $zipHash
    Installer = $setupName
    InstallerSha256 = $setupHash
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $outputRoot 'manifest.json') -Encoding utf8

Write-Host "P15_PACKAGE_BUILD=PASS"
Write-Host "P15_ZIP=$zipName SHA256=$zipHash"
Write-Host "P15_SETUP=$setupName SHA256=$setupHash"
