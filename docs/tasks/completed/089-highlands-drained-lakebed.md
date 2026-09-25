# 089 — Highlands Drained Lakebed Site

**Status:** complete. MainGame retains the Highlands canyon around a contained drained lakebed, round meadow excavation and south-rim camp. Project-owned water corrects refraction and foam, with Highlands grading and projected canyon reflections; presentation refresh preserves authored terrain and scenery.

## Objective
Replace the plain round gravel yard with the river section of the approved **Pure Nature 2: Highlands** demo, kept as close to the demo's terrain composition and asset placement as practical, and adapted to the game:
- the river is widened into a former lake that still holds water along the canyon;
- one large section of that lakebed is drained and exposed;
- the existing voxel dig ground sits inside the drained section, ringed by non-diggable ground, rubble and rocks that read as its natural boundary;
- the existing computer, recharge point, return anchor, winch, recovery pads and exhibit stands stand around the dig area.

User direction (playtest iteration): the site is a drained **lakebed**, no longer a drained reservoir. Unrelated systems and areas stay as they are.

## Concept reference
- `00`/`01`: the setting is a drained lake whose falling water exposed valuables and bones; ordinary daylight, no hazards or horror.
- `03` §1–2: one contained worksite; the diggable surface is surrounded by permanent ground that cannot be dug from above, while the saved subsurface grid stays reachable laterally beneath it. Permanent boundaries must look categorically different from diggable ground. No swimming or water interaction. Every opening in the ground is player-made.
- `03` §7 / `09`: near-overhead noon sun, vivid colour grade and underground darkness stay unchanged.
- `07` §1: everything the player needs between trips stays within about ten seconds of the shaft mouth.
- This task rewrites `03` §1–2, `01` §1, `07` §1 and `09` §1 in place: the previous "no mountains, trees, streams or lakes; gravel yard only" rule is superseded by the Highlands lakebed.

## Live codebase analysis
- `SiteLayout` fixes the voxel allocation: 24 × 100 × 24 m at 0.125 m, top at y = 0, round opening radius 12 m. Enlarging it multiplies the ~150 MB density/material footprint and save size, so the dig ground keeps this footprint; the drained section around it is permanent terrain.
- `RoundSiteSetup` owns the site surroundings: removes old environment roots, builds a 256 m closed annular gravel slab (`Surface/Walking apron`, render + collision, `PermanentTerrainBoundary`) and adjusts bedrock walls, grass radius, camera far plane, ground materials and sun. `MainGameSceneBuilder` calls it.
- The slab's bottom face is also the ceiling seen from lateral tunnels beneath the square grid's corners. A replacement must keep a closed underside there; Unity terrain is one-sided.
- Stations: `SurfaceStationSetup` creates the computer only if missing; `SalvageWinchSetup` rewrites winch/pad/stand *local* positions under `Surface/SalvageWinch` on every run; recharge, return anchor and player come from `MainGameSceneBuilder`.
- `ExcavationDaylight` scans renderers under `MainGameRoot` once and adapts only URP Lit materials; vendor BK shaders are untouched. `SurfaceGrassRenderer` clips meadow to `surfaceRadius`.
- `FpsPlayer` resolves dig contracts through `GetComponentsInParent<IDigTarget>`; any collider below a `PermanentTerrainBoundary` reports the permanent-boundary prompt.
- Demo (`Highlands_Demo.unity`): one 2000 m terrain (4097 heightmap, 4096 splat, 2048 detail, 6 layers, 14 detail and 10 tree prototypes, 9174 trees) plus prefab instances grouped as Cliffs, Peaks, BigBoulders, Boulders, Rubble_dense/sparse, Ruins, Trees, Water and Fx. Sea-level water is scaled `Water` planes at y = 10 with a non-trigger MeshCollider (walkable surface); `BK/Water` is back-face culled and world-space mapped. The east river arm runs through a 100–170 m canyon with 10–12 m shelves, 1–5 m riverbed, 40–50 m east cliffs and a waterfall into its southern end.
- Project `TerrainData` assets serialize binary even in Force Text mode.

