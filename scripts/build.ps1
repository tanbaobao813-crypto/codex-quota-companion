$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginRoot = Join-Path $repoRoot 'plugins\codex-quota-companion'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Windows .NET Framework compiler was not found.' }
$outputPath = Join-Path $pluginRoot 'bin\CodexQuotaWidget.exe'
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $pluginRoot 'src') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
$resourcePath = Join-Path $pluginRoot 'assets\character.png'
New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
& $compilerPath /nologo /target:winexe /optimize+ "/out:$outputPath" "/resource:$resourcePath,QuotaWidget.Character.png" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
Write-Output "Built $outputPath"
