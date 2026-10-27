[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$MonitorPath,
    [switch]$Launch
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$sourceExe = (Resolve-Path -LiteralPath $MonitorPath).Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $names = @($zip.Entries | ForEach-Object FullName)
    if ($names.Count -eq 0) { throw 'Package is empty.' }
    $roots = @($names | ForEach-Object { ($_ -split '/')[0] } | Sort-Object -Unique)
    if ($roots.Count -ne 1) { throw 'Expected one package root.' }
    foreach ($name in $names) {
        if ($name -match '(^|/)\.\.(/|$)|^[\\/]|^[A-Za-z]:|\\') { throw "Unsafe archive path: $name" }
        if ($name -match '(?i)(^|/)(auth\.json|.*\.jsonl|settings\.json\.bak|.*\.log|diagnostics|sessions)(/|$)') {
            throw "Private runtime data found in archive: $name"
        }
    }
    foreach ($required in @('CodexRateMonitor.exe','CodexRateMonitor.exe.config','settings.json','SHA256SUMS.txt',
        'LICENSE','README.md','README.zh-CN.md','README.zh-TW.md','assets/logo.png','assets/openai-mark.svg',
        'assets/simple-icons-LICENSE.txt','assets/THIRD-PARTY-NOTICES.md','docs/orb-spec.md',
        'docs/orb-guide.md','docs/capability-report.md','docs/orb-acceptance-report.md','docs/orb-performance.md')) {
        if (($roots[0] + '/' + $required) -notin $names) { throw "Missing package file: $required" }
    }
}
finally { $zip.Dispose() }
# Retain the evidence directory; never overwrite a prior extraction or user data.
$evidenceRoot = Join-Path $repoRoot ('.build\package-qa\中文 路径 ' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $evidenceRoot
[IO.Compression.ZipFile]::ExtractToDirectory($package, $evidenceRoot)
$unpacked = Join-Path $evidenceRoot $roots[0]
$unpackedExe = Join-Path $unpacked 'CodexRateMonitor.exe'
foreach ($document in Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs') -File | Where-Object { $_.Name -like 'orb-*.md' -or $_.Name -eq 'capability-report.md' }) {
    $packagedDocument = Join-Path (Join-Path $unpacked 'docs') $document.Name
    if (-not (Test-Path -LiteralPath $packagedDocument) -or
        (Get-FileHash -LiteralPath $packagedDocument).Hash -ne (Get-FileHash -LiteralPath $document.FullName).Hash) {
        throw "Packaged document is missing or stale: $($document.Name)"
    }
}
$exeHash = (Get-FileHash -LiteralPath $unpackedExe -Algorithm SHA256).Hash
if ($exeHash -ne (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash) { throw 'Packaged EXE differs from tested EXE.' }
$factorySettings = Join-Path $repoRoot 'config\settings.default.json'
if ((Get-FileHash -LiteralPath (Join-Path $unpacked 'settings.json')).Hash -ne
    (Get-FileHash -LiteralPath $factorySettings).Hash) { throw 'Package does not contain exact factory settings.' }
$settings = Get-Content -LiteralPath (Join-Path $unpacked 'settings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($settings.DesktopWidgetStyle -ne 'strip' -or $settings.Orb.SizePreset -ne 'compact') {
    throw 'Factory compatibility mode or default orb size changed.'
}
$hashRecord = Get-Content -LiteralPath (Join-Path $unpacked 'SHA256SUMS.txt') -Raw -Encoding UTF8
if ($hashRecord -notmatch ('(?i)' + [regex]::Escape($exeHash))) { throw 'Internal EXE hash record is incorrect.' }
if ($Launch) { & (Join-Path $PSScriptRoot 'test-development-lifecycle.ps1') -MonitorPath $unpackedExe }
$report = [pscustomobject]@{
    RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
    PackageSha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
    ExecutableSha256 = $exeHash.ToLowerInvariant()
    EntryCount = $names.Count
    ExtractionDirectory = $unpacked
    FactorySettings = 'exact source template; strip compatibility mode, compact orb size'
    DevelopmentLaunchAndExit = [bool]$Launch
    Note = 'Does not verify manual interaction, settings persistence, Windows11, or daily-instance upgrade/rollback.'
}
[IO.File]::WriteAllText((Join-Path $evidenceRoot 'result.json'), ($report | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$report | Format-List
Write-Host 'PASS package structure, runtime-data exclusion, factory settings and executable hash.'
