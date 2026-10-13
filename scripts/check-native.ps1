$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $repoRoot ('build\native-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$nativePath = Join-Path $repoRoot 'plugins\codex-quota-companion\bin\CodexQuotaWidget.exe'
$reportPath = Join-Path $testRoot 'report.json'
$previousData = $env:CODEX_QUOTA_DATA_DIR
try {
    $env:CODEX_QUOTA_DATA_DIR = Join-Path $testRoot 'private-data'
    & $nativePath --self-test | Write-Output
    if ($LASTEXITCODE -ne 0) { throw 'Quota parsing checks failed.' }
    & $nativePath --whale-check $reportPath | Out-Null
    if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Native check did not write a report.' }
    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $report.ok) { throw ('Native check failed: ' + $report.error) }
    Write-Output ('PASS: ' + $report.checks.Count + ' native window checks; no account requests.')
} finally { $env:CODEX_QUOTA_DATA_DIR = $previousData }
