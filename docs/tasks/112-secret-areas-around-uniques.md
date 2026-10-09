# 112 — Secret Areas Around Uniques

**Status:** 112.1 (the lens) built; awaiting its playtest, which decides whether **112.2** (a trail) is
needed. Plan and research: [107](completed/107-asset-only-grounds.md). `099`'s detector-off test follows.

## Objective

Every unique lies inside a **lens of ground that does not belong there**: a patch of another zone's
ground around it, the odd one out. The player sees and feels the change in a wall, follows it, and
finds the unique with their own eyes instead of the HUD. Real lake mud holds exactly such lenses:
pockets of sand or gravel left by an old shoreline, channel or flood.

## Concept reference

- **`03` §4 lenses around uniques:** each unique lies in a lens of another zone's ground; the odd one
  out is the clue. The generator shapes it around the space it already reserves for each unique.
- **`03` §4 tell rules:** the ground changes and following the change leads somewhere; felt as well as
  seen (dig speed, grain and stones, never colour alone); presence, never value; straight digging
  always works; tells run sideways as often as down; generated from the seed and stored only as
  material IDs.
- **`03` §3:** mixed spots only with a job (a lens around a unique is one); never a wall.
- **`03` §6, §8:** lenses are per-save shape and position, the same material/density data, no
  separate save record; validation checks every unique sits in its lens.
- **`05` §1–2:** uniques exist once per save, are unsellable and recovered by the crane. The HUD
  detector is frozen; nothing here may depend on it. `099` asks whether testers find every unique
  without help and follow at least one tell unprompted.

## Live codebase analysis

- **Unique spaces:** `TerrainGround.OddSpot` (Centre, Half, Min, Max) from `DiscoveryCatalog.OddSpots()`
  (authored position, `PlacementRadius` + `SoilClearance`), copied to `TerrainVolume.oddSpots` by Sync
  Discovery Models. The three computers have a 0.9 m envelope at 8, 13.5 and 21 m, all in zone 1
  (soil); a space reaches 1.8 m sideways and 1.5 m up and down, its box 1.2 times that.
- **Who keeps clear of a space:** pits (+1 m), geodes (+1 m), great and mini caves (+1.5 m, a great
  cave dropping only the chambers it blocks), ordinary finds (`OddSpotReach` past the envelope).
- **Ground generation:** `TerrainGround.Generate` runs the Burst `GroundJob` (surface soil, then pits,
  then the zone ground per sample), then main-thread passes for the chest shells, caves and geodes.
  `GroundLayout` hands pits, stashes, geodes and caves to placement and the Ground Lab replaces it.
- **Grounds:** soil, backfill (≈¾ of soil's speed), lake sediment (a little firmer than soil),
  riverbed (≈⅔ of soil's speed, the bite motion, warm brown with water-worn pebbles).
- **Host ground:** find weights read the actual material at a find's centre (`MaterialAtLocal`).
- **Ground X-ray** marks backfill, geode shell and cave rock cells; it cannot tell a lens from its zone
  ground by material alone.
- **Removed earlier:** `107` deleted the old lens (commit `566009e`): an ellipsoid only 0.9 m past the
  envelope, about 3.5 m across, barely bigger than the computer's own reveal.

## Design (112.1, the lens)

### Shape: wide, flat, thickest at the unique

The planned 2.5–4 m lens would be barely larger than the unique itself, so it would add almost no
reach. A real lens is lenticular, much wider than thick, and pinches out at its rim. That shape is
also the direction cue:

- **Outline:** an ellipse 9–11 m long and 7–8.4 m wide (`LensLong`, `LensShort`) at a seeded heading,
  its rim wobbling ±15 % (`LensWobble`), centred on the unique.
- **Thickness:** 2 × (envelope + `LensCover`), 3 m at the unique for the computers, thinning as
  `1 − q` (q the squared ellipse radius) to nothing at the rim: about 0.5 m thick at 80 % of the way
  out. A wall that cuts its edge shows a thin band; **following it where it thickens leads to the
  unique**. Its faces are roughened ±0.12 m (`LensRough`) so the band edge is ragged like a zone border.
- **Mid-plane:** tilted up to 4° along its long axis (`LensDip`) and gently warped (`LensWarp`), the
  warp fading to zero at the unique so it stays mid-lens.
- **Reach estimate:** a random shaft meets one of the three lenses about 9 % of the time each, about
  27 % for one of them; wide pits and tunnels meet them more. If testers still miss them, 112.2 adds
  the trail.

### Ground

`TerrainGround.LensGround(host)`: the riverbed, unless the host is the riverbed, then lake sediment.
The host is the zone ground at the unique's centre (a unique near a border takes its centre's zone).

- Zone 1 (soil): a **riverbed lens**, a gravel lens from a flood or old channel. Felt (⅔ of soil's
  speed with the bite motion) and seen (warm brown, pebbled). Lake sediment was rejected for the
  recent fill: it digs nearly like soil, so the felt tell would be lost in the dark.
- Zone 2 (lake sediment): riverbed (slower). Zone 3 (riverbed): lake sediment (faster).
- No ground needs its response nudged; no new material.
- **Risk:** riverbed and backfill are both stony and slower. They differ in shape (a flat band versus
  a steep column), colour (warm brown versus darker, greyer turned soil) and stones (rounded pebbles
  versus dirty rubble). The playtest note asks whether a lens ever passed for a pit.

### Generation and clearances

- `TerrainGround.Lens` (Burst-friendly struct: centre, bounds, radii, heading, half-thickness, dip,
  noise seed, ground) and `TerrainGround.Lenses(...)`, one per unique space, from seeded draws per
  unique. `InLens(lens, p)` is the one predicate for the job, the X-ray and tests.
- New feature flag `Features.Lenses` (in `All`, so the site admits it through `SiteLayout.Ground`).
- `GroundJob` precedence: surface soil (top 1.1 m), pits, lenses, zone ground; chest shells, caves and
  geodes still overwrite afterwards.
- **Pits and geodes** keep clear of the lens box (each lens's tight box grown into the space box they
  already avoid), so each tell leads to one thing.
- **Caves** still avoid only the unique's space: a lens-sized block would drop up to four chambers of
  the zone-1 hall, which lies just below the deepest unique, and it halved zone 1's mini caves (55 of
  120 over 40 seeds), which share the lenses' depths. Where a cave's shell meets a lens rim, its
  stone wins; on the seeds measured no cave touched a lens.
