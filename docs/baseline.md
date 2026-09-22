# Prototype Baseline Implementation

> **Notice:** This document is a factual inventory of systems currently functioning in the Unity prototype. **None of this implementation is considered signed-off or final.** Everything is subject to refactoring, rebalancing, or replacement to align with the new concept in `docs/concept/`.

## 1. Player & Controls (`unity/Assets/Runtime/Player/`)
- **Controller:** First-person `CharacterController` with smooth crouch (Left Ctrl, 35% speed) and modest sprint (Left Shift, 1.35x speed).
- **Tool ladder:** authored once in `ShovelProfile.Defaults()` and no longer serialized into `MainGame`; Developer admin → **Tool tuning** nudges bite/cadence/reach live and prints a paste-ready table (`Logs/tuning.txt`).
- **Jetpack & Rescue:** Vertical thrust and hover mechanics. Automatic zero-fuel rescue if stranded.
- **Input & Comfort:** Unity Input System with full runtime action rebinding. Hold-to-dig by default with a persisted toggle option. Camera FOV slider (55–90°), crosshair toggle, and preferences persistence (`Preferences/*.ini`).

## 2. Terrain & Excavation (`unity/Assets/Runtime/Terrain/`)
- **Voxel Engine:** Finite signed density field (24 × 24 × 100 m, authored by `SiteLayout`) running synchronized surface-net meshing (0.125 m resolution) with collision generation. Chunks are materialized on demand: 7,200 possible keys, 144 built on a fresh site, so startup and load cost do not scale with depth.
- **Checkpoints:** dense density (~119 MB for the shipped site) validated and gzip-Fastest encoded on a worker thread, packed payload capped at 64 MB / 256 MB unpacked. Only the current format and excavation layout load; previous formats require a new game.
- **Digging:** Hold/toggle drives frequent, thin shaving cuts for controlled excavation, with sub-cell contact refinement and matching render/collision updates. Energy scales with cadence. Detached soil and local paper-thin strips clear within each cut; thicker useful ledges and support crowns remain. Collection preserves the cutting cadence in both shaving and scoop modes.
- **Admin Tools:** Session-only debug panel (`Ctrl+Shift+F10`) with **Shaving motion: ON/OFF** for comparing shallow cuts with organic scoops, shovel tier selection, refill, and buried find markers. Shaving defaults ON; Restore normal rules and loading a session restore it.

## 3. Finds & Physics (`unity/Assets/Runtime/Interaction/`)
- **Finds:** full-size plain rocks form a dense layer immediately beneath the turf, with the ore
  ladder (coal first) beginning beneath them and deeper bands shifting the mix toward value.
  Placement uses enclosing spheres around actual visual/collision vertices with shallow soil
  cover; banded placement picks a target depth before searching nearby lateral positions, so
  each band stays populated from its top. A separate lower-reservoir allocation continues the
  progression. Populations are authored in the source catalogs, apply to new games, and saved
  finds keep their positions. Model size and mass remain authored; retired content is deleted.
- **Detection & Pickup:** Aim-assisted reveal, 60% voxel exposure threshold, held aim pickup between digging ticks and immediately after a revealing cut. `FindProximityCollection` also collects clear finds within a close camera-centred area in front of the player during walking or held digging, at any height, with direct visibility and throw/drop exclusion. Released/falling finds have longer aimed reach; station/lifting reach stays separate. Aimed and nearby collection can share a held-input frame with its scheduled terrain cut; neither collection nor its animation adds a delay.
- **Release:** Nearly exposed finds with clear interiors and only shallow surface contact become dynamic; substantial burial still anchors them. Gravity and collision determine falling/settling.
- **Handling:** Physical lift/drop (RMB) and throw (LMB). Carried finds track motion and settle physically on release; a slow creep on a slope counts as quiet, so finds stop instead of rolling away forever.
- **Dense-world cost:** meshes enclosed by pristine soil stop rendering; conservative bounds and nearby terrain edits reactivate them before small fragments can be missed. Anchored physics callbacks sleep until a terrain change or explicit handling/restore; collision and save records remain intact.

