# 111 — Zone Main Grounds

**Status:** planned after `026`. Spec written ahead (2026-10-05); re-check against the code when it
starts. Plan and research: [107](completed/107-asset-only-grounds.md). Two playtest steps: **111.1** zone 2's
ground, **111.2** zone 3's ground. Zone 4's ancient material stays with `039`.

## Objective

Going deeper changes the ground. Below the recent fill (zone 1, soil), zone 2 turns into **old lake
sediment** and zone 3 into an **old riverbed of sand and gravel**. That is the real order under a
drained lake: recent mud, then older grey lake silt and clay, then the sands and gravels of the river
that ran there before, then bedrock. Each ground is built from an owned pack texture, learned over a
whole zone, and blended into the next over a few metres. Arriving in a new zone is an event, never a
restart.

## Concept reference

- **`03` §3 "Why one main ground per zone":**
  - a zone that is mostly one ground lets the player learn how it digs, what it hides and what it
    looks like;
  - the first generator's thin repeating stack made materials meaningless.
- **Zone rules:**
  - **No zone feels like a restart:** at the tool level a player typically owns on arrival, the
    zone's main ground digs no slower than the previous zone felt near its end (Meltopia's Tesla snow).
  - **Never a wall.**
  - **Variety inside a zone:** no glaring pale surfaces (Keep Digging's white rock layer).
  - **Edge bands:** a short mixed band where zones meet.
  - **Mixed spots only with a job.**
- **`03` §2:** a diggable ground never looks like the bedrock shelf or the retaining walls.
- **`03` §4 (after `107`):** hardness shows as bite size and a little as rhythm; the ground's look and
  grain carry it, never colour alone.
- **`02` §3:** the campaign arc runs through four zones over 3–5 hours.
- **`05` §1:** host ground is a soft bias with scatter, and prices are fixed per type.

## Live codebase analysis (after `110`)

- **Depth bands:** `TerrainGround.ZoneBorders` (37.5, 75, 112.5 m) and `ZoneAt` remain as plain depth
  bands. `GroundJob.Material` returns Backfill in pits, GeodeShell in shells and Soil elsewhere.
- **Mesher, shader and X-ray:** the mesher's weight stream has free slots, and the shader's
  `DepositSurface` blends any deposit.
- **Tuning:** `EquipmentProgression` holds per-ground responses and the admin dials;
  `GroundEffect` holds the texts.
- **Host grounds:** host-ground fields and the `HostWeight` centre lookup remain unused.
- **Owned candidates:**
  - Zone 2:
    - Highlands `Mud` (grey-brown);
    - Mountains `Mud02`;
    - Crystal Caverns `GroundDirt` (normal and mask only).
  - Zone 3:
    - Highlands `Sand` and `Sand_rubble`;
    - Mountains `Gravel` and `Gravel2` (the lakebed's `PackedSediment` layer already uses `Gravel`,
      so the ground below matches the bed above).
- **Derivation:** `make_backfill.py` is the pattern for turning a pack texture into a dig-ground set.
- **Lighting:** below about 20 m everything is dark, so grounds are judged under lamps.

## Design

1. **111.1, zone 2 "old lake sediment":**
   - a dense grey-brown silt and clay with fine layering, derived from the chosen owned base;
   - the user picks from lamp-lit Ground Lab variants;
   - its response is a little firmer than soil, but within the no-restart rule at the expected tool
     level on arrival.
2. **111.2, zone 3 "old riverbed":**
   - sand and gravel, with pebbles visible in the wall;
   - the base and response are chosen the same way;
   - it must not look like the zone 3 geode shells or the bedrock shelf.
3. **Expected tool level per zone:** written into `EquipmentProgression` next to the responses, as
   the old comment "a zone's main ground is matched two purchases later" intended. A test checks the
   no-restart rule for every zone ground.
4. **Blend band:**
   - the border between zones is warped by low-frequency noise so it never reads as a flat line;
   - over about 2–4 m the two grounds mix in noisy patches (Minecraft's deepslate band);
   - the band is generated per sample, so cuts show a gradual change.
5. **Generation:** `GroundJob.Material` picks the zone's main ground from depth and the blend band.
   Pits stay in zone 1, and geodes keep their shells in whatever ground holds them.
6. **Find family:** host weights for existing finds where it makes sense (the ore ladder's deeper
   steps in zone 2–3 grounds). New families come with `011`/`012`, and container styles per zone
   with `014`.
7. **Tools:**
   - a Ground Lab bay per ground, plus one bay that shows a zone border blend;
   - Ground X-ray does not mark main grounds (they are everywhere);
   - `GroundEffect` texts.
8. **Docs:** `03` §3's table gets the new grounds, and the queue's `013` trail types per zone are
   re-decided here.

## Edge cases

- **The 150 m site:** zone 4 below 112.5 m stays soil until `039`, so it must not look like zone 3
  leaking down. If that reads oddly in play, extend zone 3's ground to the bottom until `039`.
- **Backfill vs zone ground:** backfill is generated only in zone 1, so its rubble look never
  competes with zone 3's gravel.
- **Hardness:** a harder zone ground slows every shaft, so the no-restart test is the guard.
  Playtests judge the feel.
- **Saves:** new material ids are appended, and New Game is required (the population and ground
  change).

## Tests

- In each zone, most samples (outside geodes and pits) are its main ground, and the blend band stays
  within its width.
- The no-restart rule holds for every zone ground at its expected level.
- The material field is deterministic per seed, and restore is exact.

## Acceptance criteria

1. **111.1:** zone 2's ground is in the site, lamp-lit and distinct from soil, with a gradual border.
   The user picks the variant and the dig feel passes.
2. **111.2:** the same for zone 3.
3. For each step: compiles warning-free, tests pass, and the build and its playtest note are
   delivered.

## Playtest notes (`docs/playtests/111-zone-main-grounds.md`)

- **Try:** dig down through a zone border with the expected tool and a lamp. Compare the lab variants.
  Dig a while in the new ground.
- **Good feels like:** "it's different down here", with the tool keeping up.
