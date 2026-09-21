# 066 — Meadow excavation and soil comparison

**Status:** complete. Replaced active custom turf with a pack meadow of eleven instanced grass/flower/fern layers following excavation; the custom-versus-pack soil split was later unified to pack soil in 070.

## Objective and concept
Match the supplied meadow reference across the whole diggable site using approved pack
grass, flowers and ferns. Retire the custom turf surface from active use while retaining
custom soil for a half-site comparison against pack mud, exposed by digging.
Reference: `concept/09_FEEL_ART_AND_AUDIO.md`, visual style; task 065's dry ground response.

## Code analysis
- `TerrainVolume` creates chunks using one shared material and owns all digging/save state.
- `GroundTriplanar` already blends soil with a shallow turf cap and handles both mask layouts.
  It currently uses one layout for both layers; mixed custom soil and pack turf need separate decoding.
- `SurfaceGrassRenderer` instances one grass mesh and removes unsupported clumps after cuts,
  restoration and reset. Extend these batches for multiple species rather than spawning objects.
- `SurfaceGrassSetup` configures a stretched single grass card; reservoir setup reduces its coverage.
- The approved pack supplies the exact meadow surface and plant meshes used by the surroundings.

## Architecture and changes
- Keep one terrain/grid/material owner. Add an optional world-anchored soil comparison to
  `GroundTriplanar`, disabled by default, with explicit-gradient sampling across the split.
- Both halves share pack turf; custom soil is on the workbench-facing left, pack soil on the right.
  Preserve the original soil's stone mask and the pack's smoothness-alpha decoding independently.
- Keep matte lighting, normals, shadows, daylight attenuation and existing terrain geometry.
- Extend `SurfaceGrassRenderer` with serialized species layers, independent mesh footprints,
  seeded patch variation and per-species instanced batches. Retain the single-mesh fallback.
- Update `SurfaceGrassSetup` to author the meadow species at natural proportions, reusing
  approved meshes and project-owned material copies. No plant colliders or save records.
- Extend existing ground setup to bind pack turf on the camp and excavation, configure the
  comparison, and retain it through a reservoir rebuild without rebuilding the environment now.
- Stop referencing custom turf in active ground materials; retain source assets for reversibility.

## Edge cases
- Fresh chunks, old saves and deeper walls keep the same half-site mapping.
- Turf uses the same scale, colour and mask interpretation across the comparison seam.
- Plants disappear around a cut, never float over holes, and return deterministically on reset.
- Culling includes full plant height/wind and batches remain within the vendor instance limit.
- Preserve the valley, stations, user recovery scene and player saves.

## Acceptance
- Live surface views show a full meadow with mixed short grass, white/yellow/pink flowers and ferns.
- Matching cuts clearly show both soil options, including a cut crossing the middle.
- No crystal glare, stretched grass cards or custom turf in active ground materials.
- Existing scene, daylight and grass checks pass; extend grass lifecycle coverage to multiple species.
- C# and shader compilation clean; fresh Windows player built and verified.
- Update concept/baseline/local asset cards, archive spec and record completion.

## Verification
- 231/231 EditMode checks, 2/2 grass lifecycle checks and 2/2 daylight checks passed.
- Final Play Mode views verified meadow clearings, both soil options and a cut across the split;
  six real scoops removed vegetation with the soil. Evidence: `Logs/Task066/`.
- Final meadow uses 687 supported clumps in eleven instanced batches; observed submission
  cost was about a quarter millisecond in the Editor (not a minimum-hardware benchmark).
- C# compilation and ground shader are clean. Windows build succeeded with zero errors;
  the two existing build warnings concern vendor collision pre-baking and disabled player Pipeline.
- Review used additive MainGame without a save session; the Editor is back in MainGame.
