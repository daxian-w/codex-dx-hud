[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateRange(1,2147483647)][int]$MonitorPid,
    [ValidateRange(0,2147483647)][int]$ChildPid = 0,
    [ValidateRange(10, 3600)][int]$DurationSeconds = 300,
    [ValidateRange(0, 600)][int]$WarmupSeconds = 0,
    [Parameter(Mandatory = $true)][string]$OutputFile,
    [ValidateSet('unverified','user-confirmed')][string]$StateAssurance = 'unverified',
    [ValidateSet('unspecified','collapsed','expanded','baseline')][string]$Scene = 'unspecified'
)
$ErrorActionPreference = 'Stop'
$rows = [Collections.Generic.List[object]]::new()
$processors = [Environment]::ProcessorCount
$outputPath = [IO.Path]::GetFullPath($OutputFile)
if ((Test-Path -LiteralPath $outputPath) -or (Test-Path -LiteralPath ($outputPath + '.json'))) {
    throw 'Evidence output already exists; use a new file name.'
}
$identities = @{}
foreach ($targetId in @($MonitorPid, $ChildPid)) {
    if ($targetId -le 0) { continue }
    $targetProcess = Get-Process -Id $targetId -ErrorAction Stop
    $identities[$targetId] = $targetProcess.StartTime.ToUniversalTime().Ticks
    if ($targetId -eq $MonitorPid) { $monitorExecutable = $targetProcess.Path }
    $targetProcess.Dispose()
}
if ($ChildPid -gt 0) {
    $childInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$ChildPid"
    if ($null -eq $childInfo -or $childInfo.ParentProcessId -ne $MonitorPid) {
        throw 'The requested app-server is not a direct child of this monitor.'
    }
}
$executableHash = (Get-FileHash -LiteralPath $monitorExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
$startedAtUtc = [DateTime]::UtcNow.ToString('o')
for ($warmup = 0; $warmup -lt $WarmupSeconds; $warmup += 2) { Start-Sleep -Seconds 2 }
$watch = [Diagnostics.Stopwatch]::StartNew()
$previous = @{}
while ($watch.Elapsed.TotalSeconds -lt $DurationSeconds) {
    foreach ($targetId in @($MonitorPid, $ChildPid)) {
        if ($targetId -le 0) { continue }
        $process = Get-Process -Id $targetId -ErrorAction SilentlyContinue
        if (-not $process) { throw "Measured process $targetId exited; no complete performance result." }
        if ($process.StartTime.ToUniversalTime().Ticks -ne $identities[$targetId]) {
            $process.Dispose()
            throw "Measured PID $targetId was reused; reject this run."
        }
        $sampleTime = $watch.Elapsed.TotalSeconds
        $cpu = $process.TotalProcessorTime.TotalSeconds
        $percent = $null
        if ($previous.ContainsKey($targetId)) {
            $prior = $previous[$targetId]
            $percent = 100 * ($cpu - $prior.Cpu) / ($sampleTime - $prior.Time) / $processors
        }
        $previous[$targetId] = @{ Cpu = $cpu; Time = $sampleTime }
        $rows.Add([pscustomobject]@{
            ElapsedSeconds = [math]::Round($sampleTime, 3)
            Role = if ($targetId -eq $MonitorPid) { 'monitor' } else { 'owned-app-server' }
            Pid = $targetId
            CpuPercent = $percent
            PrivateBytes = $process.PrivateMemorySize64
            WorkingSet = $process.WorkingSet64
            HandleCount = $process.HandleCount
        })
        $process.Dispose()
    }
    Start-Sleep -Seconds 2
}
$rows | Export-Csv -LiteralPath $outputPath -NoTypeInformation -Encoding UTF8
$metadata = [pscustomobject]@{
    StartedAtUtc = $startedAtUtc
    CompletedAtUtc = [DateTime]::UtcNow.ToString('o')
    MonitorPid = $MonitorPid
    OwnedChildPid = $ChildPid
    ExecutableSha256 = $executableHash
    WarmupSeconds = $WarmupSeconds
    DurationSeconds = $DurationSeconds
    LogicalProcessors = $processors
    CpuModel = @(Get-CimInstance Win32_Processor | ForEach-Object Name)
    OsVersion = [Environment]::OSVersion.Version.ToString()
    Scene = $Scene
    StateAssurance = $StateAssurance
    Limitations = 'Process counters do not prove continuously folded/expanded shape, focus, DPI, or absence of other workload; child aggregate is incomplete if OwnedChildPid is zero.'
}
[IO.File]::WriteAllText(($outputPath + '.json'), ($metadata | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
foreach ($group in ($rows | Group-Object Role)) {
    [pscustomobject]@{
        Role = $group.Name
        Samples = $group.Count
        AverageCpuPercent = [math]::Round(($group.Group | Where-Object { $null -ne $_.CpuPercent } | Measure-Object CpuPercent -Average).Average, 4)
        AveragePrivateMiB = [math]::Round(($group.Group | Measure-Object PrivateBytes -Average).Average / 1MB, 2)
        MaximumPrivateMiB = [math]::Round(($group.Group | Measure-Object PrivateBytes -Maximum).Maximum / 1MB, 2)
    } | Format-List
}
