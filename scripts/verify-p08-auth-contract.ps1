[CmdletBinding()]
param(
    [switch]$ClosureGate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Read-RepoText {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $path = Join-Path $repositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $errors.Add("Missing required file: $RelativePath")
        return ''
    }

    return [System.IO.File]::ReadAllText($path)
}

function Require-Text {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Needle,
        [Parameter(Mandatory = $true)][string]$Context
    )

    if (-not $Text.Contains($Needle, [System.StringComparison]::Ordinal)) {
        $errors.Add("$Context is missing required contract text: $Needle")
    }
}

$index = Read-RepoText 'docs/moj-api-reference/INDEX.md'
$matrix = Read-RepoText 'docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md'
$evidence = Read-RepoText 'docs/evidence/P08_CONTRACT_RECONCILIATION.md'
$currentPhase = Read-RepoText 'CURRENT_PHASE.md'
$ledger = Read-RepoText 'docs/TASK_LEDGER.md'

# Preserved owner-supplied official minimum contract. These are deliberately exact
# enough to detect accidental drift without inventing unavailable portal details.
foreach ($required in @(
    'POST /genToken',
    'application/x-www-form-urlencoded',
    'username',
    'password',
    '"data": "string"',
    'x-api-key',
    'Bearer'
)) {
    Require-Text -Text $index -Needle $required -Context 'docs/moj-api-reference/INDEX.md'
}

foreach ($contractId in @(
    'AUTH-APIKEY-01',
    'AUTH-TOKEN-01',
    'AUTH-TOKEN-02',
    'AUTH-TOKEN-03',
    'AUTH-TOKEN-04',
    'AUTH-BEARER-01',
    'AUTH-EXPIRY-01',
    'AUTH-SCOPE-01',
    'AUTH-ISOLATION-01',
    'AUTH-ISOLATION-02',
    'AUTH-REDACT-01',
    'AUTH-P07-01',
    'AUTH-METADATA-01',
    'AUTH-FIXTURE-01',
    'AUTH-ADMIN-01'
)) {
    Require-Text -Text $matrix -Needle $contractId -Context 'P08 contract matrix'
}

Require-Text -Text $matrix -Needle 'DEFERRED_EXTERNAL' -Context 'P08 contract matrix'
Require-Text -Text $evidence -Needle 'DEFERRED_EXTERNAL' -Context 'P08 contract evidence'
Require-Text -Text $currentPhase -Needle 'P08 — MOJ authentication integration' -Context 'CURRENT_PHASE.md'
Require-Text -Text $ledger -Needle '| P08 | OPEN / READY |' -Context 'docs/TASK_LEDGER.md'
Require-Text -Text $ledger -Needle '| P09 | LOCKED |' -Context 'docs/TASK_LEDGER.md'

# This independent evidence line is not allowed to close the phase.
if ($matrix -match '(?im)^Status:\s*\*\*CLOSED\*\*' -or
    $evidence -match '(?im)^Status:\s*\*\*CLOSED\*\*') {
    $errors.Add('Independent P08 contract evidence must not mark P08 CLOSED.')
}

# When P08 production authentication source exists, detect common contract inventions
# that conflict with the metadata-driven / official-evidence-only rules. Support for
# generic scope dimensions in the pre-existing TokenCacheIdentity is intentionally not
# flagged; only explicit undocumented token-request conventions are rejected here.
$productionRoots = @(
    'src/GSIP.Application/Authentication',
    'src/GSIP.Infrastructure/Authentication',
    'src/GSIP.Infrastructure/Execution'
)

$productionFiles = @()
foreach ($relativeRoot in $productionRoots) {
    $root = Join-Path $repositoryRoot $relativeRoot
    if (Test-Path -LiteralPath $root -PathType Container) {
        $productionFiles += Get-ChildItem -LiteralPath $root -Filter '*.cs' -File -Recurse
    }
}

$p08RuntimeFiles = @()
foreach ($file in $productionFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?i)genToken|x-api-key|MojAuthentication|MojAuth') {
        $p08RuntimeFiles += $file
    }
}

if ($p08RuntimeFiles.Count -eq 0) {
    $warnings.Add('No landed P08 MOJ authentication runtime source detected; open-phase audit remains pending.')
}
else {
    foreach ($file in $p08RuntimeFiles) {
        $relative = [System.IO.Path]::GetRelativePath($repositoryRoot, $file.FullName).Replace('\\', '/')
        $text = [System.IO.File]::ReadAllText($file.FullName)

        if ($text -match '(?i)https?://') {
            $errors.Add("Hard-coded external URL detected in P08 production source: $relative. Base URLs must come from environment/metadata.")
        }

        if ($text -match '["'']/?genToken["'']') {
            $errors.Add("Hard-coded /genToken path detected in P08 production source: $relative. Token path must come from the exact auth/environment metadata.")
        }

        foreach ($undocumented in @('grant_type', 'client_id', 'client_secret', 'refresh_token')) {
            if ($text -match [regex]::Escape($undocumented)) {
                $errors.Add("Undocumented authentication field '$undocumented' detected in P08 production source: $relative.")
            }
        }
    }
}

# The ordinary audit gate can be green while P08 is legally OPEN. Captain convergence
# should use -ClosureGate after composing Workers 1-3; that mode rejects pending evidence.
if ($ClosureGate) {
    $closureRequiredPaths = @(
        'tests/GSIP.P08MojAuthRuntimeChecks',
        'tests/GSIP.P08AuthIsolationChecks',
        'tests/GSIP.P08SecurityAcceptanceChecks',
        '.github/workflows/p08-moj-auth-runtime.yml',
        '.github/workflows/p08-auth-isolation.yml',
        '.github/workflows/p08-security-acceptance.yml'
    )

    foreach ($relativePath in $closureRequiredPaths) {
        $path = Join-Path $repositoryRoot $relativePath
        if (-not (Test-Path -LiteralPath $path)) {
            $errors.Add("Closure gate missing composed P08 evidence path: $relativePath")
        }
    }

    foreach ($pendingMarker in @(
        'PENDING_IMPLEMENTATION',
        'PENDING_ACCEPTANCE',
        'PENDING_P08_RUNTIME_EVIDENCE',
        'HANDOFF_REQUIRED',
        'CONTRACT_GAP'
    )) {
        if ($matrix.Contains($pendingMarker, [System.StringComparison]::Ordinal)) {
            $errors.Add("Closure gate cannot pass while contract matrix contains: $pendingMarker")
        }
    }
}

foreach ($warning in $warnings) {
    Write-Warning $warning
}

if ($errors.Count -gt 0) {
    Write-Host "P08 authentication contract validation FAILED ($($errors.Count) error(s))."
    foreach ($failure in $errors) {
        Write-Host " - $failure"
    }
    exit 1
}

$mode = if ($ClosureGate) { 'closure' } else { 'open-phase audit' }
Write-Host "P08 authentication contract validation PASSED in $mode mode."
Write-Host "Official minimum: x-api-key; POST /genToken; form-urlencoded username/password; data token response; Bearer use."
Write-Host "Undocumented TTL/scope/OAuth conventions remain prohibited unless new official evidence is captured."