## 4. Hub & Economy (`unity/Assets/Runtime/Player/`, `Runtime/Interaction/`)
- **Surface computer:** one Cosmic retro terminal opens selling for a carried haul, then upgrades immediately after Sell All or the last individual sale; an empty bag opens upgrades directly. The prompt reads simply `Use`, with no key prefix. Shovel, battery and bag progression still use `EquipmentProgression` and its shared `TierPrices`; parameters stay in code.
- **Refill Economy:** Paid battery recharge ($1 minimum, whole-dollar `$`).

## 5. UI & Presentation (`unity/Assets/Runtime/UI/`)
- **UI Toolkit:** Single UI Document (`FpsHud`) driving the HUD, Pause menu, Settings tabs, and Station trading interfaces with unified grayscale styling.
- **Capacity warning:** Full inventory uses the same persistent resource banner as low fuel; simultaneous warnings stack above the bottom edge, without overlapping feedback.
- **Focus loss:** the game still pauses when the window loses focus, but the dim overlay and pause card are hidden while focus is elsewhere, so external screenshot tools capture the game rather than the pause screen.
- **Frame pacing:** startup is capped at 144 FPS before the scene loads; device settings then apply the saved frame limit or VSync choice. Display reset also defaults to 144 FPS.
- **Resolution:** new/default graphics render at 100%; existing saved preferences remain valid. The display list retains all supported monitor modes, including 4K and higher even when the desktop currently uses a lower resolution, with timed Keep/Revert confirmation.
- **Graphics settings:** render resolution, shadows (Off/Low/Medium/High), MSAA, texture mip quality and anisotropic filtering apply immediately and persist as device preferences. Graphics reset is enabled; shadows adjust only the runtime URP clone and retain the independent excavation daylight field. High restores authored shadows; old profiles without a shadow choice use High.
- **Computer screens:** selling and upgrades share the existing fixed-size parts-board table (`Station.uss` + `ToolkitStationRows`) — money-only header, categories in their own columns, one clickable row per upgrade track, refill service or carried find. One click buys; nothing is selected first and nothing resizes.
- **Round worksite:** `RoundSiteSetup` authors a circular meadow opening and permanent dry-gravel
  walking apron with the shared computer. Valley scenery and generated terrain are removed.
  The voxel grid extends beneath the apron for lateral digging.
- **Meadow and soil:** eleven pack grass/flower/fern layers, seeded in change-driven instanced
  batches. Root support removes uprooted plants; a change-driven surface-density mask clips
  wind-displaced foliage over openings without clearing intact neighbours. The project grass
   shader also clips the round perimeter. Pack turf uses continuous top projection and a soft,
   textured soil transition; noon shadow bias prevents a tessellated self-shadow rim. Pack soil covers the active
   terrain; custom soil art/materials remain stored but unbound. Linear masks,
  preserved alpha, normal-map imports and the dry smoothness cap prevent white glare.
- **Presentation:** approved pack sky and almost overhead midday sunlight; custom grass, clouds,
  sun and trial-tool art/imports are deleted. No first-person rig is present; the shaving/scoop
  comparison lives in development admin. The full licensed vendor pack remains available.
  WindowsBuild always targets MainGame.
- **Underground lighting:** `ExcavationDaylight` derives daylight from connected excavated air, with a generous early reach, stronger loss along sideways passages and no ambient brightness floor. Soil and adapted URP Lit finds/boundaries attenuate sun, sky fill and reflections together; sustained descents and long covered branches become near-black while local lights remain effective.

## 6. Persistence & Lifecycle (`unity/Assets/Runtime/Persistence/`)
- **Saving:** Current-format v7 whole-world snapshots (`WorldSaveController`), atomic disk write, 10 s background autosave, recovery from interruptions.
- **Windows Process:** Single-instance reservation (`DesktopInstance`) focusing existing window on relaunch.