- **Order (`TerrainGround.Plan`):** lenses, great caves, pits (clear of the lenses and of the halls'
  chambers, `NearHall`), geodes, mini caves. Found while building: pits were placed before the great
  caves and blocked the zone-1 hall's chambers, so 28 of 40 seeds had **no zone-1 great cave** (40 of
  40 with pits off). The lenses pushed pits deeper and made it 37 of 40. Laying the halls first gives
  every zone its hall on all 40 seeds, with every pit still placed.
- Ordinary finds still lie in the lens (outside the unique's own space). Host weights follow the actual
  ground; the three lenses hold about 290 m³ in all, too little to shift the find mix noticeably.
- `GroundLayout.Lenses`; the Ground Lab's layout has none.

### Developer X-ray

`XrayGround.Lens` (green) marks cells that are inside a lens by `InLens` and hold its ground.

## Edge cases

- **Near a zone border:** the centre's zone decides; a lens crossing into a zone of its own ground
  merges with it (no current unique is within 15 m of a border).
- **Never isolates the unique:** ordinary diggable ground; the crane's rope tears it like any ground.
- **Surface:** the top 1.1 m stays soil; the shallowest lens top is about 6 m down.
- **Plot edge and grid walls:** the lens may run a little under the permanent ground below the bank;
  samples outside the grid are simply absent.
- **Fixtures, test grids, Ground Lab:** no lenses (features `None` or the lab's own layout).
- **Saves:** material IDs only. Existing saves keep their old ground; New Game shows the lenses.

## Tests

- Every unique's envelope plus a margin lies wholly in lens ground, for several seeds.
- The lens ground differs from the host zone's ground; the lens reaches several metres sideways and
  is thicker at the unique than towards its rim.
- Pits and geodes keep clear of the lens boxes; the same seed gives the same lenses.
- Every zone gets a great cave over a dozen seeds, the pits clear of its chambers.

## Verification (2026-10-09)

- Seed 2718 with the catalog computers: three riverbed lenses of about 95 m³; thickness along the
  long axis 3.1, 2.9, 2.6, 2.1, 1.4, 0.5 and 0 m at 0–6 m out; every sample within the envelope plus
  0.25 m is lens ground (seeds 2718, 12, 991). Ground generation 0.7 s, as before.
- A cutaway beside the 8 m lens reads as a lenticular patch of stony ground in the brown soil wall;
  Ground X-ray marks it green (`Logs/qa/112/`).

## Acceptance criteria

1. Every unique in a new site sits in a riverbed lens about 10 m across that reads in a lamp-lit wall,
   digs slower than the soil, and thickens toward the unique.
2. Ground X-ray marks lenses in green.
3. Compiles warning-free; the session's closing test run passes; build and playtest note delivered.
4. The playtest decides 112.2; then `099` runs and the user decides the detector's fate.

## 112.2 A trail (only if the playtest finds lenses too hard to meet)

A thin, winding stringer of the same ground leads several metres from the lens toward the plot's
middle, so a shaft or tunnel is more likely to meet it and follow it in: the ground version of Dome
Keeper's wires.
