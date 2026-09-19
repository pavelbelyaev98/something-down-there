# Architecture

- `unity/Assets/Runtime/Player` — input, movement, camera, battery, preferences, player state, wallet/trade/rescue, save control and session-only admin experiments.
- `unity/Assets/Runtime/Interaction` — targeting, inventory identities, seeded discovery placement, reveal/collection and find handling/physics.
- `unity/Assets/Runtime/Terrain` — finite signed density field, smooth surface-net meshes, dig adapter, boundaries and derived surface grass/daylight.
- `unity/Assets/Runtime/UI` — HUD and menu behavior (UI Toolkit under `UI/Toolkit`).
- `unity/Assets/Runtime/Persistence` — versioned whole-world snapshots, atomic disk storage, recovery and autosave.
- `unity/Assets/Runtime/Validation` — disposable validation adapters, not production mechanics.
- `unity/Assets/Scenes`, `unity/Assets/Editor`, `unity/Assets/Tests` — scenes, editor tooling and repository-owned checks. `unity/Packages/manifest.json` is the dependency source of truth.

- `MainGame.unity` owns one player/menu root and its world camera. The admin experiment loads `Content/Excavator/Resources/ExperimentalExcavator.prefab` behind session opt-in; normal and reloaded play always uses Scoop. `MainGameRoot` owns excavation, static surface/boundaries and station anchors.
- `DesktopInstance` reserves one Windows player before scene load; duplicate launches focus the existing window and exit. `WorldSaveStore` separately locks the profile. Saves live in `Application.persistentDataPath/Save` (Editor uses `EditorSave`); versioned v1–v6 readers stay supported with atomic replacement and a retained previous checkpoint.
- `FpsPlayer` owns `CameraPreferences`, `InputPreferences` and `GamePreferences` (separate `Preferences/camera-v1.ini`, `input-v1.ini`, `game-v1.json`; `EditorPreferences` in the Editor). `GamePreferences` is the sole runtime display owner; `DesktopWindow` supplies native defaults.
- `FpsHud` owns the single UI Toolkit document with `GameHudView`/`GameMenuView`; one retained EventSystem routes menu input; no Canvas HUD.
- `TerrainVolume`/`ExcavationGrid` own removed volume and synchronously rebuild matching render/collision meshes; `ExcavationDaylight`, `SurfaceGrassRenderer` and `SunPresentationSetup` derive presentation without excavation authority and save no lighting state.
- Approved vendor content stays under `Assets/BK`; game material overrides live under `Assets/Content/Nature`. `SurfaceGrassSetup` wires purchased grass into the existing renderer; BK's environment manager supplies wind globals while project systems retain lighting ownership.
- `DiscoveryCatalog`/`StarterFindSetup`/`PhotoRockSetup` keep stable item identities separate from replaceable prefab art; `DiscoveryField` owns seeded placement and `BuriedFind` transfers records.
- `SellStation`/`UpgradeStation`/`StationTrade` bind trades to inventory/wallet/battery revisions; `EquipmentProgression` owns authored capacity increments and prices.

Update this file only when folder ownership, major system boundaries or scene ownership changes.
