<#
.SYNOPSIS
    Runs only the checks that changes since the last passing run can break.

.DESCRIPTION
    Incremental by default. Each run records the content hash of every path whose checks all
    passed (unity/Library/SomethingDownThere/test-changed-state.json, per machine). The next
    run selects only paths whose content changed since then (edited, reverted or committed),
    maps them to their owning test classes, and runs the EditMode assembly plus just those
    PlayMode classes. Paths with failing checks stay selected until they pass, so a rerun
    after a fix repeats only that area. Docs, art cards and tooling select nothing; an
    unchanged tree runs nothing.

    [Explicit] tests (slow end-to-end checks, the population sweep) run with -Full; the
    population sweep also runs when catalog or placement code changes. Package, asmdef and
    Unity-version changes widen the run to the whole default PlayMode assembly.

    -Full: everything, including [Explicit]. Use it before closing a phase or when asked.
    -All: ignore the recorded state and select every uncommitted change.
    -Path: scope a run by hand. -List: print the plan only.

    Requires a connected Editor (same as the Unity CLI); without one the same scope runs in
    batchmode through tools/test-fps.ps1.

    Every run except -List also deletes Logs/ and unity/Logs entries older than 14 days.

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
    [switch]$All,
    [switch]$SkipEditMode,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projectPath = (Resolve-Path (Join-Path $root 'unity')).Path
$statePath = Join-Path $projectPath 'Library/SomethingDownThere/test-changed-state.json'

# Generated evidence (screenshots, validation and review runs) is disposable: both Logs folders
# keep two weeks. Files Unity holds open are skipped.
if (-not $List) {
    $cutoff = (Get-Date).AddDays(-14)
    foreach ($logs in @((Join-Path $root 'Logs'), (Join-Path $projectPath 'Logs'))) {
        if (-not (Test-Path -LiteralPath $logs)) { continue }
        foreach ($item in @(Get-ChildItem -LiteralPath $logs -Force | Where-Object { $_.LastWriteTime -lt $cutoff })) {
            try { Remove-Item -LiteralPath $item.FullName -Recurse -Force -ErrorAction Stop } catch { }
        }
    }
}

