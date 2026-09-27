# 006 — Material Zones & Ground Places

**Status:** complete. Four depth zones of one main ground each (soil with gravel lenses, orange clay with grey-blue sediment bands, grey rock veined with rust-red clay soft paths, and a colder zone-4 placeholder) join through short mixed bands, with seeded places per zone (rubble, open-topped concrete structures, rock masses, softer pond-clay basins as clay's tell); shovel levels grow evenly so one tool purchase always out-digs the next zone's main ground.

## Objective

Replace the thin 8 m stack that repeats all the way down with four zones, each mostly one main
ground (soil with gravel lenses → clay → rock veined with clay → a zone-4 placeholder), joined by
short mixed edge bands, with a few ground places per zone: rock masses, concrete structures (walls
and floor, soil inside) and clay basins. Retune the tool ladder so no zone feels like a restart at
the expected tool level. Arriving in a zone must be an event: "it's rock from here on".

## Concept reference (rules followed)

- `03` §3 zones: one main ground fills most of each zone; transitions are gradual; depth splits are
  roughly even quarters (the ancient zone may be shorter), tuned in generation code. Zone 1 soil
  with gravel lenses (+ stray concrete rubble); zone 2 clay (+ clay basins, drowned-village
  concrete structures); zone 3 rock veined with clay (+ rock masses, soft clay/gravel paths,
  waterworks concrete); zone 4 its own ancient material, defined in `039` (placeholder here).
- `03` zone rules: **no zone feels like a restart** (at the level a player typically owns on
  arrival, the new main ground digs no slower than the previous zone felt near its end; the drill
  milestone, level 7, lands around the rock zone); **never a wall** (every hard zone and place has
  soft paths; depth is never gated); **variety inside a zone** (colour bands, places and tells as
  landmarks; no glaring pale surfaces); **edge bands** (a short mixed band announces new ground);
  **mixed spots only with a job** (no random mixing elsewhere).
- `03` §4: resistance belongs to the ground inside the cut; deposits keep identity when saved;
  textures follow deposits with narrow blended boundaries. Every tell works the same way: the
  ground gets **easier** and following it leads somewhere. Clay's tell is "disturbed ground;
  basins", so a basin must dig easier than the clay around it.
- `03` §5 places: rock masses (mostly zone 3, several metres across), concrete structures
  (zones 2–3: walls and floor, soil inside, cracks later), clay basins (zone 2: a thick bowl of old
  pond clay, clean calm digging around organic finds). Several solutions (the starting tool always
  makes visible progress); legible before commitment; never across the main descent as a wall;
  never looks like bedrock or retaining walls (`03` §2).
- `03` §6/§8: places and zones come from the seed, stored as the same one-byte material IDs; no
  extra save records. Shapes and positions are randomized per save; zone layout is authored.
- `09` §1–2: zones read instantly with strong palettes and gradual transitions (recent fill warm
  browns; old sediment grey-blue and clay orange; deep stone dark grey rock with rust-red clay
  veins; ancient cold tones); one main ground never one repeated wall (colour bands, places); read
  by grain and relief as well as colour; no new generated ground patterns.
- `04` §1/`06` §2: the tool ladder is tuned against the zones' main grounds; the drill arrives
  around the rock zone.

## Live codebase analysis

- `TerrainMaterialSnapshot.Generate`: per-column Perlin warp, an 8 m `LayerCycle` repeating
  soil → gravel lens → clay → rock, top 1.1 m soil, rare concrete slabs per 6 m cell below the
  first cycle. Evaluated on the main thread for every sample at every session start (the grid is
  built from the seed in `TerrainVolume.InitializeSession`, and loads then replace it).
- IDs `Soil, Clay, Rock, Gravel, Concrete` (append-only bytes). `EquipmentProgression.HardnessOrder`
  soil < gravel < clay < rock < concrete; `MaterialResponse` shapes each sample's cut and the
  contact's cadence/fuel. Kernels in `ExcavationGrid.RemoveBrush`.
- `TerrainChunkMesh` (Burst `MeshJob`) writes UV2 = `(clay, rock, concrete, 1 − gravel)`; the stream
  is full. `GroundTriplanar` blends soil + four `DepositSurface` layers from it.
- Measured sustained output (m³/s, stationary bore, `TerrainMaterialTests` method) per level 1–10:
  soil .066 .107 .172 .283 .624 1.39 3.37 6.10 11.1 20.4; clay .039 .062 .098 .159 .355 .798 1.90
  3.42 6.28 11.5; rock .028 .044 .071 .114 .254 .571 1.21 2.18 3.98 7.30. Early shovel steps are
  only ~1.6× while clay is ~0.58× soil, so clay one level up is ~7% **slower** than soil one level
  down at levels 2–4: arriving in zone 2 would feel like a restart. Rock vs clay passes everywhere.

## Design

### Zones (generation code, `TerrainGround`)
- Borders at 37.5 / 75 / 112.5 m (even quarters of 150 m; absolute depths, so shallow test grids
  are all zone 1), each warped ±1.5 m by low-frequency 2D noise.
- **Edge band:** 3 m around each border; blobby 3D noise against a depth gradient interleaves the
  upper and lower main ground, so the first lumps of the next ground appear before it takes over.
- **Zone 1 (recent fill):** soil; top 1.1 m pure soil; flattened gravel lenses (a few metres wide,
  0.4–1.2 m thick) below 2 m. Gravel is a loose ground that digs ~as fast as soil.
- **Zone 2 (old sediment):** clay.
- **Zone 3 (deep stone):** rock veined with clay — winding clay tubes (two-sheet noise
  intersections) running in every direction as soft paths, a few gravel stretches.
- **Zone 4 placeholder:** the zone-3 ground with sparser veins and a colder palette until `039`
  defines the ancient material.

### Places (seeded list, only under the plot footprint, never in the top 3 m)
- Zone 1: stray concrete rubble (small tilted blocks).
- Zone 2: 2 drowned-village concrete structures (0.4–0.5 m walls and floor, soil inside, open
  top), 2 rock masses (noise-warped ellipsoids 4–7 m), 3 clay basins.
- Zone 3: 2 waterworks concrete structures (long low boxes), 3 rock masses without clay veins
  (the soft network routes around them; cracks come with `096`).
- Places never overlap one another, and none spans the plot (all ≤ 9 m against a ~30 × 22 m plot).

### Pond clay (new ground family, ID 5)
Basins are clay's tell, so they must dig **easier** than clay and read differently: `PondClay`
digs between gravel and clay (smooth clay-like shavings, wider), and renders with the clay
texture set in grey-blue with lower relief. `098`'s settled silt floors reuse it. Hardness order
becomes soil < gravel < pond clay < clay < rock < concrete. Saves stay one byte per sample.

### Tool ladder retune (`EquipmentProgression.ToolProfiles`)
Level 1 (accepted starting feel) and levels 6–10 keep their profiles; levels 2–5 grow their
radius so every shovel step is ~1.84× (the same total level 1 → 6 growth, spread evenly). Then one
purchase always outpaces the next zone's main ground: clay at level L ≥ soil at L − 1, rock at
L ≥ clay at L − 1, for every L. Prices stay; the design arrival levels are zone 1: 1, zone 2: 4–5,
zone 3: 7 (drill), zone 4: 9, validated by the economy pass (`077`). A test encodes the rule on the
measured output.

### Presentation (`GroundTriplanar`)
- Second weight stream UV3 = `(pond clay, reserved for backfill (099), 0, 0)`; missing streams
  read as none.
- Zone palettes: clay turns rust-red below the zone-3 border (the "rust-red clay veins" in dark
  rock), rock cools below the zone-4 border; soil keeps its warm browns. Transitions follow the
  warped borders with a soft blend.
- Colour bands: subtle depth-banded brightness (smooth sums of sines on a warped depth) on every
  deposit below the cap, so one main ground never reads as one repeated wall. Texture scale and
  relief still distinguish grounds without colour.

### Generation performance
A Burst `IJobParallelFor` over z-slices evaluates zones, bands, veins and a pre-filtered place
list; noise is `Unity.Mathematics` simplex. Target: full 150 m grid well under 1 s.

## Edge cases
- Top 1.1 m stays soil; first scrapes, the permanent bank and find placement are unchanged.
- Beyond the plot footprint the zones continue (plain ground, no places, no finds).
- A cut across a place boundary keeps per-sample resistance.
- Test grids shallower than a zone border contain only zone-1 ground.
- Rim/collar/preview meshes without the new stream render unchanged.
- Old saves already require New Game (094); IDs 0–4 keep their meaning.

## Acceptance criteria
1. Each zone is mostly its main ground (zone 1 ≥ 80% soil+gravel with soil dominant, zone 2 ≥ 80%
   clay, zone 3/4 ≥ 65% rock with 10–30% soft veins), edge bands mix both grounds within ±1.5 m
   of each border, generation is deterministic and leaves `UnityEngine.Random` untouched.
2. A soft path (non-rock, non-concrete samples) connects the top of zone 3 to its bottom inside
   the plot on every tested seed.
3. Places sit inside the footprint below 3 m: rubble in zone 1; concrete structures, rock masses
   and pond-clay basins in zone 2; concrete structures and rock masses in zone 3.
4. Pond clay digs faster than clay and slower than gravel at every tier; all tiers dig all six.
5. Measured output: for every level L ≥ 2, clay(L) ≥ soil(L − 1) and rock(L) ≥ clay(L − 1); every
   level still improves every family; level 1 unchanged.
6. Screenshots show each zone's main ground, an edge band, a concrete structure, a rock mass and a
   basin, with zone palettes and colour bands and no glaring pale walls.
7. Full generation stays under 1 s; tests pass; Windows build refreshed.

## Results (2026-09-27, shipped seed 2718)

- Composition under the plot, away from the bands: zone 1 soil 94.4 %, gravel 5.5 %, concrete
  rubble 0.1 %; zone 2 clay 97.4 %, pond clay 0.7 %, concrete 0.5 %, rock 0.7 %; zone 3 rock 78 %,
  clay 19 %, gravel 1.8 %; zone 4 rock 85 %, clay 14 %, gravel 1.1 %. 20 places.
- First try had 36 % clay in zone 3 (2 m veins): the vein half-width sets the volume share, so it
  was halved (.13 → .065; zone 4 .05) for ~1 m veins at ~20 %.
- Full-site generation ~0.5 s (Burst, 15 workers). The first Editor run after adding the job ran
  unburst (~20 s) until a domain reload registered the new entry point; players compile it AOT.
- Measured sustained output after the retune (m³/s, L1–L10): soil .066 .122 .224 .404 .754 1.39
  3.37 6.10 11.1 20.4; clay .039 .070 .129 .232 .431 .798 1.90 3.42 6.28 11.5; rock .028 .050
  .092 .168 .308 .571 1.21 2.18 3.98 7.30; pond clay .045 .083 .152 .277 .509 .945 2.37 4.30 7.85
  14.4. Every clay(L) ≥ soil(L − 1) (tightest at level 8: +1.6 %) and rock(L) ≥ clay(L − 1).
  Pond clay first used a faster cadence and out-dug gravel at drill levels, then matched gravel on
  fresh single shaves (both cut elliptical footprints); its bite is now 1.0 × 0.88 × 0.92 at clay's
  cadence: 1.16–1.25× clay sustained and below gravel on every metric.
- Visual review (lamp-matched light): the first clay read as a flat blood-red; the tint moved to
  clay orange with grey-blue bands on the darker strata (concept 09 §2 "grey-blue, clay orange"),
  concrete was toned down from glaring white, and the zone-4 cold tint was softened. Evidence in
  `Logs/006-sheet2.png`.

## Notes for later tasks
- `TerrainGround.Places` is the public place list (`096` cracks inside rock masses/structures,
  `098` rooms inside structures, `099` odd spots). UV3 channels y/z/w are reserved for fractured,
  crack and backfill.
- Every New Game still uses the scene's fixed seeds (excavation 2718, discoveries 90127);
  per-save randomization is not wired (see `077`).
- Beyond the plot the zones continue as plain ground (no places, no finds).

## Iteration (playtest 2026-09-27)

- The user found hard ground just slowed the digging. Hardness is now bite size only: every ground
  keeps the tool's cadence and fuel per stroke, and each response's width, length and depth absorbed
  its old cadence factor (cube root of the old slowdown per axis, backfill widened a little). Sustained
  output per ground and level stayed within about ±7% of before, so the zone rule and hardness order
  hold unchanged; backfill with the drill is 1.35× soil (was 1.47×).
