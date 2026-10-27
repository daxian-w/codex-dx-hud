[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BaselinePath,
    [Parameter(Mandatory = $true)][string]$MonitorPath
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baseline = (Resolve-Path -LiteralPath $BaselinePath).Path
$candidate = (Resolve-Path -LiteralPath $MonitorPath).Path
$runRoot = Join-Path $repoRoot ('.build\orb-migration\' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $runRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $runRoot 'OrbMigrationRegression.exe'
& $compiler /nologo /target:exe /out:"$testExe" /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'tests\OrbMigrationRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Migration harness compilation failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\app.config') -Destination ($testExe + '.config')
$template = Get-Content -LiteralPath (Join-Path (Split-Path $baseline) 'settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($mode in @('desktop','attach')) {
    $fixture = $template | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $fixture.OverlayMode = $mode
    $fixture.DesktopX = -320; $fixture.DesktopY = 180
    $fixture.Language = 'zh-TW'; $fixture.DisplayLines = '2'
    $fixture.Style.Scale = 1.35; $fixture.Style.Background = '#203040'
    $settingsPath = Join-Path $runRoot 'settings.json'
    [IO.File]::WriteAllText($settingsPath, ($fixture | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $beforeHash = (Get-FileHash -LiteralPath $settingsPath).Hash
    & $testExe $baseline capture
    if ($LASTEXITCODE -ne 0) { throw 'Old configuration load failed.' }
    & $testExe $candidate migrate
    if ($LASTEXITCODE -ne 0) { throw 'New configuration migration/persistence failed.' }
    $backupPath = $settingsPath + '.bak'
    if ((Get-FileHash -LiteralPath $backupPath).Hash -ne $beforeHash) { throw 'Migration backup does not match original bytes.' }
    Copy-Item -LiteralPath $backupPath -Destination $settingsPath -Force
    if ((Get-FileHash -LiteralPath $settingsPath).Hash -ne $beforeHash) { throw 'Restored settings differ from original.' }
    & $testExe $baseline rollback
    if ($LASTEXITCODE -ne 0) { throw 'Old configuration rollback load failed.' }
}
Write-Host "PASS desktop/attach config migration, orb persistence, exact backup restore and original-version reload: $runRoot"
Write-Host 'Scope: real old/new settings code; does not replace daily EXE or verify native startup/position visually.'
