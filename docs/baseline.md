# Prototype Baseline Implementation

> **Notice:** This document is a factual inventory of systems currently functioning in the Unity prototype. **None of this implementation is considered signed-off or final.** Everything is subject to refactoring, rebalancing, or replacement to align with the new concept in `docs/concept/`.

## 1. Player & Controls (`unity/Assets/Runtime/Player/`)
- **Controller:** First-person `CharacterController` with smooth crouch (Left Ctrl, 35% speed) and modest sprint (Left Shift, 1.35x speed).
- **Tool ladder:** authored once in `ShovelProfile.Defaults()` and no longer serialized into `MainGame`; Developer admin → **Tool tuning** nudges bite/cadence/reach live and prints a paste-ready table (`Logs/tuning.txt`).
- **Jetpack & Rescue:** Vertical thrust and hover mechanics. Automatic zero-fuel rescue if stranded.
- **Input & Comfort:** Unity Input System with full runtime action rebinding. Camera FOV slider (55–90°), crosshair toggle, and preferences persistence (`Preferences/*.ini`).

## 2. Terrain & Excavation (`unity/Assets/Runtime/Terrain/`)
- **Voxel Engine:** Finite signed density field (24 × 24 × 100 m since 005, authored by `SiteLayout`) running synchronized surface-net meshing (0.125 m resolution) with collision generation. Chunks are materialized on demand: 7,200 possible keys, 144 built on a fresh site, so startup and load cost do not scale with depth.
- **Checkpoints:** dense density (~119 MB for the shipped site) validated and gzip-Fastest encoded on a worker thread, packed payload capped at 64 MB / 256 MB unpacked. A shallower checkpoint (12 m, 32 m) deepens in place on load: the dug hole and every find keep their world position.
- **Digging:** Spherical scoop cuts, organic cut variation, and detached soil cleanup within the stroke.
- **Admin Tools:** Session-only debug panel (`Ctrl+Shift+F10`) with shovel tier selection, refill, and buried find markers.

## 3. Finds & Physics (`unity/Assets/Runtime/Interaction/`)
- **Finds:** full-size plain rocks form a dense layer immediately beneath the turf; first shallow
  scrapes reveal nearby pieces. Placement uses enclosing spheres around actual visual/collision
  vertices, with shallow soil cover and population authored in the source catalogs. The ore ladder
  begins beneath the rocks. The first few metres contain a dense continuation of full-size
  rocks with coal entering early; later bands gradually change the mix. Banded placement picks
  a target depth before searching nearby lateral positions, avoiding an empty top of each band.
  Buried envelopes use a smaller soil gap while the accepted turf layout stays intact.
  Progression bands continue into a separate deep
  allocation across the lower reservoir, authored in the same catalogs. Model size and mass remain authored; retired bottles stay
  resolvable for old saves. Population tuning applies to new games; saved finds retain their positions.
- **Detection & Pickup:** Aim-assisted reveal, 60% voxel exposure threshold for collection, held aim instant pickup.
- **Handling:** Physical lift/drop (RMB) and throw (LMB). Carried finds track motion and settle physically on release; a slow creep on a slope counts as quiet, so finds stop instead of rolling away forever.
- **Dense-world cost:** meshes enclosed by pristine soil stop rendering; conservative bounds and nearby terrain edits reactivate them before small fragments can be missed. Anchored physics callbacks sleep until a terrain change or explicit handling/restore; collision and save records remain intact.

## 4. Hub & Economy (`unity/Assets/Runtime/Player/`, `Runtime/Interaction/`)
- **Surface Stations:** Sell Station (instant trade) and Upgrade Station (shovel, battery capacity 100–400, bag capacity 10–40 slots). Every track shares one tier price ladder (`EquipmentProgression.TierPrices` = 10/25/55/100/180, authored in code and never baked into the scene); the shovel runs one tier deeper than the bag and tank.
- **Refill Economy:** Paid battery recharge ($1 minimum, whole-dollar `$`).

## 5. UI & Presentation (`unity/Assets/Runtime/UI/`)
- **UI Toolkit:** Single UI Document (`FpsHud`) driving the HUD, Pause menu, Settings tabs, and Station trading interfaces with unified grayscale styling.
- **Focus loss:** the game still pauses when the window loses focus, but the dim overlay and pause card are hidden while focus is elsewhere, so external screenshot tools capture the game rather than the pause screen.
- **Frame pacing:** startup is capped at 144 FPS before the scene loads; device settings then apply the saved frame limit or VSync choice. Display reset also defaults to 144 FPS.
- **Resolution:** new/default graphics render at 100%; existing saved preferences remain valid. The display list retains all supported monitor modes, including 4K and higher even when the desktop currently uses a lower resolution, with timed Keep/Revert confirmation.
- **Graphics settings:** render resolution, shadows (Off/Low/Medium/High), MSAA, texture mip quality and anisotropic filtering apply immediately and persist as device preferences. Graphics reset is enabled; shadows adjust only the runtime URP clone and retain the independent excavation daylight field. High restores authored shadows; old profiles without a shadow choice use High.
- **Station machines:** Workshop and Sell All are one fixed-size parts-board table (`Station.uss` + `ToolkitStationRows`) — money-only header, categories in their own columns, one clickable row per upgrade track, refill service or carried find. One click buys; nothing is selected first and nothing resizes.
- **Simple surface:** MainGame is restored to the pre-environment excavation, permanent rim/boundaries, original spawn and station models. Existing menus, movement, digging, grass and saves remain. The user will author the surroundings manually; all attempted reservoir scenery, preview assets and assembly tools are removed.
- **Presentation:** original turf/soil material, purchased grass and clear noon sky are restored. The full purchased pack and vendor demo remain available for manual design. WindowsBuild always targets MainGame.
- **Underground lighting:** `ExcavationDaylight` derives daylight from connected excavated air, with a generous early reach, stronger loss along sideways passages and no ambient brightness floor. Soil and adapted URP Lit finds/boundaries attenuate sun, sky fill and reflections together; sustained descents and long covered branches become near-black while local lights remain effective.

## 6. Persistence & Lifecycle (`unity/Assets/Runtime/Persistence/`)
- **Saving:** Versioned whole-world snapshots (`WorldSaveController`), atomic disk write, 10 s background autosave, recovery from interruptions.
- **Windows Process:** Single-instance reservation (`DesktopInstance`) focusing existing window on relaunch.