# path pattern -> checks; the FIRST matching rule wins, so specific owners come before broad
# folders. Every selected path also runs the EditMode assembly (MainGameSceneTests covers
# scene, settings and editor wiring). Broad: the whole default PlayMode assembly.
# Population: EditMode also runs its [Explicit] population sweep.
$map = @(
    # Anything can break under a new package, assembly layout or Unity version.
    @{ Pattern = '^unity/Packages/(manifest|packages-lock)\.json$|\.asmdef$|^unity/ProjectSettings/ProjectVersion\.txt$'; Play = @(); Broad = $true },
    # Partial/helper test files: the suites that actually compile or use them.
    @{ Pattern = '^unity/Assets/Tests/PlayMode/FindCollectionFlowTests\.cs$'; Play = @('FindPhysicsIntegrationTests') },
    @{ Pattern = '^unity/Assets/Tests/PlayMode/CrouchTerrainFixture\.cs$'; Play = @('CrouchTests', 'TerrainIntegrationTests') },
    @{ Pattern = '^unity/Assets/Tests/PlayMode/TestInputPreferences\.cs$'; Play = @('FindPhysicsIntegrationTests', 'StationIntegrationTests', 'WorksiteToolsIntegrationTests') },
    @{ Pattern = '^unity/Assets/Tests/PlayMode/MenuTestUI\.cs$'; Play = @('FpsUiInputTests', 'StartupMenuTests') },
    @{ Pattern = '^unity/Assets/Tests/EditMode/(DiscoveryCatalogTests|DiscoveryPlacementTests)\.cs$'; Play = @(); Population = $true },
    @{ Pattern = '^unity/Assets/Tests/EditMode/'; Play = @() },
    # Interaction owners before the folder catch-all.
    @{ Pattern = '^unity/Assets/Runtime/Interaction/(FindDetector|DetectorTargeting)\.cs$|^unity/Assets/Runtime/UI/Toolkit/(DetectorCue|GameHudView)\.cs$|^unity/Assets/Editor/RetroComputerSetup\.cs$'; Play = @('DetectorIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/(SalvageCrane\w*|TowerCraneRig|CraneRopeView|RopeDynamics|ExtractionRoutePlanner|SalvageRopeSettings|RecoveryMarkView)\.cs$|^unity/Assets/Runtime/Persistence/ExtractionSnapshot\.cs$|^unity/Assets/Editor/SalvageCraneSetup\.cs$|^unity/Assets/Content/Salvage/|^unity/Assets/TowerCrane/'; Play = @('UniqueRecoveryIntegrationTests', 'DetectorIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/(ComputerStation|StationTrade|SessionWallet|SessionInventory|EquipmentProgression|InventoryItem)\.cs$|^unity/Assets/Runtime/UI/Toolkit/ToolkitStationRows\.cs$'; Play = @('StationIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/(WorksiteTools|WorkLamp|WorldMark)\.cs$|^unity/Assets/Editor/WorksiteToolsSetup\.cs$|^unity/Assets/Content/WorksiteTools/'; Play = @('WorksiteToolsIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/(DiscoveryField|DiscoveryCatalog)\.cs$'; Play = @('DiscoveryIntegrationTests', 'FindPhysicsIntegrationTests', 'SaveIntegrationTests'); Population = $true },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/(BuriedFind|FindState)\.cs$'; Play = @('DiscoveryIntegrationTests', 'FindPhysicsIntegrationTests', 'SaveIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Interaction/'; Play = @('DiscoveryIntegrationTests', 'FindPhysicsIntegrationTests') },
    # Find content; other content (shaders, materials, textures) is covered by EditMode checks.
    @{ Pattern = '^art/.*catalog\.json$|^unity/Assets/Content/Discoveries/DiscoveryCatalog\.asset$'; Play = @('DiscoveryIntegrationTests', 'FindPhysicsIntegrationTests'); Population = $true },
    @{ Pattern = '^unity/Assets/Content/(Discoveries|PhotoRocks|Minerals)/'; Play = @('DiscoveryIntegrationTests', 'FindPhysicsIntegrationTests') },
    @{ Pattern = '^unity/Assets/Content/'; Play = @() },
    @{ Pattern = '^unity/Assets/(StylizedShovel|HandMiningDrill)/'; Play = @() },
    # Player: settings, finds, crouch and fuel each have their own suite.
    @{ Pattern = '^unity/Assets/Runtime/Player/(GamePreferences|UnityGameSettingsPlatform|GraphicsQuality|GraphicsAutoTuner|DesktopWindow|DesktopInstance|DevicePreferencesFile|CameraPreferences|InputPreferences|InputBindingCapture|FpsInput)\.cs$'; Play = @('FpsUiInputTests') },
    @{ Pattern = '^unity/Assets/Runtime/Player/Find\w*\.cs$'; Play = @('FindPhysicsIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Player/PlayerCrouch\.cs$'; Play = @('CrouchTests') },
    @{ Pattern = '^unity/Assets/Runtime/Player/(Battery|SurfaceRecharge|RescueController|ReturnWarning)\.cs$'; Play = @('RescueIntegrationTests', 'SurfaceRechargeTests') },
    @{ Pattern = '^unity/Assets/Runtime/Player/ShovelState\.cs$'; Play = @('TerrainIntegrationTests', 'ShavingIntegrationTests', 'FpsUiInputTests') },
    @{ Pattern = '^unity/Assets/Runtime/Player/'; Play = @('FpsPlayerTests', 'FpsUiInputTests', 'TerrainIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Terrain/ExcavationDaylight'; Play = @('ExcavationDaylightIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Terrain/SiteLayout\.cs$'; Play = @('TerrainIntegrationTests', 'ShavingIntegrationTests', 'DiscoveryIntegrationTests', 'FindPhysicsIntegrationTests'); Population = $true },
    @{ Pattern = '^unity/Assets/Runtime/Terrain/'; Play = @('TerrainIntegrationTests', 'ShavingIntegrationTests') },
    @{ Pattern = '^unity/Assets/Runtime/Persistence/'; Play = @('SaveIntegrationTests', 'StartupMenuTests') },
    @{ Pattern = '^unity/Assets/Runtime/UI/Toolkit/GameMenuView\.cs$'; Play = @('FpsUiInputTests', 'StartupMenuTests') },
    @{ Pattern = '^unity/Assets/Runtime/UI/'; Play = @('FpsUiInputTests') },
    # Benchmark fixtures, editor tooling and project settings: EditMode only.
    @{ Pattern = '^unity/Assets/Runtime/Validation/|^unity/Assets/Editor/|^unity/Assets/Settings/|^unity/ProjectSettings/|^unity/Packages/'; Play = @() },
    @{ Pattern = '^unity/Assets/Scenes/MainGame(\.unity$|/)'; Play = @('StartupMenuTests') }
)
# Batchmode runs [Explicit] tests only when a name filter selects them directly.
$populationSweep = 'SomethingDownThere.Tests.DiscoveryCatalogTests'

# The checks a path selects, or $null when nothing can test it (docs, art cards, tooling).
function Get-Checks([string]$file) {
    # Test-runner scaffolding and the protected recovery scene are never under test.
    if ($file -match '\.meta$' -or $file -notmatch '^(unity|art)/' -or $file -match '^unity/Assets/(InitTestScene|_Recovery/)') { return $null }
    $rule = $map | Where-Object { $file -match $_.Pattern } | Select-Object -First 1
    if ($rule) { return @{ Play = @($rule.Play); Broad = [bool]$rule.Broad; Population = [bool]$rule.Population; Unknown = $false } }
    if ($file -match '^unity/Assets/Tests/PlayMode/(\w+)\.cs$') {
        # A deleted suite has nothing left to run.
        if (-not (Test-Path -LiteralPath (Join-Path $root $file))) { return $null }
        return @{ Play = @($Matches[1]); Broad = $false; Population = $false; Unknown = $false }
    }
    if ($file -match '^unity/Assets/') { return @{ Play = @('StartupMenuTests'); Broad = $false; Population = $false; Unknown = $true } }
    return $null
}

function Get-DirtyPaths {
    # -uall lists files inside untracked folders; renames report their new path.
    @(git -C $root status --porcelain -uall | ForEach-Object {
        $entry = $_.Substring(3)
        if ($entry -match ' -> ') { $entry = ($entry -split ' -> ')[-1] }
        $entry.Trim().Trim('"')
    }) | Where-Object { $_ } | Sort-Object -Unique
}

# Raw content hashes (no LFS filter): only compared with this script's own records.
function Get-ContentHashes([string[]]$paths) {
    $hashes = @{}
    foreach ($p in $paths) { $hashes[$p] = 'deleted' }
    $existing = @($paths | Where-Object { Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf })
    # Arguments, not stdin: Windows PowerShell can prefix piped native input with a BOM.
    for ($start = 0; $start -lt $existing.Count; $start += 100) {
        $chunk = @($existing[$start..([Math]::Min($start + 99, $existing.Count - 1))])
        $out = @(git -C $root hash-object --no-filters -- @chunk)
        if ($LASTEXITCODE -ne 0 -or $out.Count -ne $chunk.Count) { throw "Could not hash changed files." }
        for ($i = 0; $i -lt $chunk.Count; $i++) { $hashes[$chunk[$i]] = $out[$i] }
    }
    return $hashes
}

function Test-Commit([string]$sha) {
    $ErrorActionPreference = 'Continue'
    & git -C $root cat-file -e "$sha^{commit}" 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

# Files: path -> content hash last tested green. A path absent from Files was clean at Head.
# Play: path -> content hash whose PlayMode classes passed while EditMode failed, so a rerun
# repeats EditMode only.
function Read-State {
    if (-not (Test-Path $statePath)) { return $null }
    try { $raw = Get-Content $statePath -Raw | ConvertFrom-Json } catch { return $null }
    if (-not $raw.head -or -not (Test-Commit $raw.head)) { return $null }
    $files = @{}; $play = @{}
    if ($raw.files) { foreach ($p in $raw.files.PSObject.Properties) { $files[$p.Name] = [string]$p.Value } }
    if ($raw.play) { foreach ($p in $raw.play.PSObject.Properties) { $play[$p.Name] = [string]$p.Value } }
    return @{ Head = [string]$raw.head; Files = $files; Play = $play; Updated = [string]$raw.updated; LastFull = [string]$raw.lastFull }
}

function Write-State([string]$head, [hashtable]$files, [hashtable]$play, [string]$lastFull) {
    New-Item -ItemType Directory -Force (Split-Path $statePath) | Out-Null
    [pscustomobject]@{ head = $head; updated = (Get-Date).ToString('yyyy-MM-dd HH:mm'); lastFull = $lastFull; files = $files; play = $play } |
        ConvertTo-Json -Depth 3 | Set-Content -Path $statePath -Encoding UTF8
}

$head = (git -C $root rev-parse HEAD).Trim()
$state = $null
if (-not $All) { $state = Read-State }
$explicitPaths = $Path.Count -gt 0
$dirty = @()
if ($explicitPaths) {
    $candidates = @($Path | ForEach-Object { $_ -replace '\\', '/' } | Sort-Object -Unique)
} else {
    $dirty = @(Get-DirtyPaths)
    $set = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($p in $dirty) { [void]$set.Add($p) }
    if ($state) {
        if ($state.Head -ne $head) { foreach ($p in @(git -C $root diff --name-only $state.Head $head)) { if ($p) { [void]$set.Add($p) } } }
        foreach ($p in $state.Files.Keys) { [void]$set.Add($p) }
    }
    $candidates = @($set | Sort-Object)
}
# Hashed before running, so edits made during the run stay selected next time.
$hashes = Get-ContentHashes $candidates
$changed = @($candidates | Where-Object {
    $explicitPaths -or $Full -or -not $state -or -not $state.Files.ContainsKey($_) -or $state.Files[$_] -ne $hashes[$_] })

$checks = @{}
$play = New-Object 'System.Collections.Generic.HashSet[string]'
$unknown = New-Object System.Collections.Generic.List[string]
$broad = $false; $population = $false
# PlayMode classes already passed for this exact content (only EditMode failed last time).
function Test-PlayPassed([string]$file) {
    return -not $explicitPaths -and -not $Full -and $state -and $state.Play.ContainsKey($file) -and $state.Play[$file] -eq $hashes[$file]
}
foreach ($file in $changed) {
    $c = Get-Checks $file
    $checks[$file] = $c
    if ($null -eq $c) { continue }
    if (-not (Test-PlayPassed $file)) {
        foreach ($name in $c.Play) { [void]$play.Add($name) }
        if ($c.Broad) { $broad = $true }
    }
    if ($c.Population) { $population = $true }
    if ($c.Unknown) { $unknown.Add($file) }
}
$testable = @($changed | Where-Object { $null -ne $checks[$_] })

$scope = if ($explicitPaths) { 'given paths' } elseif ($state) { "changes since the last passing run ($($state.Updated))" } else { 'all uncommitted changes (no passing run recorded yet)' }
Write-Host "Testing $scope`: $($testable.Count) testable of $($changed.Count) changed" -ForegroundColor Cyan
foreach ($file in ($testable | Select-Object -First 20)) { Write-Host "  $file" }
if ($testable.Count -gt 20) { Write-Host "  ... and $($testable.Count - 20) more" }
$edit = if ($SkipEditMode) { 'no EditMode' } elseif ($population) { 'EditMode assembly + population sweep' } else { 'EditMode assembly' }
$classes = if ($broad) { 'whole default assembly' } else { ($play | Sort-Object) -join ', ' }
$plan = if ($Full) { 'FULL suite, including [Explicit]' } elseif ($testable.Count -eq 0) { 'none' } else { "$edit + PlayMode: $classes" }
Write-Host "Checks: $plan" -ForegroundColor Cyan
if ($unknown.Count -gt 0) {
    Write-Host "Unmapped (StartupMenuTests guessed; add a rule):" -ForegroundColor Yellow
    foreach ($file in $unknown) { Write-Host "  $file" -ForegroundColor Yellow }
}
$lastFull = if ($state -and $state.LastFull) { $state.LastFull } else { 'never recorded' }
if (-not $Full) { Write-Host "Last full run: $lastFull (run -Full before closing a phase)." -ForegroundColor DarkGray }
if ($List) { return }

$failed = 0
$editFailed = $false
$failedClasses = New-Object 'System.Collections.Generic.HashSet[string]'
$assemblyFailed = $false
# Batch runs report only exit codes; assembly-wide failures cannot be attributed to paths.
$unattributed = $false

# Records every changed path whose checks all passed; see Read-State for the format.
function Save-Results {
    $playOk = @($changed | Where-Object {
        $c = $checks[$_]
        if ($null -eq $c -or (Test-PlayPassed $_)) { return $true }
        if ($unattributed -or ($c.Broad -and $assemblyFailed)) { return $false }
        foreach ($name in $c.Play) { if ($failedClasses.Contains($name)) { return $false } }
        return $true
    })
    $passed = @($playOk | Where-Object { $null -eq $checks[$_] -or -not ($SkipEditMode -or $editFailed) })
    $previousFull = if ($state) { $state.LastFull } else { '' }
    $fullStamp = if ($Full -and $failed -eq 0) { (Get-Date).ToString('yyyy-MM-dd HH:mm') } else { $previousFull }
    if ($explicitPaths) {
        # Hand-scoped runs only refine an existing record; they never vouch for other paths.
        if (-not $state) { return }
        foreach ($p in $passed) { $state.Files[$p] = $hashes[$p] }
        Write-State $state.Head $state.Files $state.Play $fullStamp
    } elseif ($passed.Count -eq $changed.Count) {
        # Everything uncommitted now holds tested content; clean paths match HEAD.
        $files = @{}
        foreach ($p in $dirty) { $files[$p] = $hashes[$p] }
        Write-State $head $files @{} $fullStamp
    } else {
        if (-not $state) { $state = @{ Head = $head; Files = @{}; Play = @{} } }
        foreach ($p in $playOk) { if ($null -ne $checks[$p]) { $state.Play[$p] = $hashes[$p] } }
        foreach ($p in $passed) { $state.Files[$p] = $hashes[$p]; $state.Play.Remove($p) }
        Write-State $state.Head $state.Files $state.Play $fullStamp
    }
}

if ($testable.Count -eq 0 -and -not $Full) {
    Save-Results
    Write-Host "`nNothing to test." -ForegroundColor Green
    exit 0
}

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
    # Show the runner's tail without letting it into the return value.
    & powershell @arguments | Select-Object -Last 3 | Out-Host
    return $LASTEXITCODE
}

# The Editor drops CLI requests while it reloads for Play Mode; that reads as "no status yet".
function Get-TestStatus {
    try { $raw = (& unity command test_status --project-path $projectPath --format json | ConvertFrom-Json).data.result }
    catch { return $null }
    if (-not $raw) { return $null }
    return ($raw | ConvertFrom-Json)
}

# Both modes run asynchronously so one status shape covers everything and a stale
# completed result from the previous run cannot be mistaken for this one.
function Invoke-UnityTests([string]$mode, [string]$filter, [bool]$explicit = $false) {
    $before = Get-TestStatus
    if ($before.status -eq 'running') { throw 'Wait for the active Unity test run to finish.' }
    # Unity's runner otherwise opens a modal save dialog. Intentional edits must
    # already be saved; unsaved scene experiments are disposable in this repo.
    # The Editor can still be busy leaving the previous Play Mode run; retry briefly.
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        $prepare = unity command eval_file --file (Join-Path $PSScriptRoot 'prepare-editor-tests.cs') --project-path $projectPath --format json | ConvertFrom-Json
        if ($prepare.success -and $prepare.data.result.success) { break }
        Start-Sleep -Seconds 10
    }
    if (-not $prepare.success -or -not $prepare.data.result.success) {
        throw "Could not prepare a clean test scene: $($prepare | ConvertTo-Json -Depth 5 -Compress)"
    }
    $arguments = @('command', 'run_tests', '--project-path', $projectPath, '--format', 'json')
    if ($filter) { $arguments += @('--mode', $mode, '--filter', $filter, '--filter_type', 'testName') }
    else {
        $assembly = if ($mode -eq 'editor') { 'SomethingDownThere.EditModeTests' } else { 'SomethingDownThere.PlayModeTests' }
        $arguments += @('--mode', $mode, '--filter', $assembly, '--filter_type', 'assembly')
    }
    if ($explicit) { $arguments += @('--include_explicit', 'true') }
    $arguments += @('--async_tests', 'true', '--timeout', '3600')
    $launch = & unity @arguments | ConvertFrom-Json
    if (-not $launch.success) { throw "Could not start tests: $($launch | ConvertTo-Json -Depth 5 -Compress)" }
    $deadline = (Get-Date).AddMinutes(45)
    $started = $false
    while ($true) {
        Start-Sleep -Seconds 2
        $status = Get-TestStatus
        if ((Get-Date) -gt $deadline) { throw "Test run timed out: $mode $filter" }
        if ($null -eq $status) { continue }
        if ($status.status -eq 'running') { $started = $true; continue }
        # Only a finished run counts; a transitional status is not a result.
        if ($status.status -in 'completed', 'error' -and ($started -or $status.duration -ne $before.duration)) { return $status }
    }
}

# A live Editor imports edits made outside it only when it regains focus, so without this
# the runner silently tests stale assemblies. Recompiles only when a script or asmdef is
# newer than the compiled output; stops on compile errors.
function Sync-Scripts {
    $compiled = Get-ChildItem (Join-Path $projectPath 'Library/ScriptAssemblies') -File -Include *.dll, *.pdb -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    $edited = Get-ChildItem (Join-Path $projectPath 'Assets') -File -Include *.cs, *.asmdef -Recurse |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($compiled -and $edited.LastWriteTimeUtc -le $compiled.LastWriteTimeUtc) { return }
    Write-Host "Compiling external edits ($($edited.Name))..." -ForegroundColor Yellow
    $launch = unity command recompile --project-path $projectPath --format json | ConvertFrom-Json
    if (-not $launch.success) { throw "Could not start a recompile: $($launch | ConvertTo-Json -Depth 5 -Compress)" }
    $deadline = (Get-Date).AddMinutes(5)
    while ($true) {
        Start-Sleep -Seconds 3
        # The Editor drops CLI requests during the domain reload; keep polling.
        try { $state = (unity command recompile_status --project-path $projectPath --format json | ConvertFrom-Json).data.result | ConvertFrom-Json }
        catch { $state = $null }
        if ($state -and $state.status -notin 'triggered', 'compiling') { break }
        if ((Get-Date) -gt $deadline) { throw 'Recompile timed out.' }
    }
    if ($state.failed) { throw "Scripts have compile errors: $($state.errors | ConvertTo-Json -Depth 5 -Compress)" }
}

# Failures in a run; a run that did not complete counts as one.
function Get-Failures($result) {
    if ($result.status -ne 'completed' -or $null -eq $result.summary) { return [Math]::Max(1, [int]$result.summary.failed) }
    return [int]$result.summary.failed
}

function Write-Result($result) {
    if ($result.status -ne 'completed') { Write-Host "  FAIL run did not complete: $($result.message)" -ForegroundColor Red; return }
    Write-Host ("  total {0}  passed {1}  failed {2}  ({3:N0}s)" -f $result.summary.total, $result.summary.passed, $result.summary.failed, $result.duration)
    foreach ($test in ($result.results | Where-Object { $_.Status -ne 'Passed' })) {
        Write-Host ("  FAIL {0}`n       {1}" -f $test.FullName, ($test.Message -split "`n")[0]) -ForegroundColor Red
    }
}

# SomethingDownThere.Tests.<Class>.<Test>
function Get-TestClass([string]$fullName) { return ($fullName -split '\.')[2] }

if ((Get-EditorCount) -eq 0) {
    Write-Host 'No live Editor: running the same scope in batchmode (tools/test-fps.ps1).' -ForegroundColor Yellow
    if (-not $SkipEditMode) {
        Write-Host "`nEditMode assembly (batchmode)" -ForegroundColor Green
        if ((Invoke-BatchTests 'EditMode' '') -ne 0) { $failed++; $editFailed = $true }
        if ($population -or $Full) {
            Write-Host "`nEditMode population sweep (batchmode)" -ForegroundColor Green
            if ((Invoke-BatchTests 'EditMode' $populationSweep) -ne 0) { $failed++; $editFailed = $true }
        }
    }
    if ($Full -or $broad) {
        Write-Host "`nPlayMode assembly (batchmode)" -ForegroundColor Green
        if ((Invoke-BatchTests 'PlayMode' '') -ne 0) { $failed++; $unattributed = $true }
    } else {
        foreach ($name in ($play | Sort-Object)) {
            Write-Host "`nPlayMode $name (batchmode)" -ForegroundColor Green
            if ((Invoke-BatchTests 'PlayMode' $name) -ne 0) { $failed++; [void]$failedClasses.Add($name) }
        }
    }
    Save-Results
    if ($failed -eq 0) { Write-Host "`nAll selected batch checks passed." -ForegroundColor Green; exit 0 }
    Write-Host "`n$failed batch run(s) failed." -ForegroundColor Red
    exit 1
}

Sync-Scripts
if (-not $SkipEditMode) {
    Write-Host "`nEditMode assembly$(if ($population -or $Full) { ' + [Explicit]' })" -ForegroundColor Green
    $result = Invoke-UnityTests 'editor' $null ($population -or $Full)
    Write-Result $result
    $failed += Get-Failures $result
    if ((Get-Failures $result) -gt 0) { $editFailed = $true }
}

if ($Full -or $broad) {
    Write-Host "`nPlayMode assembly$(if ($Full) { ' + [Explicit]' })" -ForegroundColor Green
    $result = Invoke-UnityTests 'playmode' $null ([bool]$Full)
    Write-Result $result
    $failed += Get-Failures $result
    if ((Get-Failures $result) -gt 0) { $assemblyFailed = $true }
    if ($result.status -ne 'completed') { $unattributed = $true }
    foreach ($test in ($result.results | Where-Object { $_.Status -ne 'Passed' })) { [void]$failedClasses.Add((Get-TestClass $test.FullName)) }
} else {
    foreach ($name in ($play | Sort-Object)) {
        Write-Host "`nPlayMode $name" -ForegroundColor Green
        $result = Invoke-UnityTests 'playmode' $name
        Write-Result $result
        $failed += Get-Failures $result
        if ((Get-Failures $result) -gt 0) { [void]$failedClasses.Add($name) }
        # A selected class that runs nothing means the mapping points at a missing class.
        if ($result.status -eq 'completed' -and $result.summary.total -eq 0) {
            $failed++; [void]$failedClasses.Add($name)
            Write-Host "  FAIL no tests ran for '$name': fix the mapping in tools/test-changed.ps1" -ForegroundColor Red
        }
    }
}

Save-Results
Write-Host ""
if ($failed -eq 0) { Write-Host "All selected checks passed." -ForegroundColor Green; exit 0 }
Write-Host "$failed test(s) failed; the next run repeats only the failing areas and new edits." -ForegroundColor Red
exit 1
