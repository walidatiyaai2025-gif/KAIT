$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactDir = Join-Path $root 'artifacts/p16-full-acceptance/performance'
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$candidateSha = (git -C $root rev-parse HEAD).Trim()
if ($env:CANDIDATE_SHA -and $candidateSha -ne $env:CANDIDATE_SHA) {
    throw "P16 performance candidate mismatch: expected=$env:CANDIDATE_SHA actual=$candidateSha"
}

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$baseUrl = "http://127.0.0.1:$port"
$stdout = Join-Path $artifactDir 'server.stdout.log'
$stderr = Join-Path $artifactDir 'server.stderr.log'

$previousBypass = $env:Setup__BypassGateForRegression
$previousRegressionConnection = $env:IdentitySecurity__RegressionConnectionString
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
$previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
$env:Setup__BypassGateForRegression = 'true'
$env:IdentitySecurity__RegressionConnectionString = 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;Encrypt=false;TrustServerCertificate=true'
$env:DOTNET_ENVIRONMENT = 'RegressionTesting'
$env:ASPNETCORE_ENVIRONMENT = 'RegressionTesting'

function Get-Percentile([double[]]$Values, [double]$Percentile) {
    if (-not $Values -or $Values.Count -eq 0) { throw 'Cannot calculate percentile for an empty sample.' }
    $sorted = @($Values | Sort-Object)
    $index = [Math]::Ceiling($Percentile * $sorted.Count) - 1
    $index = [Math]::Max(0, [Math]::Min($sorted.Count - 1, $index))
    return [double]$sorted[$index]
}

function Measure-Endpoint([string]$Name, [string]$Uri, [int]$Count, [double]$P95LimitMs) {
    $samples = New-Object System.Collections.Generic.List[double]
    for ($i = 0; $i -lt $Count; $i++) {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest $Uri -UseBasicParsing -TimeoutSec 10 -SkipHttpErrorCheck
        $stopwatch.Stop()
        if ($response.StatusCode -ne 200) { throw "$Name returned HTTP $($response.StatusCode) during performance baseline." }
        $samples.Add($stopwatch.Elapsed.TotalMilliseconds)
    }
    $values = [double[]]$samples.ToArray()
    $average = ($values | Measure-Object -Average).Average
    $maximum = ($values | Measure-Object -Maximum).Maximum
    $p95 = Get-Percentile $values 0.95
    if ($p95 -gt $P95LimitMs) { throw "$Name p95 exceeded baseline limit: p95=$([Math]::Round($p95,2))ms limit=${P95LimitMs}ms" }
    if ($maximum -gt 5000) { throw "$Name single-request latency exceeded 5000ms: $([Math]::Round($maximum,2))ms" }
    return [ordered]@{
        Name = $Name
        Uri = $Uri.Replace($baseUrl, '')
        Samples = $Count
        AverageMs = [Math]::Round([double]$average, 2)
        P95Ms = [Math]::Round($p95, 2)
        MaxMs = [Math]::Round([double]$maximum, 2)
        P95LimitMs = $P95LimitMs
        Failures = 0
    }
}

$process = $null
try {
    $process = Start-Process dotnet -ArgumentList @('run','--project','src/GSIP.Web/GSIP.Web.csproj','--configuration','Release','--no-build','--urls',$baseUrl) -WorkingDirectory $root -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $healthy = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            $probe = Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 2 -SkipHttpErrorCheck
            if ($probe.StatusCode -eq 200) { $healthy = $true; break }
        } catch { }
        if ($process.HasExited) { break }
    }
    if (-not $healthy) { throw "GSIP.Web did not become healthy for P16 performance baseline. See $stderr" }

    for ($warmup = 0; $warmup -lt 5; $warmup++) {
        $null = Invoke-WebRequest "$baseUrl/health/live" -UseBasicParsing -TimeoutSec 5
        $null = Invoke-WebRequest "$baseUrl/login?culture=en" -UseBasicParsing -TimeoutSec 5
    }

    $results = @(
        (Measure-Endpoint 'health-live' "$baseUrl/health/live" 50 1500),
        (Measure-Endpoint 'login-en' "$baseUrl/login?culture=en" 20 2500),
        (Measure-Endpoint 'login-ar' "$baseUrl/login?culture=ar-KW" 20 2500)
    )

    $manifest = [ordered]@{
        CandidateSha = $candidateSha
        Unit = 'P16::full-acceptance-release-candidate'
        BaselineKind = 'bounded-local-smoke-not-capacity-certification'
        Runner = $env:RUNNER_NAME
        Results = $results
        Acceptance = 'PASS'
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $artifactDir 'performance-baseline.json') -Encoding utf8
    Write-Host 'P16_PERFORMANCE_BASELINE=PASS'
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    if ($null -eq $previousBypass) { Remove-Item Env:Setup__BypassGateForRegression -ErrorAction SilentlyContinue } else { $env:Setup__BypassGateForRegression = $previousBypass }
    if ($null -eq $previousRegressionConnection) { Remove-Item Env:IdentitySecurity__RegressionConnectionString -ErrorAction SilentlyContinue } else { $env:IdentitySecurity__RegressionConnectionString = $previousRegressionConnection }
    if ($null -eq $previousDotnetEnvironment) { Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment }
    if ($null -eq $previousAspNetCoreEnvironment) { Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue } else { $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment }
}
