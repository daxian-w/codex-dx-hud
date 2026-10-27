[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$MonitorPath)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = (Resolve-Path -LiteralPath $MonitorPath).Path
$directory = Join-Path $repoRoot ('.build\task-polling-test\' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $directory
$testExe = Join-Path $directory 'TaskPollingRegression.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /win32manifest:"$(Join-Path $repoRoot 'src\app.manifest')" /out:"$testExe" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'tests\TaskPollingRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Task polling test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\app.config') -Destination ($testExe + '.config')
$record = [pscustomobject]@{ RecordedAtUtc = [DateTime]::UtcNow.ToString('o'); ExecutableSha256 = (Get-FileHash -LiteralPath $executable).Hash.ToLowerInvariant() }
[IO.File]::WriteAllText((Join-Path $directory 'run.json'), ($record | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
& $testExe $executable (Join-Path $directory 'sessions') | Tee-Object -FilePath (Join-Path $directory 'result.log')
if ($LASTEXITCODE -ne 0) { throw 'Asynchronous task polling/UI bridge failed.' }
Write-Output "Evidence: $directory"
