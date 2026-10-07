# 115 — Caverns and a Wider Underground

**Status:** complete:
- Underground the site is 50 m wide, about 8 m more than the plot on each side.
- One sealed cavern per zone lies beside the plot, east and west in turn: a walkable chamber in geode stone with
  minerals half-buried in its walls. The deepest is the crystal cavern: the Crystal Caverns demo's four areas, one
  to a chamber, their big formations standing from floor, walls and roof, lighting it and never breaking. The tool
  cracks and bursts the smaller clusters among them for the crystals sealed inside; more lie half-buried round them.
- Untouched solid ground is one shared page in memory and in saves.

Concept: [03 §1, §5, §7](../../concept/03_WORLD_AND_SITE.md), [05 §1](../../concept/05_DISCOVERIES.md),
[11 §1](../../concept/11_ENDING_AND_MYSTERY.md). Earlier plan: [114](114-mineral-lineup.md#the-crystal-cavern-at-the-bottom-new-task-115-after-110).

## Objective

The user (2026-10-06):
- "areas where the user can walk more, like caves at some places";
- "perhaps a larger area on the x axis underground so it's not the shape and size of the top area but underground it
  is wider so caves make more sense";
- caverns "even at some higher level, not only at the very bottom, you decide";
- "I really liked the demo from the crystals asset, would be cool if we carry over the vibe".

## Concept rules this changes or keeps

- **03 §1** said "no caves or tunnel networks". It now admits a few sealed caverns, one per zone, single rooms that
  never connect. "The hole is yours" and Meltopia's getting-lost complaint (13, closed ideas) still hold: every
  passage between places is dug, and a cavern is one room.
- **03 §1** kept the footprint "at its current width". The dig plot keeps it. The grid below grows east and west, as
  the boundary rule already allows ("the saved rectangular subsurface grid remains accessible below the bank for
  lateral digging").
- **03 §5:** geodes keep the break-through. A cavern has the same shell stone, so one tell reads "a hollow is in
  there". The player always breaks in, and inside it is dark.
- **03 §7** said "lamps are the only light". The crystal cavern is the one exception, a reward for reaching the
  bottom; every other cavern is lamp-lit.
- **13 closed idea:** cave zones skipped digging and showed finds fully exposed. Cavern finds sit half-buried
  (`CavernSink` 0.45), so a lamp shows them but digging frees them.
- **11 §1:** the crystal cavern's far end stays free for the ending's gate (13: the user's candidate).

## Codebase analysis (before)

**Grid.** 288 × 1200 × 208 cells at 0.125 m (36 × 150 × 26 m). The plot nearly fills it: |x| up to about 16 m of
the 18 m half-width, and z fully.
- Storage was dense, about 6 B per sample live (density 4, material 1, support workspace 1).
- `PagedDensity` allocated all 4096-float pages up front.
- Saves wrote every float through one gzip stream. Autosave encoded the whole 363 MB at least every 10 s whenever
  the world had changed.
- The save sample budget (`WorldSaveCodec.MaximumSamples`, 104M) capped a dense grid at 413 cells in x.

**Grid-size-dependent geometry** is baked by editor menus:
- the scene's `TerrainVolume.dimensions` and position, and the bedrock walls (Configure Excavation Size);
- the rim collar, terrain hole and streams (Configure Lakebed Excavation Site).

The bank, opening and find footprint derive from `SiteLayout` and do not change when x widens. The uniques'
authored positions are grid-local, so they move with the origin and were shifted 7 m.

**Ground.**
- `TerrainGround` seeds pits, stashes and geodes.
- `ExcavationGrid.Reset` carves pockets and hollows.
- `TerrainVolume.SeededAir` builds their chunks with the session.
- `DiscoveryCatalog` seats chest and geode contents from the population, so counts never change.

## Design

**Shared pages** (`DensitySnapshot`, `PagedDensity`):
- A page holding one value throughout is one shared read-only array per value and length, flagged per page.
- `Reset` fills by rows (`PagedDensity.Fill`): deep rows are a uniform page and allocate nothing.
- The live owner copies a page before its first write, as before.
- Saves write a marker byte, plus the value or the raw page. Save format version 21.
- `AnyModified` skips uniform runs, so the load-time chunk scan is cheaper.
- Fresh site: 21,664 of 24,574 pages are shared; about 45 MB of density is private, against 383 MB dense.
- Material IDs and the support workspace stay dense, about 100 MB each.

**Width.** `SiteLayout.WidthCells` goes from 288 to 400 (50 m), within the sample budget (100.65M of 104M).
- The scene, bedrock, collar and lakebed are re-baked.
- The retro-computer catalog positions move +7 m in grid-local x, keeping their world positions.
- The 200 m-deep headroom check is dropped: at 50 m wide a 200 m site would exceed the dense sample budget. A
  deeper site would first need material IDs and support state paged the same way.

**Caverns** (`TerrainGround.Caverns`, `TerrainGround.Caverns.cs`):

| Zone | Floor depth |
|---|---|
| 1 | 20–32 m |
| 2 | 46–70 m |
| 3 | 84–106 m |
| 4 (crystal cavern) | 126–144 m |

- **Placement:**
  - Sides alternate east and west, from a seeded start.
  - The spine runs north-south along the plot's edge, following its curve (`EdgeAlong`).
  - Each chamber's inner side lies `CavernInset` (2 m) under the plot.
  - Clear of pits, uniques' spaces and the grid's walls. Geodes keep clear of caverns.
- **Shape:**
  - 4–5 chambers (6 in the crystal cavern), about 3 m apart.
  - Ellipsoids 2.1–2.8 m across, 1.6–2.1 m half-height, 1.9–2.4 m along; crystal cavern 2.8–3.5, 2.3–2.9 and
    2.2–2.7 m.
  - Smooth-min joined with a 1 m blend, then a 0.35 m warp.
  - The floor is flattened at `Floor` with a 0.15 m roll.
  - 0–1 pillars (3 in the crystal cavern) flare at the foot.
  - 0.07 m lumps; the shell is 0.6–0.8 m of `GeodeShell`.
  - Burst-sampled like geodes (`CavernField`): a crystal cavern's box holds a few million samples.
- **Wall finds:** `Entry.Cavern` comes from `"cavern": true` on the eight minerals. `SeatCaverns` seats 12 per cavern
  (24 in the crystal cavern): instances whose band covers the floor depth, drawn by how many are left. So the
  zone's core minerals dominate:
  - zone 1: copper;
  - zone 2: iron and silver;
  - zone 3: gold;
  - crystal cavern: diamonds.

  Each is ray-marched to a wall (`CavernFace`) and sunk 45%. Finds keep out of a cavern (a reservation per chamber).
- **Scenery** (`CavernScenery` on the terrain, `CavernDressing`):
  - **Assets:** Configure Buried Props copies the demo pieces to URP (`CavernRocks`, `CavernCrystals`); Configure
    Caverns builds the dressing asset and bloom profile and wires them into the scene.
  - **When it spawns:** when the ground layout changes (new game, load, Ground Lab), seeded from the cavern, so
    nothing is saved.
  - **Every cavern:** 4 boulders and 3 rubble patches (5 and 4 in the crystal cavern).
  - **Crystal cavern also:** 6 BigBlock formations, plus 9 glowing CrystalBig/BigHex clusters on its walls. Each
    cluster is recoloured by a material property block in a demo colour (cyan, green, orange, red; base 0.3,
    emission 0.75) and has its own unshadowed point light (range 5.5, intensity 2.2).
  - **Sizes are in metres:**
    - boulders 0.5–1.1;
    - rubble 1.4–2.4;
    - formations 0.9–2.4 tall;
    - crystals 0.7–2.
  - **Lighting and look:**
    - All renderers register with `ExcavationDaylight`, so they stay dark until lit.
    - A global bloom volume (intensity 1.1, threshold 0.9) fades in on unscaled time while the view is inside the
      crystal cavern.
- **Ground Lab:** a crystal cavern under the plot's west edge, roof about 2.5 m down, so it is dug into from above.
  The prompt names it.
- **X-ray:** cavern stone is geode stone, so it is marked cyan; the legend says so.

## Rejected or changed during the work

- **Fully sparse storage** (materials and support state paged too) was not built. Uniform density pages alone gave
  the width within the existing budget. Paging the rest is the step a deeper or wider site would need.
- **The demo's cave walls, pillars, arches and surfaces** (8–33 m pieces) were not used. Scaled down they read as
  props; the caverns' own voxel stone is their walls.
- **Glowing crystals:**
  - Scaled by a fixed factor, the demo's 2–12 m spikes came out 2–4 m long and filled the chambers, so they are now
    sized in metres.
  - At emission 3, then 1.2, they read as flat colour; 0.75 keeps their texture.
- **Bloom fade on scaled time** stayed at zero while the game was paused (title, menus). It now uses unscaled time.
- **Random sides per attempt** put all four caverns east in the first seed. Sides now alternate.
- **Lights with shadows** were not used: six cube faces per light per frame were too costly for a dozen lights.
  Unshadowed light leaks about a metre past a cavern's stone, faintly, which reads as a tell rather than a fault.

## Edge cases

- **Props stay where they were set.** Digging under one leaves it standing (known; a support check is follow-up
  work if it matters in play).
- **A seed may fail to place a cavern** in a zone after 200 tries. That zone has none, its wall seats are not taken,
  and population counts are unchanged.
- **Other grids** (fixtures, benchmarks) get no caverns: `Caverns` needs the shipped extent.
- **Older saves:**
  - the save format version (21) changed, and the grid size and origin no longer match;
  - so a save from before this change asks for a new game.

## Acceptance

- **New Game seeds 4 caverns** with depths in band, alternating sides, chunks built at start, and 12/12/12/24 wall
  finds from their zones' minerals. Checked in play:

  | Cavern | Floor depth | Wall finds |
  |---|---|---|
  | Zone 1 | 30 m | copper |
  | Zone 2 | 49 m | iron and silver |
  | Zone 3 | 87 m | gold |
  | Crystal | 134 m | diamonds |

- **Plain caverns are dark** until lamp-lit.
- **The crystal cavern glows:** its walls take its crystals' colours, and its bloom shows from inside.
- **Saves:** a dug hole survives save and load in the new format, and caverns and scenery come back the same.
- **Memory:** a fresh site keeps about 45 MB of private density.
- **Tests:** the end-of-session test run passes, with updated size, layout and count tests.

## Playtest notes (`docs/playtests/115-caverns.md`)

## Iteration (user, 2026-10-06, after a first look)

"The crystal cavern is way too random, doesn't match the textures, way too many random minerals ... I can't dig
anything, which kills the vibe ... I want to break anything with purpose, break big structures into smaller crystals
... not a static cave to look at but something to interact with."

- **Style from the demo** (rendered from its scene): each grove is one colour, dozens of crystals crowded round one
  spot (big columns, sprays, many small crystals), the area lit in that colour. So each chamber now holds a grove
  (`TerrainGround.Grove`): 3 columns by a wall, 5 sprays on it, 5 shards in the floor. Colours change every two
  chambers (blue, green, red); one per chamber read as random where neighbours met, so amber went.
- **Nothing static:** the boulders, rubble and rock formations (undiggable props) and the crystal cavern's dusty
  diamonds are gone.
- **Breakable crystals:** columns and sprays are `CavernCrystal` dig targets. Each stroke adds the tool's stroke
  interval as work, so shovel and drill take the same time; a crystal needs 1.1 s per metre (a spray about 1 s of
  drilling). Shards chip off and it shrinks as it cracks; at the end it shatters and its sealed pieces fall out:
  3 from a column, 1 from a spray. The grove's light dims as its crystals go (to 35%).
- **Pieces are population finds** (glow crystals in `cavern.json`, one type per colour),
  sealed (`FindState.Sealed`, new: inactive, out of physics, pickup and the detector, allowed in saves) inside their
  crystal, released by `DiscoveryField.UnsealWithin`. A crystal is made again only while it still seals a piece, so
  broken crystals stay broken across loads with no save record of their own.
- **Props live under their own root:** under the terrain, the tool's contract lookup took them for ground.

## Iteration 2 (user, 2026-10-06, after a second look)

"They shrink when I hit, I'd prefer cracking ... the particles we use everywhere are too low quality ... some crystals
stick out as a stick, and once dug turn into pretty crystals of another type ... do it exactly as in the demo: areas
of only one type, sticking out from top and bottom, larger things ... think about whether everything should be
pickable, so the player doesn't get rich too fast ... maybe make them undiggable so they are for light, with
diggable things around them."

- **Four areas, as in the demo:** hex (green BigHex columns, prism clusters), quartz (blue quartz and beryl), ruby
  (the big red monoliths and slabs, chunky ruby clusters) and cubes (the pack's big stone blocks, amber pyrite
  clusters). `TerrainGround.AreaOf` gives each chamber one, in runs (two hex, one quartz, two ruby, one cube) in a
  seeded order; a cavern of fewer chambers (the Ground Lab's four) gets one each. A grove is 6 floor, 4 wall and
  4 roof formations, 3 clusters and 4 shards, each piece kept to the chamber whose ellipsoid it lies deepest in, so
  areas don't reach into each other.
- **Economy:** formations are scenery and light (`CavernFormation`, "Too big to break: dig the crystals and stone
  round it"). Only clusters (2 sealed pieces each) and shards hold finds, about ten a chamber; `cavern.json`'s
  counts match the chambers' seats, and any seat a tight chamber lacks goes to its area's walls instead of loose
  soil. Prices stay in the catalog.
- **Cracks, not shrinking:** a cluster carries a crack overlay (its own meshes again, a hair larger, in
  `CrystalCracks.mat`), clipped at a threshold that falls with the work done, so fractures from
  `art/crystal-cracks/make_cracks.py` spread and branch, glowing, until it bursts.
- **Look:** glow maps derived from each pack crystal's alpha (where the pack's crystal shader glows), normalised to
  one mean brightness; each area's glow scaled by its colour's luminance so green and amber keep their hue; deep
  saturated colours after the demo.
- **Particles everywhere:** `CavernShatter` throws faceted shard meshes in the crystal's colour, the pack's sparkle as
  additive glints (`GlowParticles.shader`) and its dust flipbook. The crane's crumbs are now small 3D lumps (four
  clod shapes with the backfill's grain on their faces, `SoilBreak.shader` `_SoilTex`), its dust and shaft motes the
  same flipbook; `SoilCrumbs.mat` went.
- **Rejected:** shrinking while cracking; the crystals' colour map as their glow (flat, plastic); the pack's own
  emissive contrasts (hex white-hot, rubies dark); the pale palette (washed out under tonemapping); single prisms and
  slabs as clusters (ruby 3, 4, 7, 8 and the long red sprays), which read as sticks.
- **Placement time** (end-of-session tests): the wider grid, the caverns' reservations and their groves had taken New
  Game's find placement from under one second to 1.6 s. Groves are now made once per cavern chamber and shared, large
  reservations are bucketed in 4 m cells so a candidate checks only those near it, and candidates are drawn inside the
  plot footprint's box: 0.6 s. Finds keep out of the corner a zone's cavern takes under the plot, so its depths hold a
  little fewer (a 5 m window's floor in the depth test went from 220 to 210 finds).
- **Seats keep clear of each other:** cavern wall minerals, a cluster's sealed pieces (side by side across its middle,
  turned and raised until clear of a neighbouring cluster's) and geode crystals (on a face that looks into the hollow)
  take a few seeded tries for a spot clear of the seats already taken; a piece or shard that finds none goes to its
  area's walls.
