# 112 — Secret Areas Around Uniques

**Status:** planned after `111`. Spec written ahead (2026-10-05); re-check against the code when it
starts. Plan and research: [107](107-asset-only-grounds.md). `099`'s detector-off test follows it.
Two steps: **112.1** the lens, **112.2** an optional trail, decided after 112.1's playtest.

## Objective

Every unique lies inside a **lens of ground that does not belong there**: a patch of another owned
ground around it, the odd one out. The player sees and feels the change in a wall, follows it, and
finds the unique with their own eyes instead of the HUD. Real lake mud holds exactly such lenses:
pockets of sand or gravel left by an old shoreline or a flood.

## Concept reference

- **`03` §4 odd spots:** each unique sits in ground that does not match its zone, and the odd one out
  is the clue. The generator shapes this ground around the space it already reserves for each unique.
- **Tell rule (after `107`):**
  - different ground means you are on to something (felt as well as seen, never colour alone);
  - presence, never value;
  - straight digging always works;
  - tells run sideways as often as down.
- **`05` §1:** uniques exist once per save, are unsellable and are recovered whole by the crane.
- **`05` §2:** the detector is frozen. The detector-off test (`099`) asks whether testers find every
  unique without help and follow at least one tell unprompted. The user decides the detector's fate
  from the result.
- **`03` §8:** validation checks that every unique sits in its lens.

## Live codebase analysis (after `111`)

- **Unique spaces:**
  - `TerrainGround.OddSpot` keeps each unique's space (Centre, Half, Min, Max), from
    `DiscoveryCatalog.OddSpots()`: the authored position, `PlacementRadius` and soil clearance.
  - `TerrainVolume.oddSpots` is synced by Sync Discovery Models.
  - Pits avoid them.
- **Current uniques:** three retro computers at about 8, 13.5 and 21 m, all in zone 1 (soil).
- **Grounds:** the zone grounds from `111` (zone 2 silt, zone 3 sand or gravel) exist with textures,
  responses and weight slots.
- **What was removed:** `107` deleted the old odd-spot lens generation (`TerrainGround` odd-spot
  ground and the `Ground` field). It is recoverable from commit `566009e` as a reference.

## Design

### 112.1 The lens

- **Lens ground:** each unique's space is wrapped in a lens of a ground that does not match its zone:
  - zone 1 (soil) holds a **sand or gravel lens** using `111.2`'s ground;
  - zone 2 holds a sand lens;
  - zone 3 holds a silt lens.
  - The rule is "a ground from another zone"; no new ground is invented.
- **Shape:** a flattened ellipsoid about 2.5–4 m across around the reserved space, with a noisy edge
  (the same warp as the zone blend), so its curve reads in a wall from either side.
- **Feel:** the lens keeps its ground's own response, so its dig speed differs from the surroundings.
  If the lens ground digs about the same as its host, its response is nudged so the difference is
  felt (sand easier, for example).
- **Generation:**
  - `GroundJob.Material` writes the lens inside each `OddSpot` volume;
  - pits and geodes already avoid unique spaces;
  - validation checks that every unique is enclosed by its lens.
- **X-ray** marks lenses for development.

### 112.2 A trail (only if 112.1's playtest finds lenses too hard to meet)

A thin, winding stringer of the same ground leads several metres from the lens toward the plot's
middle, so a shaft or tunnel is more likely to meet it and follow it in. It is the ground version of
Dome Keeper's wires.

## Edge cases

- **A unique near a zone border** takes the lens of the zone its centre lies in.
- **A lens must never isolate the unique** from the crane's route; it is ordinary diggable ground.
- **The lens never reaches the surface soil bank** or the plot edge.
- **Saves:** material ids only (the lens is generated). New Game is required.

## Tests

- Every unique's reserved space is fully inside its lens.
- The lens ground differs from the host zone's main ground.
- Generation is deterministic per seed.

## Acceptance criteria

1. Every unique in a new site sits in a lens that reads in a lamp-lit wall and digs differently.
2. Compiles warning-free, tests pass, and the build and its playtest note are delivered.
3. Then `099`'s detector-off playtest runs, and the user decides the detector's fate.

## Playtest notes (`docs/playtests/112-secret-areas-around-uniques.md`)

- **Try:** without Ground X-ray, dig the first 25 m sideways as well as down. Notice a lens in a wall
  and follow it to the unique.
- **Good feels like:** "that's not the same ground... something's in there."
