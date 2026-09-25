# Unity developer guide

Unity is pinned to `6000.6.0f1` with URP `17.6.0`. Runtime work belongs in `Assets/`; generated evidence stays under ignored `Logs/`.

The runtime assembly references the Editor's bundled Burst, Collections and Mathematics assemblies for surface-net generation and local exposure sampling. Keep Burst enabled in players. Jobs complete before collision/render publication; `TerrainVolume` owns their reusable native buffers, while managed paged density retains save ownership.

Follow [AGENTS.md](../AGENTS.md) for dependency and purchase policy: proactively request useful assets/utilities, and install justified free libraries with compatible licenses and pinned versions. Before release, only current code/data formats are supported; remove legacy readers, migrations, aliases and superseded assets instead of maintaining backward compatibility. Older saves may require New Game; current-format integrity and recovery still matter.

## Official Unity CLI and Pipeline

Use Unity's official CLI directly from the terminal with `com.unity.pipeline` for live editor work; no other server or bridge is configured.

```powershell
$projectPath = (Resolve-Path ./unity).Path
unity --version
unity pipeline install --project-path "$projectPath" --format json
unity pipeline list --format json
unity status --format json
unity command --project-path "$projectPath" --format json
```

Keep the project open in Unity for live commands, or open it with `unity open ./unity`. Resolve the project path to a full path; this CLI version can fail to match an open Editor with a relative path. Discover command parameters from the connected Editor. Prefer direct C# edits and CLI inspection of live state; after script changes use `recompile`, then poll `recompile_status`. Do not start a second Editor on the same project. Setup uses the official [agent plugin](https://github.com/Unity-Technologies/unity-agent-plugin) and [CLI skill](https://github.com/Unity-Technologies/skills/tree/main/skills/unity-cli).

## Windows build and validation

- With the project open: `unity command menu --path 'Tools/Something Down There/Build Windows Player' --timeout 300 --project-path "$projectPath" --format json`. Allow 300 seconds; the default request timeout can expire while Unity continues.
- With the Editor closed: `./tools/build-windows.ps1`.
- `Build Windows Release Player` writes the same path without development admin access. `Debug.isDebugBuild` gates admin; release builds must exclude it (`22`). Keep Unity's `forceSingleInstance` disabled; repeated launches focus the existing window.
- Development admin: **Ctrl+Shift+F10** opens the panel; **Motion: Automatic/Override** compares the level-selected shovel/drill action with the other motion. Close with **Resume digging** and press/hold dig again. Hold Ctrl+Shift with **1-9, 0** to select tool levels (0 selects the final level), **R** refill, **X** transparent-ground X-ray, **Home** return. Session-only; Restore normal rules returns to the owned level and its automatic motion.
- Tests with the Editor closed: `./tools/test-fps.ps1`, `./tools/test-terrain.ps1` (isolated copy). With the Editor open: `./tools/test-changed.ps1` runs the EditMode assembly plus only the PlayMode classes that own the changed files (`-Full` for everything, `-List` to print the plan, `-Path <file>` to scope it by hand). For a manual run: `unity command run_tests --mode editor --filter SomethingDownThere.EditModeTests --filter_type assembly --async_tests true --project-path "$projectPath" --format json`, then poll `test_status`; repeat with `--mode playmode --filter SomethingDownThere.PlayModeTests`. Save scene edits first.
- Play Mode visual inspection: use `capture_game_view --source screen` for the actual composed frame. `screenshot` omits overlay UI; confirm apparent missing geometry in the screen capture. Keep evidence under `Logs/`; `capture_game_view` roots even an absolute `save_path` under `Assets`, so pass a relative path, then move the image out and delete the generated folder through the AssetDatabase.
- Test scene preflight: save intentional authored changes, then run `unity command eval_file --file "$PWD/tools/prepare-editor-tests.cs" --project-path "$projectPath" --format json` from the repository root before a manual test run. `test-changed.ps1` does this automatically. It discards unsaved Editor scene changes under the user's standing permission and starts from an empty scene, preventing Unity's modal save prompt; it never writes scene files or deletes test scaffolding.
- Saving fixture: **Tools > Something Down There > Validation > Build Save Performance Player** builds `builds/validation/saving/SavePerformance.exe`; `-saveProfileSeconds 5` is a smoke check, not a qualification run.
- Recovery profiling: **Tools > Something Down There > Validation > Build Surface Performance Player**, then pass `--recovery-save <copied-world.sav> --recovery-report <report.json>` and optional `--recovery-xray`. It resumes an attached copied recovery automatically with the full population, saved lamps and a fixed camera, using the same recorded device frame cap for idle/haul. Reports include p99/peak frame times and per-stage costs for slow frames; it never owns or writes a game save.
- Environment profiling: **Validation > Build Environment Performance Player** writes the same executable using a disposable MainGame copy without its save owner. Single-scene startup preserves actual scene lighting and baked occlusion. Run with `--environment-report <absolute-report.json>`; add `--environment-final` for previous-quality/occlusion/shadow/MSAA comparisons plus camp/boundary views, `--environment-candidates` for experiments, or `--environment-variants <comma-separated-variant-names>` for a subset. Keep its window visible for GPU timing. It measures at 2560×1440 with isolated preferences and an uncapped renderer; reports and comparison PNGs go beside the report. Overrides never modify MainGame or device preferences; a missing GPU timer, wrong scene lighting or changed frame cap invalidates the run.
- Native Windows reviews share the user's desktop: announce input control, verify game focus, and repeat checks interrupted by user input. See [AGENTS.md](../AGENTS.md).

