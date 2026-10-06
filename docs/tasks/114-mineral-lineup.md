# 114 — Mineral Lineup: What Lies Where, and How It Looks

**Status:** plan. The user accepted the agent's lineup (2026-10-06). Building waits for the user's go and three
small picks (end of this spec).

## Objective

Settle which minerals the player finds at which depths and how each looks, so every zone has its own pair and
every look is a bought asset or a project edit of one. Gems come three ways: loose in the deep ground, in pockets
at the bottom, and cut in the old chests.

## User direction (2026-10-06)

- "The minerals have to be refined a lot... the only approved design so far is the rock, others are placeholders."
- "I will go with your suggestions for minerals." Creating mineral and gem looks from the existing base is fine.
- "Gems I do want in chests, but also at the bottom area somehow."
- The mineral prices and depth bands stay in the catalogs, as for every find.
- "The demo in the cave has some entrance and also cool-looking rocks; I wanted to make an area out of it
  somewhere." (screenshots: the Crystal Caverns demo's cubic rock formations, and a carved stone doorway)
- Long term (not this task): that "gate" as the final discovery with something behind it, and a mining mech as the
  last find (see the end of this spec).

## Concept reference

- `05` §1:
  - The commons include the mineral ladder.
  - Each type has a fixed price, and value rises with depth while the find rate stays about constant.
  - Each type's core band holds most of it; thin scatter goes both ways.
- `03` §3: four zones of 37.5 m each. The zone's main grounds (`111`) come later; minerals do not wait for them.
- `03` §5 and `110`: geodes are hard shells around crystal-lined hollows, and the crystals are finds.
- `05` §3: a chest's contents come from the finite population and lie only in chests.
- AGENTS.md "Bought Before Made":
  - Looks come from owned packs first.
  - Any bought pack's textures may dress new items.
  - Original modelling is for minor pieces only.

## Lineup (core bands centred in their zones; scatter about one zone each way)

| Zone | Minerals, shallow to deep | Look |
|---|---|---|
| 1 Recent fill (0–37.5 m) | rock (top metres), coal, copper | photo rock (approved); coal: the pack's ore chunks with a coal map made from the pack's own iron-ore map; copper: pack ore |
| 2 Old lake sediment (37.5–75 m) | iron, silver; pyrite ("fool's gold", optional) | pack ores; pyrite: Crystal Caverns pyrite cubes as they are |
| 3 Old riverbed (75–112.5 m) | gold (a riverbed is where placer gold lies), emerald | pack ore; Crystal Caverns beryl in our green |
| 4 Deep stone (112.5–150 m) | ruby, diamond | Crystal Caverns ruby; quartz in our clear white |

- **Order:** the ladder keeps its value order (coal cheapest, diamond dearest) and today's find rate.
- **Bands:** only the bands move, so each zone's pair is the bulk of what that zone yields.
- **Pyrite:** if taken, it is the one cheap find in zone 2, priced below copper. It looks like gold and sells like
  rock, a small joke with real geology behind it. It sits outside the ladder's strict value order, so it imports
  as a prop find (`PropFindSetup`), not a mineral.

## Gems three ways

1. **Loose rough crystals** in their bands, as now.
2. **In crystal hollows:** geodes (`110`) in zones 2–3 are lined with quartz and emerald. At the bottom, the
   crystal cavern (`115`, below) holds the ruby and diamond.
   - Their wall crystals are finds.
   - Big crystals and hexagons are the scenery.
3. **In the old chests (built in `113`, user 2026-10-06: "only ingots and crystals"):** emerald, ruby and diamond
   crystals at chest size beside the ingots, chest-only treasure taken by hand with E. They share the gem looks
   and are priced as treasure. This task settles whether a deep gem and a chest crystal of one kind are one item
   at one price.

## Assets

- **Used as they are:**
  - photo rock;
  - the Mining pack's ore chunks A/B;
  - Crystal Caverns beryl, ruby, quartz and pyrite crystals, big crystals and hexagons (pocket
    lining), and rubble.
- **Project edits (texture reuse, no new modelling):**
  - **Coal:** the ore chunk meshes with a coal-black map derived from `T_Ore_Iron_BC`/`_Mask` (darkened,
    desaturated, a little glossier). It is written by a script on the pack card, the way the backfill map is.
  - **Gem colours:** our tints on the crystal maps, as now.
- **Retired:**
  - the last Blender placeholder (coal) and `Minerals.blend`'s other placeholder models: once coal moves, the
    `art/minerals` sources keep only the catalog;
  - the round coins and the unused crystal kinds stay unused.
