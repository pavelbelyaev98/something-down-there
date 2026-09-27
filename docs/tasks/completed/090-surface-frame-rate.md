# 090 — Surface Frame Rate

**Status:** complete. Buried finds stay hidden under their placement envelope, contact shading runs after opaques at half resolution without a depth prepass, the dig-ground shader samples only weighted projections, and unchanged find records are reused by autosave; the environment keeps its full detail.

## Objective
Raise frame rate outside the hole (walking, flying, looking across the site) without making the environment blurrier or hiding grass and scenery, and remove avoidable cost while digging. The user asked about baked shadows for static scenery, fewer shallow rocks and a rarer autosave if it helps.

## Concept reference
- `05`: dense buried layers must not cost frame rate; meshes enclosed by untouched soil are hidden. Playtest direction: the shallow rock layer stays dense but not crowded (thinned by about a sixth in the catalog).
- `09`/`03`: canyon silhouettes, grass, water and nearby shadows keep their look; distant detail may only change where it is not seen.
- `14`: stable frame pacing while digging and zero perceptible checkpoint hitch.

## Analysis (native, RX 9060 XT, 2560×1440, 2× MSAA, uncapped development player)
- GPU-bound: ~10 ms GPU at the dig site and in flight against ~2.4 ms main thread; sun shadows cost only a fraction.
- **Buried finds rendered:** 317 fully buried shallow rocks (12.5k triangles each) rendered on an undug site. Renderer bounds box the rotated local box and poked 1–5 cm above the surface, so `TerrainVolume.MayExpose` treated them as possibly exposed. Placement already guarantees their vertex sphere sits 2–6 cm under the turf.
- **Depth prepass:** before-opaque SSAO requests depth before the opaque pass, which forces URP to draw every renderer twice (7M extra triangles at the dig site). Depth priming is off, so the prepass bought nothing else.
- **Ground shader:** the soil path sampled all three triplanar projections even where two carry exactly zero weight (flat plot, straight walls).
- **Autosave:** the terrain capture is a copy-on-write page table, but rebuilding ~12k find records took ~6 ms of main thread per checkpoint (Editor).
- Per cut: ~3 ms main thread in the Editor (grid, meshing, collision, find refresh), hidden behind GPU-bound frames.

## Changes
- `BuriedFind.SoilVisibilityBounds`: renderer bounds intersected with the pivot sphere through the mesh's actual vertices (cached per shared mesh; unreadable meshes keep renderer bounds). Both contain the whole mesh, so the test stays conservative, and the one-sample halo in `AnyModified` is unchanged.
- Contact shading (`GroundTextureSetup`): After Opaque + Downsample. `NatureEnvironmentSetup` copies depth after opaques so transparent water never depends on a prepass. URP now prefilters away the unused per-material SSAO keyword.
- `GroundTriplanar.shader`: soil and deposit layers branch around ineligible or zero-weight projections; output is pixel-identical.
- `BuriedFind.CaptureCheckpoint` (autosave only) reuses its immutable record until `Transform.hasChanged`, state, item or release changes; `Restore` clears it; `DiscoveryField.CaptureCheckpoint` invalidates all records if the terrain moves. Public `Capture()` returns fresh records. Autosave interval stays 10 s.
- Catalog: fewer shallow rocks, with the same number removed from the total so they do not move deeper.
- Validation: `SurfacePerformanceFixture` gains `--environment-frame`, `--environment-pit` (real cuts, then a pit view) and `--environment-defaults`. The fixture applies the measured preference tier through the shared `UnityGameSettingsPlatform.ApplySunShadows`. Previously its baseline always had sun shadows on after rebinding preferences.

## Results (GPU median ms; before → after)
| View | Sun shadows at the fixture's former tier (1024, 1 cascade) | High | Off (after) |
|---|---|---|---|
| Dig site | 10.08 → 6.82 | 10.17 → 6.97 | 6.37 |
| Shore | 7.43 → 6.52 | 7.55 → 6.63 | 6.17 |
| Flight (15 m) | 10.08 → 8.04 | 10.30 → 8.20 | 7.35 |
| Dug pit | 7.46 → 5.08 (after the find fix only → final) | — | 4.35 |

