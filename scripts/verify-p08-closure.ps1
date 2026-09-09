param(
    [switch]$ClosureGate
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Require-File([string]$Path) {
    if (-not (Test-Path $Path)) { throw "P08 closure gate missing required file: $Path" }
}

function Require-Text([string]$Path, [string]$Pattern, [string]$Message) {
    Require-File $Path
    $text = Get-Content $Path -Raw
    if ($text -notmatch $Pattern) { throw $Message }
}

$required = @(
    'docs/moj-api-reference/INDEX.md',
    'docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md',
    'docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md',
    'docs/evidence/P08_CONTRACT_RECONCILIATION.md',
    'docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md',
    'src/GSIP.Infrastructure/Execution/GenericServiceExecutionEngine.cs',
    'src/GSIP.Web/Controllers/AuthProfileAuthenticationController.cs',
    'tests/GSIP.P08AuthenticationChecks/GSIP.P08AuthenticationChecks.csproj',
    'tests/GSIP.P08MojAuthRuntimeChecks/GSIP.P08MojAuthRuntimeChecks.csproj',
    'tests/GSIP.P08AuthIsolationChecks/GSIP.P08AuthIsolationChecks.csproj',
    'tests/GSIP.P08SecurityAcceptanceChecks/GSIP.P08SecurityAcceptanceChecks.csproj',
    'scripts/verify-p08-security-acceptance.ps1'
)
$required | ForEach-Object { Require-File $_ }

Require-Text 'execution/GSIP_Full_Execution.json' 'P08 - MOJ authentication token flow' 'Canonical P08 execution-plan entry is missing.'
Require-Text 'src/GSIP.Infrastructure/Execution/GenericServiceExecutionEngine.cs' 'X-GSIP-TokenEndpointPath' 'Metadata-driven token endpoint control is missing.'
Require-Text 'src/GSIP.Web/Controllers/AuthProfileAuthenticationController.cs' 'ServiceSecretsManage' 'Administration authentication probe is not server-authorized.'
Require-Text 'src/GSIP.Web/Controllers/AuthProfileAuthenticationController.cs' 'ValidateAntiForgeryToken' 'Administration authentication probe is missing anti-forgery protection.'
Require-Text 'src/GSIP.Web/Controllers/AuthProfileAuthenticationController.cs' 'TestTokenGeneration' 'Administration token-generation Test action is missing.'
Require-Text 'docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md' 'UAT_PROVEN' 'Post-closure UAT composite-auth evidence is not marked proven.'
Require-Text 'docs/moj-api-reference/P08_AUTH_CONTRACT_MATRIX.md' 'PRODUCTION_DEFERRED_EXTERNAL' 'Unproven Production contract detail is not explicitly deferred.'
Require-Text 'docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md' 'UAT_PROVEN' 'Post-closure repair evidence does not classify observed UAT evidence.'
Require-Text 'docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md' 'PRODUCTION_DEFERRED_EXTERNAL' 'Post-closure repair evidence does not preserve the Production evidence boundary.'
Require-Text 'docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md' '67968a9230dae453b36f48549eda138d7b5401c7' 'Historical P08 exact integrated implementation baseline is not recorded.'
Require-Text 'docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md' '17/17' 'Historical P08 exact-main workflow evidence is not recorded.'

$evidence = (Get-Content 'docs/evidence/P08_MOJ_AUTHENTICATION_INTEGRATION.md' -Raw) + "`n" +
            (Get-Content 'docs/evidence/P08_CONTRACT_RECONCILIATION.md' -Raw) + "`n" +
            (Get-Content 'docs/evidence/P08_POST_CLOSURE_COMPOSITE_AUTH_REPAIR.md' -Raw)
if ($evidence -match '(?i)(authorization\s*:\s*bearer\s+[A-Za-z0-9._~+/-]{20,}|x-api-key\s*[:=]\s*[A-Za-z0-9._~+/-]{20,}|password\s*[:=]\s*[^`\s|]{12,})') {
    throw 'P08 closure evidence appears to contain plaintext credential/token material.'
}

if ($ClosureGate) {
    Require-Text 'docs/TASK_LEDGER.md' '\| P08 \| CLOSED \|' 'Closure mode requires the ledger to mark P08 CLOSED.'
    Require-Text 'docs/TASK_LEDGER.md' '\| P09 \| OPEN / READY \|' 'Closure mode requires the ledger to open P09.'
    Require-Text 'CURRENT_PHASE.md' 'P09 - Seed and implement the five MOJ services|P09 — Seed and implement the five MOJ services' 'Closure mode requires P09 to be the canonical next phase.'
}

Write-Host 'P08_CLOSURE_CONTRACT_GATE=PASS'
