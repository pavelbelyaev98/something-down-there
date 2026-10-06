# 114 — Mineral Lineup: What Lies Where, and How It Looks

**Status:** complete. Each zone has its own pair of minerals: coal and copper, iron and silver, gold and emerald,
ruby and diamond, with pyrite as a cheap aside in zone 2. Coal is the Mining pack's layered rocks in black; copper is
its ore chunks at the pack's quality with copper over about half the stone; iron, silver and gold are solid native
nuggets, gold on the pack's rounded rocks; ground gems and pyrite are dusty Crystal Caverns crystals. Chests hold ingots only.

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

## Lineup (core bands follow the zones; thin scatter both ways, as before)

| Zone | Minerals, shallow to deep | Look |
|---|---|---|
| 1 Recent fill (0–37.5 m) | rock (top metres), coal, copper | photo rock (approved); coal: the photo rock's model with a coal map made from the rock's own maps; copper: pack ore |
| 2 Old lake sediment (37.5–75 m) | iron, silver; pyrite ("fool's gold") | pack ores; pyrite: Crystal Caverns pyrite cubes, dirty and brassy |
| 3 Old riverbed (75–112.5 m) | gold (a riverbed is where placer gold lies), emerald | pack ore; Crystal Caverns beryl in our green |
| 4 Deep stone (112.5–150 m) | ruby, diamond | Crystal Caverns ruby; quartz in our clear white |

- **Order:** the ladder keeps its value order (coal cheapest, diamond dearest) and today's find rate.
- **Bands:** the core bands move, so each zone's pair is the bulk of what that zone yields. Iron, silver and gold
  cost a little more, so value still rises zone by zone (Changes).
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

## Changes (as built)

- **`art/minerals/catalog.json`:**
  - Core bands follow the zones, each overlapping the next by a metre or two so no metre runs thin: coal 1–5.5 m,
    copper 5–37, iron 34–58, silver 56–76, gold 75–98, emerald 96–113, ruby 112–132, diamond 131–149.2.
  - Scatter stays wide (the odd valuable high up). The ground gems keep 95% in their cores (86% before), so their
    higher prices do not inflate the shallow zone.
  - Prices: coal $4, copper $5, iron $8, silver $12, gold $16, emerald $30, ruby $45, diamond $70 (were $6, $9,
    $13 and $20, $30, $45).
    - With gold gone to zone 3, zone 2 rested on iron and silver; at the old prices it averaged barely more than
      zone 1.
    - Now each zone averages about 1.5x the one above or more: $7, $10.4, $22.9, $56 a find at seed 0.
  - Coal is 0.42 m long, about its old size.
  - `MineralSetup`'s checks stay (eight names, rising value and core depth). Its looks come from a model (coal)
    or from `prefab` and `prop_looks` (the rest).
- **Coal:** the photo rock's model and collider with a coal map (`art/minerals/make_coal.py`, from the rock's own
  maps: its grain stretched into near-black, a faint cold cast, a dull sheen).
- **`BuriedPropsSetup`:**
  - `PackStyle.Ore` for the ore chunks: 512 px maps, normal relief at 70%, half the pack's smoothness.
  - `GroundCrystalProps`: dirty copies of beryl, ruby, quartz and pyrite (`CrystalCaverns/Dirty`). Their colour
    maps come from `art/pure-nature-crystal-caverns/make_dirty.py`, the site's soil caked over each crystal and
    darkened into its crevices. Smoothness is 0.35.
  - Pyrite is tinted brass, at 0.6 metal.
- **Pyrite:** `art/pure-nature-crystal-caverns/ground.json`, a prop find (`PropFindSetup`) of 150 at $3, through
  zone 2.
- **Treasure:** the chest crystals stay their own items, clean, at chest size and cheaper than the ground gems.
- **Tests:** `DiscoveryCatalogTests` count and shallow lists; the depth-mix test passes on the new bands.
- **Docs:** concept `05` §1, baseline, art cards (`minerals`, `mining-pack`, crystal caverns), the `106` playtest
  note.

## Picks (settled)

1. Pyrite is in, as zone 2's cheap "fool's gold" (agent's lean, the user went with the suggestions).
2. Deep gems and chest crystals stay apart. The agent had leaned to one item per kind. The user found it "weird
   smaller crystals cost more than bigger crystals", so the big, dirty ground gems cost more than the small,
   clean chest crystals.
3. The crystal cavern is `115`, after `110`.

## Acceptance

- The catalog bands match the zone table, and each zone's pair makes up most of its finds.
- Every mineral, gem and pyrite has a bought look or a project edit of one, with LODs and a hull in budget.
- The chests' treasure lies nowhere else.
- Tests pass; a playtest walks one shaft through all four zones (with admin depth help).

## Rejected or changed during the work

