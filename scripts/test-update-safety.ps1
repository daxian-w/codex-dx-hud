[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$MonitorPath)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testDirectory = Join-Path $repoRoot ('.build\update-safety\' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDirectory
$testExe = Join-Path $testDirectory 'UpdateSafetyRegression.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /out:"$testExe" (Join-Path $PSScriptRoot 'tests\UpdateSafetyRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Update safety regression compilation failed.' }
& $testExe ([IO.Path]::GetFullPath($MonitorPath)) 2>&1 | Tee-Object -FilePath (Join-Path $testDirectory 'result.log')
if ($LASTEXITCODE -ne 0) { throw 'Update safety regression failed.' }
