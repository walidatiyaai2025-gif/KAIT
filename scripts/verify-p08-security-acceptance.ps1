param(
    [string]$Configuration = 'Release',
    [string]$EvidenceDirectory = 'artifacts/p08-security-acceptance'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$env:GSIP_P08_EVIDENCE_DIR = $EvidenceDirectory

function Invoke-CheckedDotnetRun {
    param([Parameter(Mandatory=$true)][string]$Project, [Parameter(Mandatory=$true)][string]$OutputName)
    $outputPath = Join-Path $EvidenceDirectory $OutputName
    & dotnet run --project $Project -c $Configuration --no-build 2>&1 | Tee-Object -FilePath $outputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Acceptance executable failed: $Project"
    }
}

Invoke-CheckedDotnetRun 'tests/GSIP.P08SecurityAcceptanceChecks/GSIP.P08SecurityAcceptanceChecks.csproj' 'p08-independent-output.txt'
Invoke-CheckedDotnetRun 'tests/GSIP.P08SecurityAcceptanceChecks/MojRuntime/GSIP.P08MojRuntimeAcceptance.csproj' 'p08-moj-runtime-independent-output.txt'

# Preserve already-closed lower-phase security boundaries on the same exact candidate.
Invoke-CheckedDotnetRun 'tests/GSIP.P07ExecutionSecurityChecks/GSIP.P07ExecutionSecurityChecks.csproj' 'p07-execution-security-output.txt'
Invoke-CheckedDotnetRun 'tests/GSIP.P06TokenCacheChecks/GSIP.P06TokenCacheChecks.csproj' 'p06-token-cache-output.txt'

# Worker 2 owns this project. Consume it when integrated; never duplicate its implementation.
$p08IsolationProject = 'tests/GSIP.P08AuthIsolationChecks/GSIP.P08AuthIsolationChecks.csproj'
if (Test-Path $p08IsolationProject) {
    Invoke-CheckedDotnetRun $p08IsolationProject 'p08-auth-isolation-output.txt'
}

$forbiddenPatterns = @(
    'P08-(?:api|bearer|token|scope|fresh)-[A-F0-9]{32,}',
    'p08-(?:user|pass)-[A-F0-9]{24,}',
    'SYNTHETIC_RUNTIME_SECRET_7f9c_DO_NOT_LOG',
    'P06-TOKEN-CACHE-PLAINTEXT-SENTINEL',
    'P06-NEAR-EXPIRY-SECRET-SENTINEL',
    'Authorization\s*:\s*Bearer\s+\S+',
    'x-api-key\s*[:=]\s*\S+'
)

$leaks = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path $EvidenceDirectory -File -Recurse | ForEach-Object {
    $text = Get-Content -Raw -LiteralPath $_.FullName
    foreach ($pattern in $forbiddenPatterns) {
        if ($text -match $pattern) {
            $leaks.Add("$($_.Name):$pattern")
        }
    }
}

if ($leaks.Count -gt 0) {
    throw "P08 evidence leakage scan failed. Matches=$($leaks.Count). Plaintext values are intentionally not echoed."
}

$summaryPath = Join-Path $EvidenceDirectory 'acceptance-summary.json'
if (-not (Test-Path $summaryPath)) {
    throw 'P08 independent acceptance did not produce its sanitized summary.'
}

$summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
$composed = [ordered]@{
    CandidateSha = $env:CANDIDATE_SHA
    Unit = 'P08::security-acceptance-ci'
    IndependentBoundaryCases = $summary.Cases.Count
    IndependentBoundaryAcceptance = 'PASS'
    IndependentMojRuntimeAcceptance = 'PASS'
    P07ExecutionSecurityRegression = 'PASS'
    P06TokenCacheRegression = 'PASS'
    P08AuthIsolationIntegrated = (Test-Path $p08IsolationProject)
    LeakageScan = 'PASS'
    SyntheticOnly = $true
    RealCredentials = $false
}
$composed | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'ci-evidence.json') -Encoding utf8

Write-Host "P08_SECURITY_ACCEPTANCE_COMPOSED=PASS;BOUNDARY_CASES=$($summary.Cases.Count);MOJ_RUNTIME=PASS;LEAKAGE_SCAN=PASS"
