[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$MonitorPath)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = (Resolve-Path -LiteralPath $MonitorPath).Path
$probeDirectory = Join-Path $repoRoot ('.build\task-state-probe\' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $probeDirectory
$probeExe = Join-Path $probeDirectory 'TaskStateProbe.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /out:"$probeExe" /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'tests\TaskStateProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Task-state probe compilation failed.' }
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\app.config') -Destination ($probeExe + '.config')
$record = [pscustomobject]@{
    RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
    ExecutableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
    Scope = 'Fresh provider from actual product assembly. Does not inspect state inside an existing process or prove account ownership.'
}
[IO.File]::WriteAllText((Join-Path $probeDirectory 'run.json'), ($record | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
& $probeExe $executable | Tee-Object -FilePath (Join-Path $probeDirectory 'snapshots.jsonl')
if ($LASTEXITCODE -ne 0) { throw 'Task-state probe failed; no raw protocol payloads were printed.' }
Write-Output "Evidence: $probeDirectory"
