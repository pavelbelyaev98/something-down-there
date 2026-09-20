# 064 - Demo-Matched Valley, Water & Natural Boundary

## Objective
Replace the box-canyon worksite with an alpine valley matched to the approved Pure Nature 2:
Mountains demo (`Mountain_Demo.unity`) and enclose it with real mountains rather than walls:
- Vivid green meadow, dense conifers, grass-ledged cliffs, stream -> lake -> waterfall and
  hazy blue peaks, under the demo's sky/fog/ambient/post stack.
- The diggable sediment pad stays the worksite centre; everything around it is non-diggable.
- The invisible `Perimeter` walls are deleted; containment is terrain steepness only.
- Starter-find bottle assets are removed entirely from the project.

Reference captures: `Logs/064/demo-*.png`. Result captures: `Logs/064/064-*.png`.

## Demo measurements (source of truth)
- Sky `Sky_Mountains.mat` (`BK/Sky`); ambient **Flat** `(0.622,0.639,0.657)` x1.2; fog
  **Exponential** `(0.162,0.459,0.591)` density `0.003`; sun Euler `(45,45,0)` intensity 1.2.
- Post: `Mountains_PostProcess.asset`. Terrain material: `Layers/Terrain.mat` (URP Terrain Lit).
- Layers (order/scale): Gravel1(5m), Gravel2(10m), Grass01(10m), Mud02(10m), Mud01(5m).
- Terrain details: 23 vertex-lit prefab prototypes, byte densities (the demo packs meadow near 255).
- Water: Unity Plane + `Ocean2.mat`; `WaterStream`/`WaterFall` prefabs; reflection probe.
- **Mountain prefabs are ~2.8 cm across at scale 1**, which is why the demo drives them with
  five-figure scales (e.g. Mountain2 at `65489,38016,74688` = 1.9 km x 357 m, ~1.2 km out).

## Geography (`GroundHeight`)
A radial bowl, not a box. All radii are modulated by `Lobe(bearing)` (integer harmonics only,
so the field stays continuous across +-pi) so the valley edge wanders instead of reading as a
drawn circle; `RidgeNoise` breaks the skyline.

| Band | Radius | Rise |
|---|---|---|
| Pad (flat, diggable) | `|x|,|z| <= 16` | 0 |
| Meadow (walkable) | 17 -> `68 * lobe` | 6 m |
| Forested slope | -> `106 * lobe` | 20 m |
| Bank step 1 | +13 m | 26 m (slope 2.0) |
| Bench (grass, trees) | +14 m | flat |
| Bank step 2 | +16 m | 20 m (slope 1.25) |
| Ridge line | -> 260 m | 12 +- 22 m |

- **Boundary:** both bank steps beat the 45 deg `CharacterController.slopeLimit`. Verified by a
  360-bearing sweep in `MainGameSceneTests`; weakest bearing is now 1.31 (52.6 deg).
- **North corridor** (z 10 -> 300): floor at -2.6 m so the lake ends on a shore, benched
  shoulders, lake basin at z 122 +- 31, a notch at `|x| < 9` that holds the waterline to the lip
  and then steps up, and forested hillside beyond.
- **Peaks:** two rings authored in metres of finished mountain and converted through the prefab's
  measured bounds - inner 18 peaks 470-690 m tall at 720-900 m, outer 13 peaks 700-1000 m at
  1.25-1.65 km. Footprints are squashed against the height (aspect 1.3-2.1) so a small valley can
  hold them close without their skirts overlapping the playable terrain. Shadow casting off;
  camera far clip 3,200 m.

## Code changes
- `Assets/Editor/ReservoirEnvironmentSetup.cs`: radial height field + lobe/ridge noise, demo splat
  and 23-prototype detail set, slope-driven cliff pass, forest/meadow/lake scatter, stream, lake,
  notched falls, mountain rings, reflection probe, `Perimeter` removal.
- `Assets/Editor/SunPresentationSetup.cs`: single presentation owner (vendor sky copy, demo
  fog/ambient/sun, post copy on the `Daylight Colors` volume).
- `Assets/Editor/GroundTextureSetup.cs`: demo ambient block, sun shadow strength .9.
- `Assets/Editor/MainGameSceneBuilder.cs`: no `Perimeter` group.
- `Assets/Editor/StarterFindSetup.cs` + `art/starter-finds/catalog.json`: bottle assets pruned.
- `Assets/Tests/EditMode/MainGameSceneTests.cs`: new contract (5 layers, detail set, no perimeter,
  far clip, peak height floor, 360-bearing containment sweep). The test assembly now references
  `SomethingDownThere.Editor` so it can sample the generator's height field directly.

## Decisions worth keeping
- **Planar terrain UVs smear on any steep face.** This, not lighting or texture choice, is why the
  first pass read as grey sheets. Two rules follow: bench tall walls into steps a grass splat can
  hold, and clothe whatever is left by *scanning the height field for steep patches* rather than
  hand-placing cliff rings — hand-placed rings go stale the moment the shape is retuned.
- **Seat cliffs on the lowest ground under their footprint.** `Spawn` samples under the pivot; a
  40 m block on a steep face then hangs with daylight beneath it.
- **Size peaks against the rim, not by eye.** The rim is ~100 m at ~230 m (24 deg); peaks must clear
  that by a wide margin or they read as pebbles on the horizon.
- **Scope the corridor blend to its own channel.** Blending the corridor profile in on `z` alone let
  a shallow bearing cross the channel wall while the blend was still weak, smearing a 40 m wall
  into a walkable ramp and opening the valley on 3 of 720 headings.

## Edge cases
- Terrain never overhangs the 16 m opening (asserted per tile); tile seams stay continuous.
- Stream/lake never touch the dig footprint or stations; the pad edge is never lowered below 0.
- Water and BK materials survive the build shader strip (referenced in-scene).
- `ExcavationDaylight` only rewrites URP/Lit materials, so BK props/water/terrain are unaffected.
- Old saves with retired bottle ids keep resolving through `LegacyAliases`.

## Acceptance criteria
1. First-person captures of worksite, meadow, corridor and lake read like the demo references:
   green meadow, hazy peaks on every bearing, water, dense forest. **Met** (`Logs/064/064-*.png`).
2. No `Perimeter` objects remain; every bearing out of the bowl is steeper than the slope limit.
   **Met** (weakest 1.31 vs 1.00 limit).
3. Stream + lake render with `BK/Water`; waterfall on the notch face; terrain details in the meadow.
   **Met.**
4. Bottle starter-find assets and source art are gone; minerals/photo rocks still sync. **Met.**
5. EditMode + PlayMode suites green; fresh `builds/windows/SomethingDownThere.exe` smoke-runs.
