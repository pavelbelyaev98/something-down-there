<#
.SYNOPSIS
    Runs only the checks a working-copy change can actually break.

.DESCRIPTION
    Maps changed paths to the test classes that own them, then runs the EditMode
    assembly (cheap) plus just those PlayMode classes. Use -Full for the whole
    suite before a milestone, -List to only print the plan.

    Requires a connected Editor (same as the Unity CLI). With the Editor closed,
    use ./tools/test-fps.ps1 or ./tools/test-terrain.ps1 instead.

.EXAMPLE
    ./tools/test-changed.ps1
    ./tools/test-changed.ps1 unity/Assets/Runtime/Player/ShovelState.cs
    ./tools/test-changed.ps1 -Full
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Path = @(),
    [switch]$Full,
    [switch]$SkipEditMode,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projectPath = (Resolve-Path (Join-Path $root 'unity')).Path

if (-not $Path -or $Path.Count -eq 0) {
    $Path = @(git -C $root status --porcelain | ForEach-Object { $_.Substring(3).Trim() }) |
        Where-Object { $_ } | Sort-Object -Unique
}
$Path = @($Path | ForEach-Object { $_ -replace '\\', '/' })

# path pattern -> owning test classes. EditMode runs whole-assembly anyway; the
# PlayMode list is what keeps a tuning pass to a couple of minutes.
$map = @(
    @{ Pattern = '^unity/Assets/Runtime/Interaction/(FindDetector|DetectorTargeting)\.cs$|^unity/Assets/Runtime/UI/Toolkit/(DetectorCue|GameHudView)\.cs$|^unity/Assets/Editor/RetroComputerSetup\.cs$'; Play = @('DetectorIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/SalvageWinch.*\.cs$|^unity/Assets/Editor/SalvageWinchSetup\.cs$'; Play = @('UniqueRecoveryIntegrationTests', 'DetectorIntegrationTests') },
    @{ Pattern = '^art/.*catalog\.json$|^unity/Assets/Content/'; Play = @('DiscoveryIntegrationTests', 'FindCollectionFlowTests', 'StartupMenuTests', 'SaveIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/'; Play = @('DiscoveryIntegrationTests', 'FindCollectionFlowTests', 'FindPhysicsIntegrationTests', 'SaveIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Terrain/'; Play = @('TerrainIntegrationTests', 'SaveIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Player/ShovelState\.cs$'; Play = @('TerrainIntegrationTests', 'DiscoveryIntegrationTests', 'FpsUiInputTests') },
    @{ Pattern = '^unity/Assets/Runtime/Player/'; Play = @('FpsPlayerTests', 'FpsUiInputTests', 'RescueIntegrationTests', 'SurfaceRechargeTests', 'SaveIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/UI/'; Play = @('FpsUiInputTests', 'StationIntegrationTests', 'StartupMenuTests') },
    @{ Pattern = '^unity/Assets/Runtime/Persistence/'; Play = @('SaveIntegrationTests', 'StartupMenuTests') },
    @{ Pattern = '^unity/Assets/Editor/'; Play = @() },
    @{ Pattern = '^unity/Assets/Scenes/MainGame\.unity$'; Play = @('StartupMenuTests', 'SaveIntegrationTests', 'TerrainIntegrationTests', 'DiscoveryIntegrationTests') }
)

$play = New-Object System.Collections.Generic.HashSet[string]
$unknown = New-Object System.Collections.Generic.List[string]
foreach ($file in $Path) {
    if ($file -match '^docs/' -or $file -match '^builds/' -or $file -match '^Logs/') { continue }
    if ($file -match '^unity/Assets/Tests/(EditMode|PlayMode)/(.+)\.cs$') {
        if ($Matches[1] -eq 'PlayMode') { [void]$play.Add($Matches[2]) }
        continue
    }
    $matched = $false
    foreach ($rule in $map) {
        if ($file -match $rule.Pattern) { foreach ($name in $rule.Play) { [void]$play.Add($name) }; $matched = $true }
    }
    if (-not $matched -and $file -match '^unity/Assets/') { $unknown.Add($file) }
}

if ($Full) {
    $play.Clear()
    $play.Add('*')
}

Write-Host "Changed paths: $($Path.Count)" -ForegroundColor Cyan
foreach ($file in $Path) { Write-Host "  $file" }
Write-Host ($(if ($Full) { 'Checks: FULL suite' } else { "Checks: EditMode assembly + PlayMode: $(($play | Sort-Object) -join ', ')" })) -ForegroundColor Cyan
if ($unknown.Count -gt 0) {
    Write-Host "Unmapped (no test class guessed, review these):" -ForegroundColor Yellow
    foreach ($file in $unknown) { Write-Host "  $file" -ForegroundColor Yellow }
    if (-not $Full) { [void]$play.Add('StartupMenuTests') }
}
if ($List) { return }

function Get-EditorCount {
    $status = unity status --format json | ConvertFrom-Json
    if ($null -eq $status.data) { return 0 }
    return [int]$status.data.count
}

# No live Editor (the usual case when only the player is being used): fall back to
# batchmode runs of the same classes through tools/test-fps.ps1.
function Invoke-BatchTests([string]$mode, [string]$filter) {
    $arguments = @('-NoProfile', '-File', (Join-Path $PSScriptRoot 'test-fps.ps1'), '-Mode', $mode)
    if ($filter) { $arguments += @('-Filter', $filter) }
    & powershell @arguments | Select-Object -Last 3
    return $LASTEXITCODE
}

function Get-TestStatus {
    $raw = (& unity command test_status --project-path $projectPath --format json | ConvertFrom-Json).data.result
    if (-not $raw) { return $null }
    return ($raw | ConvertFrom-Json)
}

# Both modes run asynchronously so one status shape covers everything and a stale
# completed result from the previous run cannot be mistaken for this one.
function Invoke-UnityTests([string]$mode, [string]$filter) {
    $before = Get-TestStatus
    if ($before.status -eq 'running') { throw 'Wait for the active Unity test run to finish.' }
    # Unity's runner otherwise opens a modal save dialog. Intentional edits must
    # already be saved; unsaved scene experiments are disposable in this repo.
    $prepare = unity command eval_file --file (Join-Path $PSScriptRoot 'prepare-editor-tests.cs') --project-path $projectPath --format json | ConvertFrom-Json
    if (-not $prepare.success -or -not $prepare.data.result.success) {
        throw "Could not prepare a clean test scene: $($prepare | ConvertTo-Json -Depth 5 -Compress)"
    }
    $arguments = @('command', 'run_tests', '--project-path', $projectPath, '--format', 'json')
    if ($filter) { $arguments += @('--mode', $mode, '--filter', $filter, '--filter_type', 'testName') }
    else {
        $assembly = if ($mode -eq 'editor') { 'SomethingDownThere.EditModeTests' } else { 'SomethingDownThere.PlayModeTests' }
        $arguments += @('--mode', $mode, '--filter', $assembly, '--filter_type', 'assembly')
    }
    $arguments += @('--async_tests', 'true', '--timeout', '3600')
    $launch = & unity @arguments | ConvertFrom-Json
    if (-not $launch.success) { throw "Could not start tests: $($launch | ConvertTo-Json -Depth 5 -Compress)" }
    $deadline = (Get-Date).AddMinutes(45)
    $started = $false
    while ($true) {
        Start-Sleep -Seconds 5
        $status = Get-TestStatus
        if ($null -ne $status -and $status.status -eq 'running') { $started = $true }
        elseif ($started -or ($null -ne $status -and $status.duration -ne $before.duration)) { return $status }
        if ((Get-Date) -gt $deadline) { throw "Test run timed out: $mode $filter" }
    }
}

$failed = 0
if ((Get-EditorCount) -eq 0) {
    Write-Host 'No live Editor: running the same scope in batchmode (tools/test-fps.ps1).' -ForegroundColor Yellow
    if (-not $SkipEditMode) {
        Write-Host "`nEditMode assembly (batchmode)" -ForegroundColor Green
        if ((Invoke-BatchTests 'EditMode' '') -ne 0) { $failed++ }
    }
    if ($play.Count -gt 0) {
        foreach ($name in ($play | Sort-Object)) {
            if ($Full) { $name = '' }
            Write-Host "`nPlayMode $name (batchmode)" -ForegroundColor Green
            if ((Invoke-BatchTests 'PlayMode' $name) -ne 0) { $failed++ }
            if ($Full) { break }
        }
    }
    if ($failed -eq 0) { Write-Host "`nAll selected batch checks passed." -ForegroundColor Green; exit 0 }
    Write-Host "`n$failed batch run(s) failed." -ForegroundColor Red
    exit 1
}

if (-not $SkipEditMode) {
    Write-Host "`nEditMode assembly" -ForegroundColor Green
    $result = Invoke-UnityTests 'editor' $null
    Write-Host ("  total {0}  passed {1}  failed {2}  ({3:N0}s)" -f $result.summary.total, $result.summary.passed, $result.summary.failed, $result.duration)
    $failed += $result.summary.failed
    foreach ($test in ($result.results | Where-Object { $_.Status -ne 'Passed' })) {
        Write-Host ("  FAIL {0}`n       {1}" -f $test.FullName, ($test.Message -split "`n")[0]) -ForegroundColor Red
    }
}

if ($play.Count -gt 0) {
    foreach ($name in ($play | Sort-Object)) {
        Write-Host "`nPlayMode $name" -ForegroundColor Green
        if ($Full) {
            $result = Invoke-UnityTests 'playmode' $null
        } else {
            $result = Invoke-UnityTests 'playmode' $name
        }
        Write-Host ("  total {0}  passed {1}  failed {2}  ({3:N0}s)" -f $result.summary.total, $result.summary.passed, $result.summary.failed, $result.duration)
        $failed += $result.summary.failed
        foreach ($test in ($result.results | Where-Object { $_.Status -ne 'Passed' })) {
            Write-Host ("  FAIL {0}`n       {1}" -f $test.FullName, ($test.Message -split "`n")[0]) -ForegroundColor Red
        }
        if ($Full) { break }
    }
}

Write-Host ""
if ($failed -eq 0) { Write-Host "All selected checks passed." -ForegroundColor Green; exit 0 }
Write-Host "$failed test(s) failed." -ForegroundColor Red
exit 1
