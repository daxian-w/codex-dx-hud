[CmdletBinding()]
param([string]$MonitorPath = '', [string]$CaptureDirectory = '', [switch]$ResourceCheck)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($MonitorPath)) { $MonitorPath = Join-Path $repoRoot 'artifacts\CodexRateMonitor-Orb-Dev2\CodexRateMonitor.exe' }
if ([string]::IsNullOrWhiteSpace($CaptureDirectory)) { $CaptureDirectory = Join-Path $repoRoot '.build\orb-ui-regression\captures' }
$testRoot = Join-Path $repoRoot '.build\orb-ui-regression'
$null = New-Item -ItemType Directory -Path $testRoot -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $testRoot 'OrbUiRegression.exe'
& $compiler /nologo /target:exe /win32manifest:"$(Join-Path $repoRoot 'src\app.manifest')" /win32icon:"$(Join-Path $repoRoot 'assets\app.ico')" /out:"$testExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'tests\OrbUiRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Orb UI regression compilation failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\app.config') -Destination ($testExe + '.config') -Force
$testArguments = @(([IO.Path]::GetFullPath($MonitorPath)), ([IO.Path]::GetFullPath($CaptureDirectory)))
if ($ResourceCheck) { $testArguments += '--resource-check' }
$null = New-Item -ItemType Directory -Path $CaptureDirectory -Force
$runRecord = [pscustomobject]@{
    RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
    ExecutableSha256 = (Get-FileHash -LiteralPath $MonitorPath -Algorithm SHA256).Hash.ToLowerInvariant()
    TestSourceSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'tests\OrbUiRegression.cs') -Algorithm SHA256).Hash.ToLowerInvariant()
    ResourceCheck = [bool]$ResourceCheck
    Scope = 'Native UI fixture using actual product assembly; not full MonitorContext/app-server process performance.'
}
[IO.File]::WriteAllText((Join-Path ([IO.Path]::GetFullPath($CaptureDirectory)) 'run.json'), ($runRecord | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
& $testExe @testArguments 2>&1 | Tee-Object -FilePath (Join-Path ([IO.Path]::GetFullPath($CaptureDirectory)) 'result.log')
if ($LASTEXITCODE -ne 0) { throw 'Orb UI regression failed.' }
