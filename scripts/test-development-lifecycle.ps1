[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$MonitorPath)
$ErrorActionPreference = 'Stop'
$executable = (Resolve-Path -LiteralPath $MonitorPath).Path
$existing = @(Get-CimInstance Win32_Process -Filter "Name='CodexRateMonitor.exe'" | Where-Object ExecutablePath -eq $executable)
if ($existing.Count -gt 0) {
    # Never close a user's running preview just to satisfy a test precondition.
    # Use the exact binary in an independent directory and only factory settings.
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $sourceDirectory = Split-Path $executable
    $sourceHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
    $isolatedDirectory = Join-Path $repoRoot ('.build\lifecycle-isolated\' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path (Join-Path $isolatedDirectory 'assets')
    Copy-Item -LiteralPath $executable -Destination (Join-Path $isolatedDirectory 'CodexRateMonitor.exe')
    Copy-Item -LiteralPath ($executable + '.config') -Destination (Join-Path $isolatedDirectory 'CodexRateMonitor.exe.config')
    foreach ($asset in @('logo.png','openai-mark.svg','simple-icons-LICENSE.txt','THIRD-PARTY-NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path (Join-Path $sourceDirectory 'assets') $asset) -Destination (Join-Path (Join-Path $isolatedDirectory 'assets') $asset)
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'config\settings.default.json') -Destination (Join-Path $isolatedDirectory 'settings.json')
    $executable = Join-Path $isolatedDirectory 'CodexRateMonitor.exe'
    if ((Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash -ne $sourceHash) { throw 'Isolated lifecycle binary differs from source.' }
    Write-Host "NOTE source directory is running; lifecycle uses isolated copy, SHA256=$($sourceHash.ToLowerInvariant())"
}
$primary = Start-Process -FilePath $executable -ArgumentList '--development-instance','--settings' -WorkingDirectory (Split-Path $executable) -WindowStyle Hidden -PassThru
try {
    # WaitForInputIdle may return while WinForms is still constructing its first
    # dialog. The app registers these signals only after MonitorContext returns.
    # Observe that readiness explicitly before testing duplicate-instance IPC.
    $instanceDirectory = (Split-Path $executable).TrimEnd('\') + '\'
    $instanceHasher = [Security.Cryptography.SHA256]::Create()
    try {
        $instanceSuffix = ([BitConverter]::ToString($instanceHasher.ComputeHash(
            [Text.Encoding]::UTF8.GetBytes($instanceDirectory.ToLowerInvariant())))).Replace('-', '').Substring(0, 16)
    } finally { $instanceHasher.Dispose() }
    $readyWatch = [Diagnostics.Stopwatch]::StartNew()
    $signalsReady = $false
    do {
        $primary.Refresh()
        if ($primary.HasExited) { throw 'Development monitor exited during startup.' }
        $settingsReady = $null
        $exitReady = $null
        try {
            $settingsReady = [Threading.EventWaitHandle]::OpenExisting('Local\CodexRateMonitorNative.Dev.' + $instanceSuffix + '.Settings')
            $exitReady = [Threading.EventWaitHandle]::OpenExisting('Local\CodexRateMonitorNative.Dev.' + $instanceSuffix + '.Exit')
            $signalsReady = $true
        } catch [Threading.WaitHandleCannotBeOpenedException] {
            Start-Sleep -Milliseconds 50
        } finally {
            if ($settingsReady) { $settingsReady.Dispose() }
            if ($exitReady) { $exitReady.Dispose() }
        }
    } while (-not $signalsReady -and $readyWatch.ElapsedMilliseconds -lt 8000)
    if (-not $signalsReady) { throw 'Development instance did not register settings and shutdown signals within eight seconds.' }
    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($primary.Id)" | Where-Object Name -eq 'codex.exe')
    $second = Start-Process -FilePath $executable -ArgumentList '--development-instance','--settings' -WindowStyle Hidden -PassThru
    if (-not $second.WaitForExit(5000)) { throw 'Duplicate instance did not exit.' }
    if ($second.ExitCode -ne 0) { throw 'The running instance did not accept the settings signal.' }
    $instances = @(Get-CimInstance Win32_Process -Filter "Name='CodexRateMonitor.exe'" | Where-Object ExecutablePath -eq $executable)
    if ($instances.Count -ne 1 -or $instances[0].ProcessId -ne $primary.Id) { throw 'Development singleton is not isolated correctly.' }
    $stop = Start-Process -FilePath $executable -ArgumentList '--development-instance','--shutdown-development' -WindowStyle Hidden -PassThru
    if (-not $stop.WaitForExit(5000) -or $stop.ExitCode -ne 0) { throw 'Application-owned shutdown signal failed.' }
    if (-not $primary.WaitForExit(5000)) { throw 'Monitor did not exit gracefully within five seconds.' }
    foreach ($child in $children) {
        if (Get-Process -Id $child.ProcessId -ErrorAction SilentlyContinue) { throw 'Monitor-owned app-server remained after shutdown.' }
    }
    Write-Host 'PASS isolated startup, single instance, settings signal, graceful shutdown and owned-child cleanup.'
}
finally {
    if (-not $primary.HasExited) {
        # Only request this application's explicit development-instance shutdown.
        $cleanup = Start-Process -FilePath $executable -ArgumentList '--development-instance','--shutdown-development' -WindowStyle Hidden -PassThru
        $null = $cleanup.WaitForExit(5000)
        $null = $primary.WaitForExit(5000)
    }
    $primary.Dispose()
}
