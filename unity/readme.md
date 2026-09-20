# Unity developer guide

Unity is pinned to `6000.6.0f1` with URP `17.6.0`. Runtime work belongs in `Assets/`; generated evidence stays under ignored `Logs/`.

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
- Development admin: **Ctrl+Shift+F10** opens the panel; hold Ctrl+Shift with **1-6** to select a shovel, **R** refill, **X** buried-find markers, **Home** return. Session-only; no launcher or launch flag.
- Tests with the Editor closed: `./tools/test-fps.ps1`, `./tools/test-terrain.ps1` (isolated copy). With the Editor open: `./tools/test-changed.ps1` runs the EditMode assembly plus only the PlayMode classes that own the changed files (`-Full` for everything, `-List` to print the plan, `-Path <file>` to scope it by hand). For a manual run: `unity command run_tests --mode editor --filter SomethingDownThere.EditModeTests --filter_type assembly --async_tests true --project-path "$projectPath" --format json`, then poll `test_status`; repeat with `--mode playmode --filter SomethingDownThere.PlayModeTests`. Save scene edits first.
- HUD/menu inspection: `capture_game_view --source screen` in Play Mode (`screenshot` renders the camera and omits overlay UI). Save captures under `Logs/`, never in game assets.
- Saving fixture: **Tools > Something Down There > Validation > Build Save Performance Player** builds `builds/validation/saving/SavePerformance.exe`; `-saveProfileSeconds 5` is a smoke check, not a qualification run.
- Native Windows reviews share the user's desktop: announce input control, verify game focus, and repeat checks interrupted by user input. See [AGENTS.md](../AGENTS.md).

## Blender MCP

The Blender Lab `MCP` extension 1.0.0 runs in Blender 5.2 on `127.0.0.1:9876`. Its official stdio bridge lives in the isolated, ignored `.tools/blender-mcp/` environment: `blender-mcp` 1.0.0 pinned to commit `4309a39646e644261624bfcd2bca669b343b7621`, MCP SDK 1.30.0 (`<2`). No art is created by setup.

- Run `./tools/setup-blender-mcp-for-codex.ps1 -Install` to reproduce the installation (omit `-Install` to register an existing one); `tools/blender-mcp-server.cmd` launches the same executable. Keep the Blender add-on running and restart the Codex extension after registration. [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).
- Removal: close clients, delete `.tools/blender-mcp/` (excluded by `.gitignore`), and remove only `[mcp_servers.blender]` from `C:/Users/pavel/.codex/config.toml` (`codex mcp remove blender`) and `servers.blender` from `.vscode/mcp.json`. The user's pre-existing Blender add-on was used, not modified. Verification evidence: `Logs/Task19/`.

## Purchased assets and Git

- Keep the complete approved `Assets/BK` import and `Assets/BK.meta` in the private project repository for reproducible clones and future use. The initial import is approximately 1.65 GB; account for Git LFS storage/transfer quotas before pushing.
- `.gitattributes` sends BK textures, models and binary terrain/lighting data to Git LFS. Keep all `.meta`, shader, script, material, prefab and scene files in ordinary Git. Never ignore metadata or commit `Library`, `Temp`, `Logs` or `builds`.
- On a new workstation, install Git LFS, run `git lfs install`, clone, then `git lfs pull` before opening Unity. For this first import, include `.gitattributes`, the complete BK folder and its parent metadata alongside the integration changes; inspect `git lfs status` before committing.
- Keep vendor paths/GUIDs stable. Use material copies or prefab variants in `Assets/Content` for game tuning; apply publisher updates separately and check the local `art/pure-nature-mountains/README.md` patch note after reimporting.
- `Tools > Something Down There > Configure Approved Surface Grass` rebinds grass and required URP settings. The existing renderer owns soil support/culling; BK's environment manager owns wind globals with all lighting overrides disabled. Validate excavation, restored terrain, shaders and the Windows player after updates.
- Keep demo scenes available as references; the build includes MainGame and its dependencies. Purchased surface shaders need explicit integration before use as underground finds with excavation darkness.
- Open `Assets/Scenes/MainGame.unity` to author the playable scene. It contains the simple excavation and stations; the user now designs the surroundings manually. Keep `MainGameRoot`'s player, camera, excavation, station and save references intact.
- The rejected reservoir generation/copy tools, scenery, terrain data and project preview scene are removed. The original `Assets/BK` demo is still available as a reference. Duplicate vendor assets into project-owned folders before editing shared data.
- Terrain and Terrain Physics modules remain available for manual terrain work. Keep new TerrainColliders outside the excavation opening so they cannot cap digging.
- `Tools > Something Down There > Build Windows Player` always builds MainGame regardless of the active Editor scene.

## Source map

- `Assets/Runtime/Player` — movement, input, camera, battery, preferences, wallet, save.
- `Assets/Runtime/Interaction` — targeting, inventory, find placement and handling.
- `Assets/Runtime/Terrain` — excavation grid, mesh, collision, boundaries, surface presentation.
- `Assets/Runtime/UI` — HUD and menus (UI Toolkit).
- `Assets/Runtime/Persistence` — snapshots, storage, recovery.
- `Assets/Runtime/Validation` — temporary fixtures only.
- `Assets/Editor`, `Assets/Tests` — scene/build tooling and repository-owned checks.
