$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $root 'artifacts'
$artifactDir = Join-Path $artifactsRoot 'p06-security-evidence'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$sentinel = $env:GSIP_P06_SECRET_SENTINEL
if ([string]::IsNullOrWhiteSpace($sentinel)) {
    throw 'GSIP_P06_SECRET_SENTINEL is required for P06 leakage verification.'
}

function Get-Sha256Text([string]$Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

function Test-ContainsBytes([byte[]]$Haystack, [byte[]]$Needle) {
    if ($Needle.Length -eq 0 -or $Haystack.Length -lt $Needle.Length) { return $false }
    for ($i = 0; $i -le $Haystack.Length - $Needle.Length; $i++) {
        $matches = $true
        for ($j = 0; $j -lt $Needle.Length; $j++) {
            if ($Haystack[$i + $j] -ne $Needle[$j]) { $matches = $false; break }
        }
        if ($matches) { return $true }
    }
    return $false
}

$sentinelHash = Get-Sha256Text $sentinel
$sentinelHash | Set-Content (Join-Path $artifactDir 'synthetic-sentinel.sha256') -Encoding ascii

$p06Directories = @(Get-ChildItem $artifactsRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'p06-*' } |
    Sort-Object FullName -Unique)
if ($p06Directories.Count -eq 0) { $p06Directories = @((Get-Item $artifactDir)) }

$specialistReferences = @($p06Directories |
    Where-Object { $_.FullName -ne $artifactDir } |
    ForEach-Object {
        $sourceDir = $_
        Get-ChildItem $sourceDir.FullName -Recurse -File -ErrorAction Stop | Sort-Object FullName | ForEach-Object {
            [ordered]@{
                source = $sourceDir.Name
                file = [System.IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
                bytes = $_.Length
                sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                category = if ($_.Name -match '(?i)migration|database|schema') { 'migration' }
                    elseif ($_.Name -match '(?i)authori|idor|permission|admin') { 'authorization-idor' }
                    elseif ($_.Name -match '(?i)redact|leak|secret') { 'redaction-no-leak' }
                    elseif ($_.Name -match '(?i)cache|token') { 'cache-isolation' }
                    elseif ($_.Name -match '(?i)browser|screenshot|ui') { 'browser' }
                    else { 'supporting' }
            }
        }
    })
[ordered]@{
    commit = $env:GITHUB_SHA
    references = $specialistReferences
} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $artifactDir 'specialist-evidence-references.json') -Encoding utf8

$browserReferences = @($specialistReferences | Where-Object { $_.category -eq 'browser' })
[ordered]@{
    status = if ($browserReferences.Count -gt 0) { 'available' } else { 'not_available_on_commit' }
    references = $browserReferences
    note = 'Browser/UI evidence is referenced only when generated on the same checked-out commit.'
} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $artifactDir 'browser-evidence-reference.json') -Encoding utf8

$p06Directories = @(Get-ChildItem $artifactsRoot -Directory -ErrorAction Stop |
    Where-Object { $_.Name -like 'p06-*' } |
    Sort-Object FullName -Unique)
$needle = [System.Text.Encoding]::UTF8.GetBytes($sentinel)
$files = @($p06Directories | ForEach-Object { Get-ChildItem $_.FullName -Recurse -File -ErrorAction Stop } | Sort-Object FullName -Unique)
$leakedFiles = New-Object System.Collections.Generic.List[string]
$unsafeHeaderFiles = New-Object System.Collections.Generic.List[string]
$textExtensions = @('.txt','.log','.json','.xml','.html','.htm','.csv','.md','.trx')
$unsafeHeaderPatterns = @(
    '(?im)Authorization\s*:\s*Bearer\s+(?!<redacted>|\[redacted\]|REDACTED)\S+',
    '(?im)x-api-key\s*[:=]\s*(?!<redacted>|\[redacted\]|REDACTED)\S+',
    '(?im)(consumer[_ -]?secret|client[_ -]?secret|password)\s*[:=]\s*(?!<redacted>|\[redacted\]|REDACTED|null|none)\S+'
)

foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    if (Test-ContainsBytes $bytes $needle) { $leakedFiles.Add($file.FullName) | Out-Null }
    if ($textExtensions -contains $file.Extension.ToLowerInvariant()) {
        $text = [System.IO.File]::ReadAllText($file.FullName)
        foreach ($pattern in $unsafeHeaderPatterns) {
            if ([regex]::IsMatch($text, $pattern)) {
                $unsafeHeaderFiles.Add($file.FullName) | Out-Null
                break
            }
        }
    }
}

if ($leakedFiles.Count -gt 0) {
    $relative = $leakedFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($root, $_) }
    throw ('Synthetic secret sentinel leaked into prohibited P06 output: ' + ($relative -join ', '))
}
if ($unsafeHeaderFiles.Count -gt 0) {
    $relative = $unsafeHeaderFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($root, $_) }
    throw ('Potential unredacted secret-bearing value found in P06 output: ' + ($relative -join ', '))
}

[ordered]@{
    phase = 'P06'
    commit = $env:GITHUB_SHA
    scannedDirectories = @($p06Directories.Name)
    scannedFiles = $files.Count
    syntheticSentinelSha256 = $sentinelHash
    plaintextSentinelOccurrences = 0
    unsafeSecretBearingOutputOccurrences = 0
    status = 'PASS'
    dataClassification = 'synthetic-only; no plaintext secret values retained'
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $artifactDir 'leak-scan-summary.json') -Encoding utf8

$manifestFiles = @(Get-ChildItem $artifactDir -File | Where-Object { $_.Name -notin @('manifest.json','manifest.sha256') } | Sort-Object Name)
$manifest = [ordered]@{
    project = 'GSIP'
    phase = 'P06'
    unit = 'P06::security-ci-evidence'
    commit = $env:GITHUB_SHA
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    secretMaterialIncluded = $false
    syntheticSentinelSha256 = $sentinelHash
    files = @($manifestFiles | ForEach-Object {
        [ordered]@{
            file = $_.Name
            bytes = $_.Length
            sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
}
$manifestPath = Join-Path $artifactDir 'manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content $manifestPath -Encoding utf8
(Get-FileHash $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content (Join-Path $artifactDir 'manifest.sha256') -Encoding ascii

Write-Host "P06 security evidence leak scan PASS; scanned $($p06Directories.Count) P06 artifact directories and $($files.Count) files; no plaintext sentinel retained."