Dig site: find visibility −0.8 ms (18.4M → 8.1M triangles), contact shading −1.0 ms (→ 4.5M), ground shader −1.5 ms. Checkpoint capture ~6.5 → ~1.3 ms (Editor). `ssao_previous` is timing-only in current builds: its keyword variants are stripped.

## Rejected or deferred (measured)
- **Baked shadows / lightmaps:** High sun shadows cost ~0.6–0.85 ms at the dig site and in flight. Baking a 1 km terrain and ~5,700 static renderers needs vendor lightmap UVs and probe lighting for LODs/terrain trees. It would also create a baked-versus-realtime seam at the dig collar, while the excavation still needs realtime sun. Not worth it.
- **Terrain basemap 150 → 75 m** (−0.5–0.8 ms): visibly blurrier ground (512² basemap). **Grass distance/density** (−0.6–1.0 ms): hides grass in view. **Merging terrain layers:** all nine are painted near the play area. Rejected to keep the environment intact.
- **Water** (~0.7–1.0 ms, procedural noise and refraction): part of the accepted look. **Far plane 1500 m, HDR off, bloom off:** ≤0.07 ms each.
- **GPU Resident Drawer:** BK shaders lack DOTS instancing and the frame is GPU-bound. **Parallel collision baking per cut:** CPU cost is already hidden behind GPU time.

## Edge cases
Rotated and scaled finds, finds at grid edges (still visible outside the grid), released or held finds, X-ray, and slivers between exposure samples. Checkpoint records: released bodies never reuse; restore/new game/recovery clear or refresh them; tests that edit a returned record call `Restore`.

## Acceptance criteria
- No fully buried find renders on an undug site; exposed and nearby finds still render and collect.
- Surface views gain measurable GPU time with pixel-identical ground and indistinguishable contact shading; water depth unaffected.
- Autosave capture no longer rebuilds unchanged finds; saved records match live finds.
- Relevant tests pass; fresh Windows player built.

## Verification
- Native before/after runs, same views: `unity/Logs/PerfPass/{device,groups,fixed,final-device,final-defaults}`. Ground crops old/new shader: max difference 1/255 (flat) and 0 (pit); only wind-animated horizon grass differs. SSAO variants in the pit: mean difference ≤0.43/255. Shore water difference is within the animation noise floor.
- In-Editor: 0 of 12,377 reused records differed from their live find after 48 real cuts.
- Tests: EditMode 289/289; PlayMode Discovery 30/30, Save 14/14, Terrain 23/23, Startup 8/8, UI 26/26, FpsPlayer 20/20, Rescue 7/7, Recharge 6/6, FindPhysics 40/41. `VisibleSliverBetweenExposureSamplesStillRenders` now places the mesh's real top vertex (not its inflated box) above the turf, and also asserts that a fully buried tilted rock stays hidden. The shaft in `RealExcavationSale…` digs terrain hits only, since the new layout put a rock under its ray. `DroppedRocksSettleAfterRepeatedExtremePitchChanges` hit its 180 s timeout twice; it hit the same timeout earlier the same day under background load, before this change.
- Fresh Windows player built with only the existing collision pre-bake and Pipeline advisories; it starts and exits without log errors.

## Iteration: shared checkpoint records
- The reused record was returned by the public `BuriedFind.Capture()`, so a caller that edited a capture also edited the cache and every other holder of it. `DetectorIntegrationTests` caught it: moving a copy moved the "original" too. Autosave now uses internal `DiscoveryField.CaptureCheckpoint()`/`BuriedFind.CaptureCheckpoint()` (shared, never edited). `Capture()` always returns a fresh record and leaves `hasChanged` alone.