- **Only if the playtest asks:**
  - single crystals cut out of the clusters in Blender, if the clusters read too cartoonish at find size;
  - the pack's mineral-deposit decals on the soil around ores as a "something is here" tell (a separate task after
    `111`).

## Changes

- **`art/minerals/catalog.json`:**
  - re-centred bands;
  - coal takes `prefab` and `prop_looks` (the coal chunk variants);
  - `MineralSetup`'s checks stay (eight names, rising value and core depth).
- **`BuriedPropsSetup`:** a coal material and coal variants of the ore chunks.
- **Treasure:** the chest crystals (`art/pure-nature-crystal-caverns/catalog.json`) and the deep gems share one
  item and price per kind, or stay apart, as decided here; chest seats and treasure instances still add up exactly.
- **Pyrite** (if taken): a prop find source with its own band.
- **Tests:**
  - `DiscoveryCatalogTests` count lists;
  - the depth-mix test (`DepthMixSlidesFromJunkToValueAndKeepsScatteredOutliers`) checked against the new bands;
  - chest treasure only in chests.
- **Docs:** concept `05` §1 (the zone pairs), baseline, art cards (`minerals`, `mining-pack`, crystal caverns),
  the playtest note.

## Picks for the user

1. Pyrite as zone 2's cheap "fool's gold": in or out? (Agent: in.)
2. Deep gems and chest crystals: one item per kind (a ruby is a ruby, one price) or apart (chest crystals worth
   more). Agent: one item; the chest's are simply ones someone already dug up.
3. The crystal cavern (`115`) as its own task after `110`, sharing the geode's break-in (agent: yes).

## Acceptance

- The catalog bands match the zone table, and each zone's pair makes up most of its finds.
- Every mineral, gem and pyrite has a bought look or a project edit of one, with LODs and a hull in budget.
- The chests' treasure lies nowhere else.
- Tests pass; a playtest walks one shaft through all four zones (with admin depth help).

## The crystal cavern at the bottom (new task `115`, after `110`)

- **The idea:** the user's "area" from the Crystal Caverns demo, as a natural cave near the bottom of the site that
  a shaft or tunnel breaks into.
- **What the demo is made of** (2,037 placements, all in our import):
  - cave walls, cliffs, peaks, pillars, bridges and surfaces;
  - the six cubic rock formations (`BigBlock_1`–`6`, the "cool-looking rocks");
  - rubble and boulders;
  - quartz, beryl, ruby, fluorite and pyrite crystals, big crystals and hexagons;
  - light shafts and sparkles.
  - Its lava is not used. The carved doorway in the user's second screenshot is not among them (see below).
- **Building it:**
  - The cave is seeded air carved like the Ground Lab's scenes: always present from New Game, its chunks built with
    the session.
  - The pack's meshes are its walls, as solid, undiggable scenery the voxel ground meets.
  - They get project URP copies (`BuriedPropsSetup`), so they light like the dig: dark until the player's lamps
    reach them, never sunlit underground.
- **What it holds:**
  - the deep gems (ruby and diamond on its walls);
  - a few uniques' lenses or ending parts later;
  - at its far end, the gate (below).
- **Its own spec:** layout, how its walls meet the voxel ground (the carve sits just inside the mesh shell, so dug
  ground never shows a mesh's back), performance and save.

## Long term: the gate and the mech (Phase 7, not this task)

- **The gate:**
  - The carved doorway in the user's screenshot is built from Crystal Caverns' `BigBlock` pieces, which we own.
    They include tall carved pillars with engraved symbols (`BigBlock_3`, `_4`), plain blocks and slabs, and an
    L-shaped lintel (`_6`).
  - Its cave arches are natural rock, and Highlands' Mosaic Ruin set is floor mosaics, so neither is the gate.
  - It would stand at the cavern's far end, matching the open payoff "parts collected to open something"
    (`11` §1).
- **The mech:**
  - A mining mech as the final object matches `11` §3's "an impossibly ancient original of the player's machine".
  - It works only if it reads as made of ancient materials (stone, bronze), not as a sci-fi robot.
  - A bought mech re-dressed with the packs' stone textures could do that.
- **Agent's view:** they work best together. The ending components open the gate, and behind it stands the ancient
  mech, the machine yours descends from. Both are recorded in `13` as the user's candidates; the choice comes with
  the mystery payoff before Phase 7.
