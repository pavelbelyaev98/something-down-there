# Architecture

- `unity/Assets/Runtime/Player` — input, movement, camera, battery, preferences, player state, wallet/trade/rescue, save control and session-only developer controls.
- `unity/Assets/Runtime/Interaction` — targeting, inventory identities, seeded discovery placement, reveal/collection and find handling/physics.
- `unity/Assets/Runtime/Terrain` — finite signed density field, smooth surface-net meshes, dig adapter, boundaries and derived surface grass/daylight.
- `unity/Assets/Runtime/UI` — HUD and menu behavior (UI Toolkit under `UI/Toolkit`).
- `unity/Assets/Runtime/Persistence` — versioned whole-world snapshots, atomic disk storage, recovery and autosave.
- `unity/Assets/Runtime/Validation` — disposable validation adapters, not production mechanics.
- `unity/Assets/Scenes`, `unity/Assets/Editor`, `unity/Assets/Tests` — scenes, editor tooling and repository-owned checks. `unity/Packages/manifest.json` is the dependency source of truth.
- `unity/Assets/Plugins/Sirenix` — approved Odin Inspector/Validator in Editor Only mode; `Assets/Editor/Validation/MainGameValidation.asset` owns game-content scan scope. Odin's build automation runs that profile before builds; game persistence remains independent.

- `MainGame.unity` owns one player/menu root and world camera. `MainGameRoot` owns excavation, permanent walking apron/boundaries and station anchors; there is no tool rig or alternate-cut system.
- `DesktopInstance` reserves one Windows player before scene load; duplicate launches focus the existing window and exit. `WorldSaveStore` separately locks the profile. Saves live in `Application.persistentDataPath/Save` (Editor uses `EditorSave`); only current-format v7 saves are supported, with atomic replacement and a retained previous checkpoint. Old content aliases, depth migration, fractional-money conversion and experiment fields are removed.
- `FpsPlayer` owns `CameraPreferences`, `InputPreferences` and `GamePreferences` (separate `Preferences/camera-v1.ini`, `input-v1.ini`, `game-v1.json`; `EditorPreferences` in the Editor). `GamePreferences` is the sole runtime display owner; `DesktopWindow` supplies native defaults.
- `FpsHud` owns the single UI Toolkit document with `GameHudView`/`GameMenuView`; one retained EventSystem routes menu input; no Canvas HUD.
- `TerrainVolume`/`ExcavationGrid` own removed volume and synchronously rebuild matching render/collision meshes; `ExcavationDaylight`, `SurfaceGrassRenderer` and `SunPresentationSetup` derive presentation without excavation authority and save no lighting state.
- Approved vendor content stays under `Assets/BK`; game material and texture overrides live under `Assets/Content/Nature`. `SurfaceGrassSetup` adapts the vendor grass shader with `SurfaceGrassSupport.hlsl` and wires meadow species into the existing renderer; `SurfaceGrassValidator` rejects missing meshes/materials or shaders without excavation clipping. BK's environment manager supplies wind globals while project systems retain lighting ownership.
- MainGame owns excavation and stations; `RoundSiteSetup` authors the circular opening and permanent gravel apron above the subsurface grid. `GroundTextureSetup` configures the shared meadow cap and pack soil throughout active terrain; custom soil assets remain inactive. `WindowsBuild` always builds MainGame.
- `DiscoveryCatalog`/`DiscoveryContentSetup`/`PhotoRockSetup`/`MineralSetup` keep stable item identities separate from replaceable prefab art; `DiscoveryField` owns seeded placement and `BuriedFind` transfers records. The merged catalog lives in `Content/Discoveries`; only current rock/mineral content IDs resolve.
- `SellStation`/`UpgradeStation`/`StationTrade` bind trades to inventory/wallet/battery revisions; `EquipmentProgression` owns authored capacity increments and prices.

Update this file only when folder ownership, major system boundaries or scene ownership changes.
