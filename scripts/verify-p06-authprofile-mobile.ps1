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

function Invoke-CdpCommand {
    param(
        [Parameter(Mandatory)] [System.Net.WebSockets.ClientWebSocket] $Socket,
        [Parameter(Mandatory)] [int] $Id,
        [Parameter(Mandatory)] [string] $Method,
        [hashtable] $Params = @{}
    )

    $payload = [ordered]@{ id = $Id; method = $Method; params = $Params } | ConvertTo-Json -Depth 12 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = [System.ArraySegment[byte]]::new($bytes)
    $Socket.SendAsync(
        $segment,
        [System.Net.WebSockets.WebSocketMessageType]::Text,
        $true,
        [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()

    while ($true) {
        $stream = [System.IO.MemoryStream]::new()
        try {
            do {
                $buffer = New-Object byte[] 65536
                $receiveSegment = [System.ArraySegment[byte]]::new($buffer)
                $received = $Socket.ReceiveAsync(
                    $receiveSegment,
                    [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
                if ($received.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
                    throw "Browser DevTools connection closed while waiting for $Method."
                }
                if ($received.Count -gt 0) {
                    $stream.Write($buffer, 0, $received.Count)
                }
            } until ($received.EndOfMessage)

            $text = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
        }
        finally {
            $stream.Dispose()
        }

        if ([string]::IsNullOrWhiteSpace($text)) { continue }
        $message = $text | ConvertFrom-Json
        if ($null -eq $message.id -or [int]$message.id -ne $Id) { continue }
        if ($message.error) {
            throw "Browser DevTools command $Method failed: $($message.error.message)"
        }
        return $message.result
    }
}

function Start-ResponsiveBrowser {
    param([string] $ProfileDirectory)

    $port = Get-Random -Minimum 20000 -Maximum 45000
    $arguments = @(
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        '--hide-scrollbars',
        '--allow-file-access-from-files',
        '--force-device-scale-factor=1',
        "--remote-debugging-port=$port",
        "--user-data-dir=$ProfileDirectory",
        'about:blank'
    )
    $process = Start-Process $browser -ArgumentList $arguments -PassThru

    $version = $null
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        Start-Sleep -Milliseconds 100
        if ($process.HasExited) { break }
        try {
            $version = Invoke-RestMethod "http://127.0.0.1:$port/json/version" -TimeoutSec 2
            if ($version) { break }
        }
        catch { }
    }
    if (-not $version) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
        throw 'Browser DevTools endpoint did not become available for exact responsive evidence.'
    }

    $targets = Invoke-RestMethod "http://127.0.0.1:$port/json/list" -TimeoutSec 5
    $target = $targets | Where-Object { $_.type -eq 'page' } | Select-Object -First 1
    if (-not $target -or -not $target.webSocketDebuggerUrl) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw 'Browser DevTools page target was not available for responsive evidence.'
    }

    $socket = [System.Net.WebSockets.ClientWebSocket]::new()
    try {
        $socket.ConnectAsync(
            [Uri]$target.webSocketDebuggerUrl,
            [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
    }
    catch {
        $socket.Dispose()
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw
    }

    return [pscustomobject]@{
        Process = $process
        Socket = $socket
        Profile = $ProfileDirectory
    }
}

$cases = @(
    @{ Html='auth-profiles-en.html'; Screenshot='p06-auth-profiles-en-mobile.png'; Culture='en'; Direction='ltr' },
    @{ Html='auth-profiles-ar.html'; Screenshot='p06-auth-profiles-ar-mobile.png'; Culture='ar-KW'; Direction='rtl' }
)

foreach ($case in $cases) {
    $htmlPath = Join-Path $artifactDir $case.Html
    if (-not (Test-Path $htmlPath)) { throw "Missing authenticated evidence HTML: $($case.Html)" }

    $fileUrl = 'file:///' + $htmlPath.Replace('\','/')
    $profileDir = Join-Path $artifactDir ("browser-profile-" + ($case.Culture -replace '[^a-zA-Z0-9_-]', '-'))
    Remove-Item $profileDir -Recurse -Force -ErrorAction SilentlyContinue
    $session = Start-ResponsiveBrowser -ProfileDirectory $profileDir

    try {
        $socket = $session.Socket
        $commandId = 1
        $null = Invoke-CdpCommand -Socket $socket -Id $commandId -Method 'Page.enable'; $commandId++
        $null = Invoke-CdpCommand -Socket $socket -Id $commandId -Method 'Runtime.enable'; $commandId++
        $null = Invoke-CdpCommand -Socket $socket -Id $commandId -Method 'Emulation.setDeviceMetricsOverride' -Params @{
            width = 390
            height = 844
            deviceScaleFactor = 1
            mobile = $false
            screenWidth = 390
            screenHeight = 844
        }; $commandId++
        $null = Invoke-CdpCommand -Socket $socket -Id $commandId -Method 'Page.navigate' -Params @{ url = $fileUrl }; $commandId++

        $ready = $false
        $lastReadyState = '<unknown>'
        for ($attempt = 0; $attempt -lt 200; $attempt++) {
            Start-Sleep -Milliseconds 100
            $result = Invoke-CdpCommand -Socket $socket -Id $commandId -Method 'Runtime.evaluate' -Params @{
                expression = 'document.readyState'
                returnByValue = $true
            }; $commandId++
            $lastReadyState = [string]$result.result.value
            if ($lastReadyState -eq 'complete') { $ready = $true; break }
        }
        if (-not $ready) {
            throw "Responsive page did not finish loading for $($case.Culture) within 20 seconds (last readyState='$lastReadyState')."
        }

        $metricsResult = Invoke-CdpCommand -Socket $socket -Id $commandId -Method 'Runtime.evaluate' -Params @{
            expression = 'JSON.stringify({innerWidth:window.innerWidth,clientWidth:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,innerHeight:window.innerHeight})'
            returnByValue = $true
        }; $commandId++
        $metrics = $metricsResult.result.value | ConvertFrom-Json
        $innerWidth = [int]$metrics.innerWidth
        $clientWidth = [int]$metrics.clientWidth
        $scrollWidth = [int]$metrics.scrollWidth
        $innerHeight = [int]$metrics.innerHeight

        if ($innerWidth -ne 390 -or $clientWidth -ne 390) {
            throw "Narrow evidence viewport is not exact 390 CSS px for $($case.Culture): inner=$innerWidth client=$clientWidth."
        }
        if ($innerHeight -ne 844) {
            throw "Narrow evidence viewport height is not exact 844 CSS px for $($case.Culture): innerHeight=$innerHeight."
        }
        if ($scrollWidth -gt $clientWidth) {
            throw "Horizontal overflow detected for $($case.Culture) at 390px: scrollWidth=$scrollWidth clientWidth=$clientWidth."
        }

        $screenshotResult = Invoke-CdpCommand -Socket $socket -Id $commandId -Method 'Page.captureScreenshot' -Params @{
            format = 'png'
            fromSurface = $true
            captureBeyondViewport = $false
        }
        $output = Join-Path $artifactDir $case.Screenshot
        [System.IO.File]::WriteAllBytes($output, [Convert]::FromBase64String([string]$screenshotResult.data))
        if (-not (Test-Path $output) -or (Get-Item $output).Length -lt 12000) {
            throw "390px screenshot is unexpectedly small for $($case.Culture)."
        }

        Write-Host "P06 responsive evidence $($case.Culture): viewport=${clientWidth}x${innerHeight} scrollWidth=$scrollWidth PASS"
    }
    finally {
        if ($session.Socket) {
            try {
                if ($session.Socket.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
                    $session.Socket.CloseAsync(
                        [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                        'done',
                        [System.Threading.CancellationToken]::None).GetAwaiter().GetResult()
                }
            }
            catch { }
            $session.Socket.Dispose()
        }
        if ($session.Process -and -not $session.Process.HasExited) {
            Stop-Process -Id $session.Process.Id -Force -ErrorAction SilentlyContinue
        }
        Remove-Item $profileDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