## Design
- **Section:** a 1000 m window of the demo terrain centred on the east river arm, copied at demo resolution. All Cliffs/BigBoulders/Boulders/Rubble/Ruins/Trees and elevated rivers/waterfalls inside it, plus every demo peak for the horizon, are instantiated as vendor prefab instances with their demo overrides and relative placement. Demo sea-level planes are replaced by a generated lake surface (see below). Birds, butterflies, reflection probes, demo camera, light, volume and environment manager are not copied.
- **Coordinates:** the drained-section dig centre in demo space maps to the MainGame origin; the voxel top stays y = 0.
- **Widened lake:** the canyon floor below the old shoreline is flood-filled from the river. Inside it, the ground falls from the old shoreline through an exposed bathtub band into water, then to a deeper bed. Submerged demo plants, trees and small objects are removed; exposed ones are lowered onto the new ground; large rocks stay.
- **Drained section:** a broad flat of mud, rubble and sparse re-growing grass on the east shelf against the cliff, sloping gently into the lake to the west and the waterfall pool to the south. Its demo trees and lush detail grass are cleared.
- **Dig area:** a flat plateau around the opening. The terrain has a hole over the opening; a draped rim mesh (`Surface/Excavation rim`) covers the terrain-hole staircase with an exact circular edge in the lakebed rubble texture and keeps the closed underside above the grid corners. Rubble patches and rock clusters ring the opening with walkable gaps; the camp side stays clear.
- **Water:** one generated lake-surface mesh with the demo water material covers only ground below the water line, so the voxel pit never contains water. (Iteration 1 replaced the hidden wading floor with play-area walls at the water line.)
- **Stations:** the existing tested cluster stays on the south rim (computer, recharge, return anchor/spawn looking north up the canyon, winch, pads and exhibit stands), lifted onto the lakebed ground. Boundary rocks and rubble keep that arc clear.
- **Presentation:** existing sun, sky and colour grade stay. The demo's exponential haze is enabled and the camera far plane is 3 km. Terrain shadow casting is off.
- **Tooling:** `RoundSiteSetup` becomes `LakebedSiteSetup` (menu *Configure Lakebed Excavation Site*), regenerating terrain data, environment, rim, water and camp from the vendor demo deterministically. The gravel slab and its assets are deleted. Generated assets live in `Assets/Content/Lakebed`; the binary terrain data is stored with Git LFS.

## Edge cases
- Terrain/rim/collider must never cap the circular opening; grass stays within it; the terrain hole never exposes sky from above.
- Lateral tunnels under the grid corners see the rim underside, not through terrain.
- No demo object, water surface or collider intersects the 24 × 24 m grid column or the station footprints; the winch rope clearance above the rim stays free.
- Stations, recharge footprint and spawn stand on flat permanent ground outside the opening.
- Setup reruns rebuild the same environment and do not duplicate objects; vendor assets and the demo scene stay unmodified; the recovery scene is untouched.
- Saves: layout/format of the voxel grid is unchanged, so current saves still load.

## Acceptance criteria
- MainGame shows the recognisable Highlands river canyon with a widened lake, one large drained lakebed section and the dig opening inside it, framed by a natural rubble/rock boundary; stations stand around the opening.
- Digging, pickup, winch recovery, recharge and return still work; the rim and terrain report the permanent-boundary prompt; the player cannot sink under the water.
- Scene/site/terrain/station tests updated to the new ownership and passing; no compiler warnings; Odin validation clean.
- Fresh Windows build at `builds/windows/SomethingDownThere.exe`.
- Concept, baseline, architecture, readme and asset card updated.

## Implementation notes and rejected attempts
- Demo window: 2049² heights at demo spacing (1000 m), 2048² splat, 1024² details; trees, details and objects inside the flooded bed or camp clearance are removed, exposed small objects follow the new ground. Peaks within 1.6 km are kept for the horizon.
- Lake bed: shoreline falls from the old 13 m contour to 1.2 m under water within 5 m, then to 5.5 m. A shallow 1–1.5 m shelf was tried first; the BK water foamed white across it.
- Boundary stones: standing `Rock_*` pillars read as a stone circle and were replaced by lying, half-buried stones at uneven radii. Loose rubble is pushed outward until its bounds clear the opening, so digging never leaves pebbles floating.
- Vendor objects lose *Batching Static*: runtime static batching of ~4,600 LOD objects exhausted system memory on repeated MainGame loads and crashed the Editor during PlayMode tests.
- Terrain shadow casters ignore terrain holes: the collar flickered black/white and the whole shaft would have been shadowed. Terrain shadows are off; cliffs and rocks still cast shadows.
- Camp turned 90° to the cliff side was rejected: the winch recovery physics tests are tuned to the south-rim cable direction and two failed. Several fixtures also assumed a +z spawn heading; `TerrainIntegrationTests.PlacePlayer` and the find-physics setup now start from a fixed heading, and the admin-reset check compares against the return anchor.
- Opening the demo makes Unity auto-upgrade a few vendor files (URP asset, particle materials); those diffs are discarded.

