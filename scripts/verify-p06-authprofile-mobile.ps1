$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $root 'artifacts/p06-authprofile-admin'
$browserCandidates = @(
    (Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft/Edge/Application/msedge.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft/Edge/Application/msedge.exe')
) | Where-Object { $_ -and (Test-Path $_) }
$browser = $browserCandidates | Select-Object -First 1
if (-not $browser) { throw 'No supported Chrome/Edge browser found for P06 responsive evidence.' }

$cases = @(
    @{ Html='auth-profiles-en.html'; Screenshot='p06-auth-profiles-en-mobile.png'; Culture='en'; Direction='ltr' },
    @{ Html='auth-profiles-ar.html'; Screenshot='p06-auth-profiles-ar-mobile.png'; Culture='ar-KW'; Direction='rtl' }
)

foreach ($case in $cases) {
    $htmlPath = Join-Path $artifactDir $case.Html
    if (-not (Test-Path $htmlPath)) { throw "Missing authenticated evidence HTML: $($case.Html)" }

    $instrumentedPath = Join-Path $artifactDir ("metric-" + $case.Html)
    $html = Get-Content $htmlPath -Raw
    $metricScript = @'
<script data-p06-responsive-metric>
(function () {
  document.documentElement.setAttribute('data-evidence-inner-width', String(window.innerWidth));
  document.documentElement.setAttribute('data-evidence-scroll-width', String(document.documentElement.scrollWidth));
  document.documentElement.setAttribute('data-evidence-client-width', String(document.documentElement.clientWidth));
})();
</script>
'@
    $html.Replace('</body>', "$metricScript`n</body>") | Set-Content $instrumentedPath -Encoding utf8

    $fileUrl = 'file:///' + $instrumentedPath.Replace('\','/')
    $common = @(
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        '--hide-scrollbars',
        '--allow-file-access-from-files',
        '--force-device-scale-factor=1',
        '--window-size=390,844'
    )

    $dump = & $browser @common '--dump-dom' $fileUrl 2>$null | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Browser DOM metric capture failed for $($case.Culture)." }
    $innerMatch = [regex]::Match($dump, 'data-evidence-inner-width="(\d+)"')
    $scrollMatch = [regex]::Match($dump, 'data-evidence-scroll-width="(\d+)"')
    $clientMatch = [regex]::Match($dump, 'data-evidence-client-width="(\d+)"')
    if (-not $innerMatch.Success -or -not $scrollMatch.Success -or -not $clientMatch.Success) {
        throw "Responsive DOM metrics were not emitted for $($case.Culture)."
    }

    $innerWidth = [int]$innerMatch.Groups[1].Value
    $scrollWidth = [int]$scrollMatch.Groups[1].Value
    $clientWidth = [int]$clientMatch.Groups[1].Value
    if ($innerWidth -ne 390 -or $clientWidth -ne 390) {
        throw "Narrow evidence viewport is not exact 390 CSS px for $($case.Culture): inner=$innerWidth client=$clientWidth."
    }
    if ($scrollWidth -gt $clientWidth) {
        throw "Horizontal overflow detected for $($case.Culture) at 390px: scrollWidth=$scrollWidth clientWidth=$clientWidth."
    }

    $output = Join-Path $artifactDir $case.Screenshot
    $arguments = @($common + @("--screenshot=$output", $fileUrl))
    $browserProcess = Start-Process $browser -ArgumentList $arguments -Wait -PassThru
    if ($browserProcess.ExitCode -ne 0 -or -not (Test-Path $output)) { throw "390px screenshot failed for $($case.Culture)." }
    if ((Get-Item $output).Length -lt 12000) { throw "390px screenshot is unexpectedly small for $($case.Culture)." }

    Write-Host "P06 responsive evidence $($case.Culture): viewport=$clientWidth scrollWidth=$scrollWidth PASS"
}

Remove-Item (Join-Path $artifactDir 'metric-auth-profiles-en.html') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $artifactDir 'metric-auth-profiles-ar.html') -Force -ErrorAction SilentlyContinue
