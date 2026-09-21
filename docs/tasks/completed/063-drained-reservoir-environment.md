# 063 — Drained Reservoir Environment

**Status:** complete — environment later removed in 067. Authored four terrain tiles from the approved pack (sediment dig ground, grassy camp terrace, rocky forested banks, distant peaks) around the worksite.

## Objective

Author a believable drained mountain-reservoir valley around the existing MainGame worksite using only
approved vendor assets (Pure Nature 2: Mountains). Viewed from inside the basin the composition must
read natural, not as an arena:

- **Center — diggable:** the 24 × 24 m voxel site reads as soft dry sediment / mud / gravel with
  sparse plants (new sediment triplanar material on the dig surface).
- **Camp side — non-diggable:** the south terrace (worksite yard + rims) stays firm turf with more
  grass, stones and camp debris on the same elevation as the dig.
- **Outer edges — non-diggable:** steep rocky reservoir banks cup the basin; dense conifer forest on
  the upper slopes; scattered boulders/stones in the foreground; valley opens north toward the
  future dam site and is closed by distant BK mountains.

No dam, tent, crate or stump assets exist in the project. The user approved skipping them; the far
valley end is left as a clear, correctly scaled location for a future dam.

## Concept Reference

`docs/concept/03_WORLD_AND_SITE.md` (surface is authored, not generated ugliness),
`docs/concept/09_FEEL_ART_AND_AUDIO.md` (high mountain daylight, vivid grass, purchased pack).

## Live Codebase Analysis

- `MainGameRoot` owns exactly one `TerrainVolume` (24 × 24 × 100 m, top at y = 0, `SiteLayout`),
  four flat rim boxes (`Surface/* rim`) using `GardenGround`, four visible 1.2 m perimeter wall
  boxes + airspace blockers (`Perimeter/*`, `PermanentTerrainBoundary`), bedrock walls, stations,
  player, `ExcavationDaylight` and `SurfaceGrassRenderer` (`coverage` is currently hardcoded 12%).
- `MainGameSceneTests` encodes the old "simple site" contract (no scenery, all non-ground renderers
  are URP Lit). It must be updated with this task; the core ownership/boundary assertions stay.
- `ExcavationDaylight` adapts every `Universal Render Pipeline/Lit` renderer under `MainGameRoot`;
  vendor shaders and terrain are untouched. Environment props must therefore keep vendor shaders.
- PlayMode terrain tests raycast from the pad and rely on spawn/return anchors being collider-free;
  no environment object may sit above the 24 × 24 opening or near `Surface/ReturnAnchor`.
- `SurfaceGrassIntegrationTests` builds its own fixture and does not set coverage, so a serialized
  `coverage` field must default to today's 12%.

## Architecture / Changes

New `Assets/Editor/ReservoirEnvironmentSetup.cs` (menu: `Tools > Something Down There > Build
Drained Reservoir Environment`) owns the entire build, idempotently:

1. **Terrain frame:** four `TerrainData` assets in `Assets/Content/Environment/` (N/S/E/W around
   the pad; inner edges at ±16 so no `TerrainCollider` can ever cap the dig opening). One global
   height function: flat worksite terrace at y≈0 blending to a basin floor at −1.35 m, meandering
   east/west banks rising ~30 m, south bank behind the camp, valley narrowing north toward the
   future dam site, cross-ridge closing the view. Uses the BK terrain layers (`Grass01`, `Gravel1`,
   `Mud01`, `Mud02`) with a procedural splat: mud sediment floor, gravel at slope toes/edges, grass
   on upper slopes and the south camp terrace.
2. **Dig surface material:** new `Assets/Content/Nature/ReservoirSediment.mat` using the existing
   `Something Down There/Ground Triplanar` shader with BK `Mud01`/`Gravel` textures (dry crust over
   soft mud). Assigned to `TerrainVolume.soilMaterial` and the edit-mode preview; rims keep
   `GardenGround` (firm camp yard).
3. **Scatter:** deterministic seeded placement of vendor prefabs under `MainGameRoot/Environment`:
   cliffs (Cliff1–8) on the banks, fir/pine/spruce clusters and bushes, boulders/stones/pebbles,
   grass/flowers/ferns near the camp and lip, branch litter, and Mountain1/2 as distant peaks.
   Prefab instances keep vendor links; all props stay clear of the 24 × 24 opening and the spawn.
4. **Boundary dressing:** perimeter wall renderers are hidden (colliders, airspace and
   `PermanentTerrainBoundary` components untouched) and a jittered, natural rubble line of stones
   and boulders sits on the boundary line, with gravel painted under it in the splat.
5. **Grass density:** `SurfaceGrassRenderer` gains a serialized `coverage` (default .12 = current
   behaviour); MainGame is set to sparse grass (~3%) over the sediment.
6. Camera far clip raised to 600 m so the banks and mountains read; no lighting or save changes.

## Edge Cases

- Never place scenery inside x,z ∈ [−20, 20] or over the dig column; no colliders near
  `Surface/ReturnAnchor`.
- Terrain inner edges stop at ±16; rim tops stay at y = 0 with a ~2 cm terrain lip to avoid
  z-fighting.
- Re-running the tool deletes only `MainGameRoot/Environment` and regenerates terrain assets.
- Old saves are untouched: environment is static scene content, no density or player state changes.

## Acceptance Criteria

1. `Tools > Something Down There > Build Drained Reservoir Environment` runs clean and is
   idempotent; MainGame saves with one `MainGameRoot/Environment` group.
2. From the spawn the player sees: sediment dig centered among flat basin sediment, a firmer grassy
   camp side, rocky banks with cliffs and forest, and mountains down the valley to the north.
3. No visible arena walls; the perimeter colliders and all `PermanentTerrainBoundary` counts are
   unchanged; the dig and boundary PlayMode tests pass unchanged.
4. `MainGameSceneTests` updated for the environment; EditMode and PlayMode suites are green.
5. `builds/windows/SomethingDownThere.exe` rebuilt and playable; screenshots reviewed.
6. Docs updated: concept surface/art notes, `docs/baseline.md`, asset card, `docs/tasks.md`.
