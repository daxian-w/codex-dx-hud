[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$MonitorPath,
    [string]$EvidenceDirectory = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = (Resolve-Path -LiteralPath $MonitorPath).Path
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
    $EvidenceDirectory = Join-Path $repoRoot '.build\orb-release-qa'
}
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
$null = New-Item -ItemType Directory -Path $evidence -Force
$startingHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
$cases = @(
    'verify.ps1',
    'test-orb.ps1',
    'test-orb-ui.ps1',
    'test-task-polling.ps1',
    'test-usage-panels.ps1',
    'test-usage-refresh.ps1',
    'test-auth-recovery-regression.ps1',
    'test-update-safety.ps1',
    'test-scale-migration.ps1',
    'test-settings-layout.ps1',
    'test-settings-dpi-transition.ps1',
    'test-settings-opening.ps1',
    'test-overlay-surface.ps1',
    'test-overlay-dpi.ps1',
    'test-window-attachment.ps1',
    'test-fullscreen-overlay.ps1',
    'test-development-lifecycle.ps1'
)
$results = [Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $parameters = @{}
    if ($case -ne 'verify.ps1') { $parameters.MonitorPath = $executable }
    if ($case -in @('test-orb.ps1', 'test-orb-ui.ps1', 'test-settings-layout.ps1')) {
        $parameters.CaptureDirectory = Join-Path $evidence ([IO.Path]::GetFileNameWithoutExtension($case))
    }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $state = 'passed'
    $errorType = $null
    try {
        Write-Host "Running $case"
        & (Join-Path $PSScriptRoot $case) @parameters 2>&1 |
            Tee-Object -FilePath (Join-Path $evidence ($case + '.log'))
    }
    catch {
        $state = 'failed'
        $errorType = $_.Exception.GetType().Name
        Write-Host "FAILED $case : $($_.Exception.Message)"
    }
    $results.Add([pscustomobject]@{ Script = $case; Status = $state; Seconds = $watch.Elapsed.TotalSeconds; ErrorType = $errorType })
}
$endingHash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
if ($startingHash -ne $endingHash) { throw 'The tested executable changed during the release run.' }
$summary = [pscustomobject]@{
    RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
    ExecutableSha256 = $endingHash.ToLowerInvariant()
    FileVersion = (Get-Item -LiteralPath $executable).VersionInfo.FileVersion
    Results = $results.ToArray()
    RealPointerVerification = 'not covered by these programmatic tests'
    Windows11Verification = 'not covered by this Windows10 host'
}
[IO.File]::WriteAllText((Join-Path $evidence 'summary.json'), ($summary | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$results | Format-Table Script,Status,Seconds
if (@($results | Where-Object Status -eq 'failed').Count -gt 0) { throw 'Release regression has failed cases; inspect evidence logs.' }
Write-Host "PASS all $($results.Count) release groups against SHA256 $($endingHash.ToLowerInvariant())."
