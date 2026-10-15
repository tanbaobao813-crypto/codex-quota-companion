$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginRelative = 'plugins\codex-quota-companion'
$manifestPath = Join-Path $repoRoot ($pluginRelative + '\.codex-plugin\plugin.json')
$version = (Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json).version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
$stage = Join-Path $repoRoot ('build\package-' + [Guid]::NewGuid().ToString('N'))
$files = @('README.md','CHANGELOG.md','LICENSE','package.json','.gitignore','.agents\plugins\marketplace.json','启动客户端和额度伙伴.cmd','关闭额度伙伴.cmd','scripts\build.ps1','scripts\check-native.ps1','scripts\check-startup.ps1','scripts\package.ps1','tests\mcp.test.mjs','tests\StartupReliabilityChecks.cs')
$files += @('.codex-plugin\plugin.json','.mcp.json','hooks\hooks.json','scripts\control.mjs','scripts\mcp.mjs','scripts\session-start.mjs','assets\character.png','assets\preview.png','assets\NOTICE.md','bin\CodexQuotaWidget.exe') | ForEach-Object { $pluginRelative + '\' + $_ }
$files += Get-ChildItem -LiteralPath (Join-Path $repoRoot ($pluginRelative + '\src')) -File -Filter '*.cs' | ForEach-Object { $pluginRelative + '\src\' + $_.Name }
foreach ($relative in $files) {
    $source = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw ('Missing release file: ' + $relative) }
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
$hashLines = foreach ($relative in ($files | Sort-Object)) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $stage $relative) -Algorithm SHA256).Hash.ToLowerInvariant()
    $hash + '  ' + $relative.Replace('\','/')
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllLines((Join-Path $stage 'SHA256SUMS.txt'),[string[]]$hashLines,$utf8)
$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$zip = Join-Path $dist ('codex-quota-companion-v' + $version + '-windows.zip')
$temporaryZip = Join-Path $dist ([Guid]::NewGuid().ToString('N') + '.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$temporaryZip)
Move-Item -LiteralPath $temporaryZip -Destination $zip -Force
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'),$zipHash + '  ' + [IO.Path]::GetFileName($zip) + [Environment]::NewLine,$utf8)
Write-Output $zip
Write-Output ('SHA256 ' + $zipHash)
