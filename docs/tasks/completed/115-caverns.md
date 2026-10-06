# 115 — Caverns and a Wider Underground

**Status:** complete:
- Underground the site is 50 m wide, about 8 m more than the plot on each side.
- One sealed cavern per zone lies beside the plot, east and west in turn: a walkable chamber in geode stone with
  minerals half-buried in its walls. The deepest is the crystal cavern, dressed, glowing and lit in the Crystal
  Caverns demo's colours.
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