## Odin Inspector and Validator

- Approved installed version: **4.0.2.4** for both Inspector/Serializer and Validator, under `Assets/Plugins/Sirenix`. Update them together and preserve publisher GUIDs.
- Source/license: user-purchased Sirenix packages from Unity Asset Store, under the purchased seat license; private repository only. Vendor binaries use Git LFS; personal activation credentials stay outside the repository.
- Unity 6.6 import adjustment: reserialized five outdated plugin importer metadata files after enabling Editor Only mode; publisher binaries and GUIDs preserved. Retain Editor Only mode and Validator profile/automation settings when updating.
- Use Inspector attributes or Visual Designer when they simplify content authoring and tuning. Keep ordinary Unity serialization unless a concrete requirement needs more; existing `WorldSaveController`/save codec still own persistence.
- **Editor Only mode is enabled**: Inspector and Validator work in the Editor; the unused Odin runtime serializer is excluded from players. [Publisher guidance](https://odininspector.com/tutorials/getting-started/editor-only-mode).
- `Assets/Editor/Validation/MainGameValidation.asset` scans MainGame with dependencies and `Assets/Content`. Recovery and unused BK demo scenes are excluded; vendor assets actually used by MainGame remain covered. Open this profile in **Tools > Odin > Validator** for a manual scan.
- Odin's built-in build automation runs this specific profile to completion before a build, stops on errors and logs warnings. Configuration lives in `Assets/Plugins/Sirenix/Odin Validator/Editor/Config/AutomationConfig.asset`; retain it in source control. No automatic fixes are enabled.
- CLI scripts can load the profile, construct `Sirenix.OdinValidator.Editor.ValidationSession`, and enumerate `ValidateEverythingEnumerator(openClosedScenes: true, showProgressBar: false)`. Inspect each returned `ResultType`; session counters alone do not count this manual enumeration. Save reports under ignored `Logs/`. [Publisher API example](https://odininspector.com/tutorials/odin-validator/using-the-validator-in-your-custom-pipeline).
- Add project-specific rules in `Assets/Editor` when real failure cases justify them. Built-in checks find broken asset setup; digging behavior, performance, rendering quality and save correctness still need their existing tests and playtests.

## Blender MCP

The Blender Lab `MCP` extension 1.0.0 runs in Blender 5.2 on `127.0.0.1:9876`. Its official stdio bridge lives in the isolated, ignored `.tools/blender-mcp/` environment: `blender-mcp` 1.0.0 pinned to commit `4309a39646e644261624bfcd2bca669b343b7621`, MCP SDK 1.30.0 (`<2`). No art is created by setup.

- Run `./tools/setup-blender-mcp-for-codex.ps1 -Install` to reproduce the installation (omit `-Install` to register an existing one); `tools/blender-mcp-server.cmd` launches the same executable. Keep the Blender add-on running and restart the Codex extension after registration. The add-on is the user's pre-existing installation, unmodified. [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).

## Purchased assets and Git

- Keep the complete approved `Assets/BK` import and `Assets/BK.meta` in the private project repository for reproducible clones and future use. The initial import is approximately 1.65 GB; account for Git LFS storage/transfer quotas before pushing.
- `.gitattributes` sends BK textures, models and binary terrain/lighting data to Git LFS. Keep all `.meta`, shader, script, material, prefab and scene files in ordinary Git. Never ignore metadata or commit `Library`, `Temp`, `Logs` or `builds`.
- Keep the approved Sirenix import and its settings in the private repository too. Its DLLs, symbols, packaged demos and binary resources use Git LFS; preserve metadata and readable configuration in ordinary Git. Keep personal license keys and activation credentials out of source control.
- On a new workstation, install Git LFS, run `git lfs install`, clone, then `git lfs pull` before opening Unity. For this first import, include `.gitattributes`, the complete BK folder and its parent metadata alongside the integration changes; inspect `git lfs status` before committing.
- Keep vendor paths/GUIDs stable. Use material copies or prefab variants in `Assets/Content` for game tuning; apply publisher updates separately and check the local `art/pure-nature-mountains/README.md` patch note after reimporting.
- `Tools > Something Down There > Configure Pure Nature Environment` restores BK's environment manager (wind globals, all lighting overrides disabled) and the URP settings the pack shaders need. Validate excavation, restored terrain, shaders and the Windows player after updates.
- Keep demo scenes available as references; the build includes MainGame and its dependencies. Purchased surface shaders need explicit integration before use as underground finds with excavation darkness.
- Open `Assets/Scenes/MainGame.unity` to author the playable scene. It contains the excavation, stations and the generated Highlands lakebed (see below). Keep `MainGameRoot`'s player, camera, excavation, computer and save references intact.
- The original `Assets/BK` demo is available as a reference. Duplicate vendor assets into project-owned folders before editing shared data.
- Terrain and Terrain Physics modules remain available for manual terrain work. Keep new TerrainColliders outside the excavation opening so they cannot cap digging.
- `Tools > Something Down There > Build Windows Player` always builds MainGame regardless of the active Editor scene.

## Lakebed site

- `Tools > Something Down There > Configure Lakebed Excavation Site` regenerates `MainGameRoot/Environment`, `Surface/Excavation rim` and the camp placement from the unmodified Highlands demo (`LakebedSiteSetup`). It replaces earlier environment edits, so tune the recipe constants rather than hand-editing generated objects. It opens the demo additively; discard the vendor auto-upgrade diffs it leaves (URP asset, two particle materials).
- Generated assets live in `Assets/Content/Lakebed`; the binary `LakebedTerrain.asset` (~60 MB) is stored in Git LFS. Vendor scenery stays prefab instances with the demo's overrides.
- The plot outline lives in `SiteLayout`. After changing it or the grid size, rerun **Configure Excavation Size** and full site setup, then rebake occlusion.
- Lakebed dressing is tuned in `LakebedSiteSetup.Lakebed.cs`; rerun full site setup, then rebake occlusion. Project material copies in `Content/Lakebed` keep Inspector tuning across reruns; to regenerate one, delete it through the Project window with another scene open (never on disk while the Editor runs).
- **Tools > Something Down There > Refresh Lakebed Lighting and Water** reapplies presentation and saves MainGame without rebuilding terrain or object placement. Water materials and the color profile retain existing Inspector tuning. `LakebedSiteSetup.ConfigureWaterShader()` regenerates the project shader adaptation after a vendor update, with guarded source anchors.
- **Refresh Lakebed Performance** reapplies terrain draw settings, scenery caster overrides, distance-based LOD thresholds (from vendor values; `LakebedSiteSetup.DetailSwitchDistances`) and project tree variants/foliage materials while preserving terrain heights, paint and placements. It also prepares visibility volumes and clears stale occlusion. After this command, full site regeneration, or moving/removing canyon rocks, open **Window > Rendering > Occlusion Culling > Bake**, then save MainGame. Only solid permanent scenery is an occluder; never mark `Excavation`, its preview, terrain, foliage or water as occluders. Camera volumes include the full excavation depth. Baked data lives beside MainGame and is tracked with LFS.
- The built-in Unity Occlusion Culling module (`com.unity.modules.umbra@1.0.0`, supplied and licensed with the Editor) is required for that bake to work in Windows players; keep it in the package manifest.
- The terrain has a hole under the rim collar; keep any new TerrainCollider or scenery out of the dig plot's column. `MainGameSceneTests` checks the rim, terrain hole, column clearance, water, probe, play-area bounds and station support.
- The play area (walls + flight ceiling, Ignore Raycast layer) is generated from the drained-section outline. Setup then removes scenery and terrain trees that no viewpoint inside it can see; rerun setup after changing the outline, ceiling or window, otherwise newly visible gaps can appear.

### Editing the world manually

1. Stop Play Mode and open `Assets/Scenes/MainGame.unity`. `_Recovery/0.unity` is a protected older recovery snapshot, not the current level. `MainGameRoot` is a GameObject grouping the level and gameplay systems, not another scene; keep it and its references.
2. Expand `MainGameRoot/Environment`: `Lakebed terrain` is the surrounding Unity Terrain; `Cliffs`, `Peaks`, `Boulders`, `Trees`, `Water`, etc. contain ordinary authored objects. Select an object, press **F** in Scene view to frame it, then **W/E/R** to move/rotate/scale. Drag approved prefabs into these groups; scene instance overrides keep vendor source assets intact.
3. Select `Lakebed terrain`, then use its Inspector terrain tools to sculpt heights, paint textures, trees or plants. This edits the project-owned `LakebedTerrain.asset`. Preserve the hole below `Surface/Excavation rim`; surrounding terrain must not cover the dig opening.
4. `MainGameRoot/Excavation` owns the procedural voxel ground. Its chunks/finds are created for play, so the digging surface is not another landscape to sculpt with Unity Terrain tools. `Surface` contains the rim and camp stations; maintain their gameplay references and keep them outside the opening.
5. Water tuning: `Assets/Content/Lakebed/LakeWater.mat`, `RiverWater.mat` and `TrickleWater.mat`; keep the trickle in step with the lake and its render queue below the lake's. Lighting: `MainGameRoot/Sun`, `Daylight Colors` and `Environment/Lakebed reflections`; the shared profile is `Assets/Content/Environment/ReservoirPostProcess.asset`. Keep URP **Reflection Probes > Box Projection/Blending**, depth texture and opaque texture enabled. The probe uses a static vendor cubemap: moving scenery does not rebake it automatically.
6. Rebake occlusion after moving/removing canyon rocks, then save the scene with **Ctrl+S** and Play. Stop Play before editing: GameObject changes made during play disappear on exit, while edits to shared assets can persist. **Configure Lakebed Excavation Site** is a full regeneration and replaces manual environment/terrain edits; use the lighting/water or performance refresh for those settings alone. Build Windows Player always builds MainGame.

Unity reference: [reflection probe projection](https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/reflections/class-reflection-probe) requires the matching URP asset setting as well as the probe checkbox.

## Surface computer

- The user-selected `Assets/Cosmic_Retro_Computer_1_FREE` import supplies computer 3 for the combined sell/upgrade/refill station. Keep the vendor prefab/material/GUIDs; author standing scale and interaction bounds through `SurfaceStationSetup`. See [asset card](../art/retro-computer/README.md).
- `Tools > Something Down There > Configure Surface Computer` refreshes its visual/collider in MainGame. Save deliberate scene changes after running it.

## Unique recovery authoring

- `Tools > Something Down There > Sync Discovery Models` merges the common catalogs with `art/retro-computer/catalog.json`; `RetroComputerSetup` derives the centered computer 7 mesh, hull and prefab while preserving the trading computer and vendor import.
- `Tools > Something Down There > Configure Unique Recovery` wires the winch, receiving pad, exhibit stand, hook and player references in the open MainGame. It preserves the existing `SalvageWinchSettings` asset so tuning survives setup reruns. Original fixture sources live under `art/salvage-winch`.
- Tune hold/travel/clearance/search limits in `Assets/Content/Salvage/WinchSettings.asset`; item exposure, identity, lore and authored placement remain in the source catalog. `SalvageWinchValidator` checks the connected scene before builds.

Runtime folder ownership is documented in [docs/architecture.md](../docs/architecture.md).

## Worksite lamps and markings

- **L** previews a reusable work lamp; **M** previews/cycles a route symbol; **R** rotates. Primary places, secondary cancels, and Interact retrieves a lamp or erases an aimed mark. All actions appear in Controls.
- `Tools > Something Down There > Configure Work Lamps and Markings` refreshes the MainGame kit references and original Blender-derived assets under `Assets/Content/WorksiteTools`; the source recipe and license card live in `art/work-lamps`.
- The save format includes equipment ownership, physics and route marks with terrain. Older development checkpoints require New Game; tools never convert or silently replace them.
- Lanterns emit soft local light in every direction, retaining ground occlusion with sun shadows Off. Setup sizes the existing URP additional-light atlas for the kit and merges geometry by material; no extra render package is required.
