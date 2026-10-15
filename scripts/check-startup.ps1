$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginRoot = Join-Path $repoRoot 'plugins\codex-quota-companion'
$checkRoot = Join-Path $repoRoot ('build\startup-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkRoot -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$files = @(Get-ChildItem -LiteralPath (Join-Path $pluginRoot 'src') -File -Filter '*.cs' | ForEach-Object { $_.FullName })
$fixture = Join-Path $checkRoot 'StartupReliabilityChecks.exe'
$resource = Join-Path $pluginRoot 'assets\character.png'
& $compiler /nologo /target:winexe /optimize+ /main:CodexQuotaWidget.StartupReliabilityChecks "/out:$fixture" "/resource:$resource,QuotaWidget.Character.png" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll @files (Join-Path $repoRoot 'tests\StartupReliabilityChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Startup fixture build failed.' }
$reportPath = Join-Path $checkRoot 'report.json'
& $fixture $reportPath | Out-Null
if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Startup fixture did not write a report.' }
$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $report.ok) { throw ('Startup check failed: ' + $report.error) }
Write-Output ('PASS: ' + $report.checks.Count + ' startup and lifetime checks; no account requests.')