- **Coal from the pack's ore chunk:** it was never built. The user saw the old Blender coal ("too low poly, I don't
  like that style at all") and asked for the style of their rock, so coal is the rock itself.
- **The ores as the pack ships them:** "not bad, maybe a bit too high quality, and the light a bit too
  reflective". `PackStyle.Ore` softens and dulls them toward the rock.
- **Clean crystals in the ground:** "bigger crystals that appear as we mine... nicer to make them dirtier".
- **Coal's first map:** near-black (sRGB 0.05–0.25) lost the rock's grain. 0.07–0.35 read as grey slate, and
  0.03–0.22 over the grain's stretched range reads as coal.
- **Pyrite in the vendor colour:** it rendered near white under the dirty map, so it now takes a brass tint and some
  metal.
- **The first bands** (cores centred in each zone: coal 1–8 m, copper 8–34, iron 38–58...; scatter only about a
  zone each way, gems from 60 m) broke three of the depth rules:
  - metres 34–37 held 8 finds instead of about 45, a gap between copper's and iron's cores;
  - coal arrived late (its core spread over 7 m and its model shrank to 0.3 m);
  - no valuable turned up shallow.
- **400 pyrite:** with iron and silver at $6 and $9, they dragged zone 2's average below 1.4x zone 1's.

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

## Iteration (user, 2026-10-06, after a first look)

- "Coal is decent but looks too much like a rock now": coal moved off the photo rock onto the Mining pack's layered
  rocks (large and small, two looks), black at 0.17 with a dull sheen (0.5). It reads as a blocky lump with bedding
  planes.
  - Their meshes are copied into `Content/BuriedProps/MiningPack/Rocks` and their maps into 1024 px PNGs
    (`art/mining-pack/make_maps.py`). The pack's 4K rock maps, 160 MB a set, stay out of the repository.
  - The photo-rock coal (`art/minerals/coal`, `make_coal.py`) and the retired Blender mineral sources are deleted, and
    `MineralSetup` builds prefab looks only.
- "Iron looks like lava": its colour map's orange veins are turned a dull rust and the rock a little cooler
  (`Ore_Iron_Rust.png`, `make_maps.py`).
- "Silver could be higher quality": the silver ore keeps the pack's maps and gloss (`PackStyle.Pack`).
- "Copper is a rock plus copper; wonder if pure copper is better": copper's second look is now a native copper
  nugget, the pack's large jagged rock in copper metal (0.7 metal, 0.55 gloss). It is an A/B with the ore chunk:
  both appear in play and side by side in the gallery. Each prop look may carry its own `model_scale`.
- "The crystals look like they have black spots instead of dust": the soil caked on the dirty crystals was tinted by
  the crystal's dark colour. The colour is now baked into the map with a pale dust film (`make_dirty.py`), and the
  dirty materials draw it in white.
- Found by the tests: nearby pickup's sight line went to the find's exact nearest point, which on the layered coal is
  an edge the ray grazed past. It now aims 3 cm inside the find (`FindProximityCollection.EdgeInset`).

## Iteration 2 (user, 2026-10-06)

- **Copper:** "fine to be rock and copper in one, but I need more copper on it": the native nugget A/B is dropped and
  both looks are the pack's ore chunks on `Ore_Copper_Rich.png` (`make_maps.py`: the pack's copper flecks grown and
  joined by soft patches to about half the stone, 22% before, in the pack's own copper colour, with its metal and
  sheen written into the mask).
- **Iron, silver and gold** ("can be full gold/silver"): solid native nuggets on the pack's large and small jagged
  rocks (`RockFinds`, scale 0.15 and 0.22, about the ore chunks' size), each material the rock's light and dark only
  (`Rocks/<set>_Metal.png`: luminance over its mean, kept within 0.6–1.1) in the metal's colour. Iron (0.46, 0.44,
  0.43; metal 0.85, smoothness 0.5) first read as plain grey rock at metal 0.6; silver (0.86, 0.87, 0.89; 0.85,
  0.6); gold (1, 0.77, 0.34; 0.85, 0.62). The pack's iron, silver and gold ores, the rust map and their vendor files
  in the repository are gone (`M_Ore_Iron` stays: the ore chunk models reference it).

## Iteration 3 (user, 2026-10-06: gold "looks like fake gold or gold wrappers")

- The jagged rock's facets with a high gloss read as crumpled foil. Gold moved to the pack's rounded rocks (large
  at scale 0.26, small at 0.36, about the other nuggets' size) with smoothness 0.48: a water-worn lump. Open to the
  user's next look.

## Iteration 4 (user, 2026-10-06: some minerals "can be larger ... closer to the rock size")

- Plain rocks are about 0.5 x 0.3 x 0.4 m. Iron and silver nuggets grew 1.3 times (to about 0.34 x 0.22 x 0.53 m),
  pyrite 1.8 times (about 0.36 x 0.43 x 0.38 m) and gold 1.15 times.
