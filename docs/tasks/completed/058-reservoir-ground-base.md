# 058 — Reservoir ground base

> Superseded by task 062: rejected scenery, project preview assets and assembly tools removed; the user now authors the environment manually.

## Objective and concept
Replace the rejected rock enclosure with a spacious, ground-only drained reservoir. Use the
approved Mountain_Demo terrain as the source for surface relief, gravel/mud/grass layers and
grass varieties. Keep broad exposed sediment, irregular lighter-green growth and low uneven
banks. No placed trees, rocks, mountains, debris, water or excavation marker ring in this pass.
References: concept 03 (site), 09 (visual style), and the user's reservoir screenshots.

## Existing ownership and changes
- MainGame owns TerrainVolume, its saved excavation grid, surface stations and permanent walls.
  Preserve their dimensions, coordinates and save format; enlarge the surrounding walkable floor.
- Replace ReservoirEnvironmentSetup's prefab arrangement with an editor-authored Unity Terrain
  using a cropped/rescaled demo height field, softened basin relief and an excavation opening.
  Store the independent TerrainData and copied layers under Content/Nature; keep BK untouched.
- Blend approved demo sediment, gravel and grass across the new terrain and the existing voxel
  cap through a shared control map. Preserve the existing underground shader and daylight.
- Extend SurfaceGrassRenderer's existing excavation-aware instancing with optional painted
  coverage and several grass meshes/materials. Derive outside detail placement from Terrain;
  retain deterministic local cut/restore behavior inside the excavation.
- Keep the safety perimeter beyond the enlarged basin; remove the old close invisible walls.
  Preserve buried side/floor boundaries and the return/recharge station access.
- Keep the result editable with normal Terrain sculpt/paint tools; rebuilding is explicit.

## Edge cases and verification
- Terrain holes must never cap an existing excavation or leave a collision seam at its edge.
- Grass roots follow actual terrain height and remain absent from bare sediment and cuts.
- No vendor demo managers, cameras, lighting, tree instances or water enter MainGame.
- Save-free play validation only; do not open user saves or modify the recovery scene.
- Retain shared materials, spatial culling and inexpensive instancing for the enlarged view.
- Unity's built-in Terrain and Terrain Physics modules must be enabled, otherwise Play Mode
  strips those components. Keep heightmap instancing off for the observed Unity 6.6 depth-priming
  conflict at terrain holes; foliage continues to use instancing and the height field uses LODs.

## Acceptance
- [x] Rejected scenery removed; broad irregular ground with patchy lighter varied grasses.
- [x] Demo-derived layer palette and height variation visibly inspected from player and overhead views.
- [x] Ground/collision join and station access verified; digging and grass restoration pass checks.
- [x] Existing saves/grid remain compatible; no original vendor assets modified.
- [x] Compilation and relevant tests pass; fresh Windows player built.
- [x] Concept, baseline, architecture and local asset guidance updated; spec archived.

## Validation
EditMode 230/230; grass integration 1/1, excavation daylight 2/2 and terrain integration 21/21.
Save-free MainGame play verified walking in four directions beyond the former enclosure,
station footing, collision at the terrain opening, and a visible excavation with supported grass.
Player, overhead and terrain/voxel material comparisons inspected; evidence is in ignored
`Logs/Task058/`. Vendor content, recovery scene, saved grid and player profiles remain untouched.
Windows development player rebuilt successfully with no code/shader errors or warnings. The
build's sole tooling warning reports the intentionally absent player-side Pipeline configuration.
Visual direction remains a first ground pass for playtest; no new native FPS claim is made.