## Verification
- EditMode 288/288 and full PlayMode 224/224 passed; new scene test covers rim/terrain-hole geometry, dig-column clearance, lake/wading floor, batching flags, terrain shadows and station support. No compiler warnings.
- Play Mode review: meadow opening in the drained section, lake to the west, cliffs and waterfall canyon; sunlight reaches a dug shaft; Editor frame ~6–7 ms CPU/GPU at spawn.
- Windows build succeeded after the Odin validation step; the player starts without log errors (~3.7 GB private memory at the title).

## Iteration 1 — playtest feedback
- Feedback: rivers reflected light oddly; FPS dropped; the player could fly high and roam the map; the dig edge needed a clearer, prettier natural transition; UI check requested.
- Reflections: MainGame now uses the demo's sky material (project copy), flat ambient 0.736 × 1, warm sun colour/intensity (angle stays at noon) and a Custom reflection probe with the demo's baked box-projected cubemap covering the canyon. The post profile was already near-identical; the demo's volumetric clouds and wind were not copied (GPU cost; the wind is tuned for the dig meadow).
- Play area: 72 wall boxes along the drained outline (9 m inside its mapped edge, at the water line) and a flight ceiling that stops the feet at 16 m, on Ignore Raycast so aim, digging, lamps and the winch ignore them. The wading floor is gone.
- FPS: draw calls were ~5,700 at spawn, mostly vendor LOD objects and terrain trees/grass. Setup now renders every scenery object in a unique flat colour from 174 viewpoints (grid + edge, ground and ceiling height, black terrain, 2× LOD bias) and removes those never seen (~3,200 of ~4,600), and removes terrain trees without terrain line of sight (~1,900 of ~3,200). The ~150 border pebbles are one merged mesh; terrain pixel error is 3. Draw calls at spawn fell to ~3,200.
- Edge: the rim collar is 0.9 m wide and uses the dig meadow's own material (bare soil ring); a continuous border of small collider-free pebbles marks the circle; a tinted project copy of the Highlands grass layer (`RimMeadow`) plus sparse terrain grass fades raggedly into the mud; station footprints stay grass-free. A lime-green halo with a tinted URP Lit collar was tried first and read as a separate ring.
- Pitfall: creating an asset after `CreateAsset(terrainData)` reloads the unsaved terrain data (heights became zero); the meadow layer is created first.
- UI: title, load-failure dialog (the old Sep 8 editor save is an older format), HUD, computer, pause and settings screens verified in Play Mode after New Game.

## Iteration 2 — water, lighting and world authoring
- Goal: retain the Highlands composition and noon sun, with readable water and shoreline detail instead of white glare. MainGame is the authored scene; the protected recovery snapshot is unrelated to the current environment.
- Diagnosis: MainGame inherited the Mountains grade; URP disabled probe box projection/blending despite the copied probe enabling projection. BK water feeds the already-lit opaque scene back through diffuse lighting, adds foam to that colour, and takes a fractional power of negative foam falloff beyond the shore.
- Architecture: extend `SunPresentationSetup` and `LakebedSiteSetup`, using project-owned water shader/material copies. Composite refraction without lighting it twice, bound foam math, keep normals/reflections, and apply presentation independently of terrain regeneration. Preserve manually tuned copies on later setup runs.
- Edges: lake and elevated rivers share the correction; waterfall materials retain their separate shader. Depth/opaque textures, camera post processing, render-pipeline reflection support and translated baked-probe placement must agree. Never save or overwrite recovery, rebuild terrain for a lighting fix, or lose the normal player pose while reviewing water.
- Acceptance: compare shoreline, open water, elevated river and surface gameplay views; no broad clipped-white water; no new compiler/console errors; existing scene/lighting/terrain checks pass once changes are finished; fresh Windows player. Document terrain/object/material authoring and the destructive scope of site regeneration.
- Verification: same-view original/corrected water comparison, elevated stream and an actual composed gameplay frame after excavation reviewed; no new console errors or warnings. All 288 EditMode and 36 selected PlayMode checks passed. Recovery scene hash is unchanged; MainGame geometry and terrain data are preserved.
- Build cleanup: replace deprecated URP light-loop and GBuffer declarations in the shared BK shaders and project copies, following the installed URP headers. Preserve publisher GUIDs and the project grass MotionVectors pass; its generator correctly rejects the older vendor template. The local Mountains asset card records reimport requirements.
- Delivery: fresh Windows development build succeeds with zero errors and no shader compiler warnings; its only warning is that Pipeline automation is disabled in the player. The full build also reports future pre-baked collision requirements on existing vendor meshes. The standalone player starts and exits cleanly; MainGame is left open with its surrounding terrain selected for authoring.
