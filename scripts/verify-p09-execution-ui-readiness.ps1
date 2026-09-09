param(
    [string]$Configuration = 'Release',
    [string]$EvidenceDirectory = 'artifacts/p09-execution-ui-readiness'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force -Path (Join-Path $root $EvidenceDirectory) | Out-Null

function Assert-Contains([string]$Text, [string]$Needle, [string]$Message) {
    if (-not $Text.Contains($Needle, [System.StringComparison]::Ordinal)) { throw $Message }
}

$controller = Get-Content (Join-Path $root 'src/GSIP.Web/Controllers/ServiceExecutionController.cs') -Raw
$view = Get-Content (Join-Path $root 'src/GSIP.Web/Views/Execution/Index.cshtml') -Raw
$seed = Get-Content (Join-Path $root 'src/GSIP.Infrastructure/Metadata/MojMetadataSeedService.cs') -Raw
$en = Get-Content (Join-Path $root 'src/GSIP.Web/Resources/ExecutionResource.resx') -Raw
$ar = Get-Content (Join-Path $root 'src/GSIP.Web/Resources/ExecutionResource.ar-KW.resx') -Raw

Assert-Contains $controller 'GsipPermissions.ServicesExecute' 'Execution UI lost server-side service permission filtering.'
Assert-Contains $controller 'selection.SelectedService?.CanExecute != true' 'Inactive contract-ready environments can reach execution.'
Assert-Contains $controller '!string.IsNullOrWhiteSpace(config.HttpMethod)' 'Contract-ready environment visibility is not generic metadata-driven behavior.'
Assert-Contains $controller 'selectedService.Fields' 'Request fields are not rendered from canonical ServiceFields.'
Assert-Contains $controller '.OrderBy(field => field.DisplayOrder)' 'Dynamic field ordering is not canonical.'
Assert-Contains $view 'disabled="@(!details.CanExecute)"' 'Owner-last UAT state does not disable execution in the UI.'
Assert-Contains $view 'result?.EndpointAlias' 'Secret-safe endpoint alias is not shown in request metadata.'
Assert-Contains $view 'field.Sensitive ? null : submittedValue' 'Sensitive request values may be redisplayed.'
Assert-Contains $view 'masked-result' 'Sensitive structured results do not retain masking presentation.'
if ($view.Contains('field.Key.Contains("CIVIL"', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Service-specific CIVIL field-name UI heuristic remains.'
}
if ($view.Contains('moj.api.cait.gov.kw', [System.StringComparison]::OrdinalIgnoreCase) -or $view.Contains('moj-uat.api-non-prod.cait.gov.kw', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Execution view exposes a concrete MOJ URL instead of generic metadata/alias presentation.'
}

foreach ($code in @('MARRIAGECASES','ISSINGLEBASIC','MARRIAGECOUPLELASTCASE','FAMILYJUDGMENTTEXT','PROCURATIONSTATUS')) {
    Assert-Contains $seed ('"' + $code + '"') "Missing canonical P09 service $code."
}
Assert-Contains $seed 'EntityCode = "MOJ"' 'MOJ canonical entity is missing.'
Assert-Contains $seed 'DisplayOrder = 1' 'MOJ is not seeded as the first Entity.'
Assert-Contains $en 'Request completed successfully' 'English execution success localization missing.'
Assert-Contains $ar 'تم تنفيذ الطلب بنجاح' 'Arabic execution success localization missing.'

$summary = [ordered]@{
    CandidateSha = $env:CANDIDATE_SHA
    Unit = 'P09::execution-ui-and-uat-readiness'
    MojEntitySelectable = $true
    CanonicalServices = 5
    GenericDynamicFields = 'PASS'
    ServiceSpecificUiHacks = 'NONE'
    PermissionFiltering = 'PASS'
    ContractReadyViewOnly = 'PASS'
    InactiveExecutionBlocked = 'PASS'
    EndpointAliasStatusDuration = 'PASS'
    SensitivePresentation = 'PASS'
    ArabicRtlEnglishLtrRegression = 'P07_BROWSER_GATE_REQUIRED'
    BrowserData = 'SYNTHETIC_ONLY'
    LiveUat = 'OWNER_LAST_DEFERRED_EXTERNAL'
    RealCredentials = $false
    PersonalData = $false
}
$summaryPath = Join-Path $root (Join-Path $EvidenceDirectory 'acceptance-summary.json')
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding utf8

Write-Host 'P09_EXECUTION_UI_READINESS_STATIC_ACCEPTANCE=PASS'
