[CmdletBinding()]
param([string]$MonitorPath = '', [string]$CaptureDirectory = '', [switch]$LiveSource)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($MonitorPath)) { $MonitorPath = Join-Path $repoRoot 'artifacts\CodexRateMonitor-Orb-Dev\CodexRateMonitor.exe' }
if ([string]::IsNullOrWhiteSpace($CaptureDirectory)) { $CaptureDirectory = Join-Path $repoRoot '.build\orb-regression\captures' }
$testRoot = Join-Path $repoRoot '.build\orb-regression'
$null = New-Item -ItemType Directory -Path $testRoot -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $testRoot 'OrbRegression.exe'
& $compiler /nologo /target:exe /out:"$testExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'tests\OrbRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Orb regression compilation failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\app.config') -Destination ($testExe + '.config') -Force
$testArguments = @(([IO.Path]::GetFullPath($MonitorPath)), ([IO.Path]::GetFullPath($CaptureDirectory)))
if ($LiveSource) { $testArguments += '--live-source' }
& $testExe @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Orb regression failed.' }
