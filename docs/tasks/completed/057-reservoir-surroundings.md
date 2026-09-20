# 057 — Drained reservoir surroundings

> Superseded by task 062: rejected scenery, project preview assets and assembly tools removed; the user now authors the environment manually.

## Objective
Frame the existing excavation with a composed mountain reservoir environment using the
approved Pure Nature pack: dry rocky banks, a former waterline, conifers above it, a narrow
inaccessible gorge, a substantial rocky back boundary and distant peaks. Keep the working
floor open. No water or dam; ground replacement and gameplay redesign are outside this pass.

## Concept reference
- `03_WORLD_AND_SITE.md`: finite drained reservoir, clear permanent boundaries.
- `07_SURFACE_HUB_AND_DISPLAY.md`: legible and reachable surface services.
- `09_FEEL_ART_AND_AUDIO.md`: stylized licensed art, bright surface and true dark tunnels.
- User references: layered cliff/forest silhouettes and mountain depth; dry basin composition.

## Existing code and ownership
- MainGame owns the excavation, four permanent walking rims, surface stations and eight
  perimeter blockers. SiteLayout and saved terrain positions must remain unchanged.
- SurfaceGrassRenderer already uses BK grass and derives support from excavated soil.
- SunPresentationSetup owns sunlight, sky and color grading; the BK manager supplies wind only.
- Vendor cliffs/trees already have LODs; mountain meshes are inexpensive background props.

## Implementation
- Add editor-only ReservoirEnvironmentSetup, following the existing scene setup tools.
  Rebuild only its named Reservoir Surroundings subtree; author placements in C#, save normal
  scene/prefab references and retain publisher metadata. No runtime spawning or new world manager.
- Compose banks, rockfall gorge, larger back wall, clustered forest, sparse rim debris and
  distant mountain layers. Mark the excavation edge with low stones outside the voxel bounds.
- Hide the old perimeter renderers within the new natural edge while retaining safety ownership.
  Move safety faces behind the bank geometry and extend permanent rim support outward beneath it;
  the excavation footprint and inner rim edge remain unchanged. Near scenery gets matching collision
  and PermanentTerrainBoundary; distant scenery has none. Bake the seven rock imports' triangle collision.
- Store game materials under Content/Nature. A small URP rock shader adds a world-height
  mineral stain to the purchased rock textures; vendor shaders remain untouched.
- Extend the existing sky with the pack's cloud noise and tune daylight through its current owner.
- Keep vendor LODs, shared materials and instancing; disable distant shadow casting and expensive
  scenery lighting features. Inspect actual rendered cost before accepting the composition.
- Update the existing scene invariant test for approved scenery/materials; no cosmetic test suite.

## Edge cases
- Scenery must not overlap diggable soil, block station/recharge access, float after a terrain
  reset or consume save data. Keep user recovery scenes and save profiles untouched.
- No second camera, terrain volume, sun, wind manager or audio source; no imported demo root.
- Gorge stays a scenic view with a visible rockfall closing access. Safety faces must sit behind
  visible rock at normal player height. Keep an open sky over the excavation for jetpack returns.
- Local scenery haze must not brighten underground darkness or add global fog.

## Acceptance
- [x] Four surface directions are enclosed by coherent rock/forest layers, with a distinct gorge,
  back wall and distant mountains visible from player height; no water or dam.
- [x] Digging footprint, grass clearing, stations, return anchor and saved world positions work.
- [x] No missing materials/scripts or shader/C# compile diagnostics.
- [x] Live visual review and measured scenery cost at fixed views, with relevant existing tests.
- [x] Fresh Windows player; baseline, concept, architecture and local asset card updated.

## Validation
- 230 Edit Mode and 24 relevant Play Mode checks passed. The scene invariant check passed again
  after collision baking and permanent rim support were finalized.
- Save-free Play Mode review verified walking to visible rock, clear station/return approaches,
  real excavation and grass removal. All 72 radial probes met visible geometry before safety faces.
- Native tuning comparison at 2560 x 1440 on RX 9060 XT / Ryzen 7 9700X: full scenery median
  frame time 3.89–4.87 ms across four views; mountains added less than 0.1 ms. This is a fixed-view
  comparison on this machine, not a minimum-hardware guarantee. Captures and reports stay in Logs/Task057.
- The fixture rejects hidden/no-render measurements, reports unavailable draw counters honestly,
  and waits for a focused preview. It never owns a player save.
- Final Windows build succeeded with zero errors. The one package notice says the player-side CLI
  server is disabled intentionally. Collision pre-bake and shader/compiler diagnostics are clean.
