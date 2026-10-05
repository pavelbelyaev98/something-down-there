# 110 — Geodes

**Status:** planned after `109`; `026` (C4) follows it. Spec written ahead (2026-10-05); re-check
against the code when it starts. Plan and research: [107](107-asset-only-grounds.md). Two playtest
steps: **110.1** shell, hollow and break-in; **110.2** crystals.

## Objective

Deep in the site lie a few **geodes**:
- **The shell:** a ball of hard rock, hollow inside and lined with crystals. Its curved face shows in
  a tunnel wall and tells the player something is in there.
- **Getting in:** the drill crawls through the shell in seconds, and C4 (`026`) cracks it fast.
- **Inside:** the wall gives onto a dark, sealed hollow, and the lamp lights crystals worth far more
  than anything around them.

Geodes are the user's "cave-like areas" and "harder ground where C4 is the better tool", grounded in
the real thing. Keokuk geodes are dug out of river-bed limestone and shale along the Mississippi. The
Pulpí geode in Spain is big enough to walk into.

## Concept reference

- **`03` §1 "No caves or tunnel networks":** every passage is one the player dug, and the only
  pre-existing air is a few small sealed pockets the player breaks into. After `107` those are the
  chests' hollows. This ticket adds geode hollows: small, never connected, never a network.
- **`03` §2:** "tough but diggable" never looks like an eternal wall. The shell must never read as
  the bedrock shelf or the retaining walls.
- **`03` §3 zone rules:** never a wall, no zone feels like a restart, and mixed spots only with a job.
  A geode is optional and small; a shaft that meets one goes around it or through it.
- **`03` §4 tell rule (after `107`):** different ground means you are on to something. The shell is
  the tell: felt as very hard and seen as a curved face of distinct rock. It never relies on colour
  alone.
- **`03` §7 true darkness:** sealed air stays dark until the player's opening or a lamp lights it.
  Daylight only travels through connected air.
- **`03` §8:** generated from the seed; validation checks that every geode is closed until broken
  into and reachable with baseline equipment.
- **`04` §8 C4:** an optional accelerator, never the only way; a big, predictable blast; finds
  survive. Geode shells are C4's natural target (reaction in `026`).
- **`05` §1:** depth pays through the mix, never a price bonus. Each type has a fixed price, and
  deeper geodes hold richer crystal types. Uniques are not involved.
- **`05` §3 exposure rule:** every find is seen before it is collected. Crystals in a lit hollow are
  seen first.
- **`06` §3:** a rare find affords one big upgrade, not half the tree.

## Live codebase analysis (after `107`)

- **Seeded air:**
  - `ExcavationGrid.Reset` carves pockets as seeded air (`CarveHollow`: an oriented SDF box in a local
    frame), from data passed into the grid.
  - `TerrainVolume.MaterializeSeededAir`/`MaterializeAround` build chunks around them before they are
    opened.
  - Seeded air counts as modified for restore and adds no `RemovedVolume`.
- **Layout:**
  - `TerrainGround.Stashes`/`Stash` and `GroundLayout` are the pattern for a `Geodes` list with
    bounds.
  - `GroundLayout.KeepOut()` is the empty reservation hook left by `107`.
  - `GroundJob.Material` writes a material inside a shape, as `InPit` does.
- **Seats:**
  - Pit and chest seats claim finds from the population first (`DiscoveryCatalog.Generate` 197-215,
    `SeatChests`, `DiscoveryField.Generate` 354-364).
  - Junk seats take only `Junk` entries.
  - Seats carry no orientation.
- **Anchoring:** `BuriedFind.CanReleaseFromSoil` keeps a find anchored while its pivot or any
  exposure sample is inside solid ground. A crystal whose base is sunk into the shell therefore holds
  until the wall around it is dug. Seeded air counts as exposure.
- **Darkness:** `ExcavationDaylightGrid` lights only air connected to the surface
  (`ExcavationDaylightTests.SideBoundaryAndSealedChambersDoNotAdmitSky`).
- **Break-in:** deleted in `107`. Rebuild it from commit `566009e`, `TerrainVolume.Rooms.cs`
  (`CheckBreakIn`: the first cut that clears a sample within 1.5 cells outside the air box fires once
  per pocket) and `EmitBreakIn` with the particle pool in `TerrainVolume.Release.cs`. That code kept
  its opened state for the session only.
- **Mesher, shader and X-ray:** one weight stream with free slots, generic `DepositSurface`, and
  X-ray marks per ground (after `107`). `ToolRigPresenter.MotionFamily.Hard` exists for hard grounds.
- **Assets:**
  - Crystal Caverns gives crystal and mineral models (quartz, pyrite, fluorite, beryl, cobalt, ruby,
    gemstone, prism, big hexagon, giant crystal) with LODs.
  - It also gives cave rock models (walls, surfaces, big blocks) and tiling `_RockDetail1/2` and
    `GroundDirt` maps.
  - Its own crystal shader is on the pre-6.1 URP API.
  - Underground props must use URP Lit materials so `ExcavationDaylight` can swap them (the
    `BuriedPropsSetup`/`MineralSetup` pattern).

## Design

### 110.1 Shell, hollow and break-in

- **Ground:** `TerrainMaterialId.GeodeShell` is appended, taking a weight slot, an X-ray mark and a
  Ground Lab bay.
- **Hardness:** about **a quarter of soil's dig rate**, with the `Hard` motion family. Going through
  the shell takes seconds with the tool expected at that depth, never minutes; tune in the Ground Lab
  with the admin dials.
- **Generation (`TerrainGround.Geodes`, new `Features.Geodes`, on the site):**
  - **Where:** zones 2–3 only for now; zone 4 belongs to `039`. About two in zone 2 and three in
    zone 3 (constants).
  - **Shape:**
    - the hollow is a slightly flattened ellipsoid about 1.8–2.6 m across, big enough to crouch
      inside;
    - the shell is about 0.6–0.9 m thick;
    - the outer surface is lumpy (low-frequency noise) and the inner one smoother;
    - each geode gets a seeded rotation.
  - **Clearance:** inside the find footprint, and clear of:
    - uniques' spaces;
    - pits and stashes;
    - the side walls and the bottom shelf;
    - each other (several metres apart).
  - **Never in the way:** there is no "main shaft" to keep clear, because the player digs anywhere. A
    geode is small enough to pass around. Meeting one is a reward, not a block.
  - `GroundLayout` gains `Geodes`, and `KeepOut()` returns their outer bounds so ordinary finds stay
    out of the shell.
  - `GroundJob` writes GeodeShell between the outer and inner surfaces.
  - `ExcavationGrid.Reset` carves each hollow as seeded air (an ellipsoid SDF next to `CarveHollow`),
    and `MaterializeSeededAir` covers geodes.
- **Break-in (rebuilt from history):**
  - A pocket record (centre, local frame, air radii, bounds) replaces the old `Room`.
  - The first cut that opens the hollow fires `BrokeIntoPocket` once and drifts a little dust into the
    dark (`EmitBreakIn` and its particle pool).
  - The opened state is derived on load: a pocket whose surrounding samples are already air counts as
    opened, so dust never replays.
- **Shell look:** built from Crystal Caverns rock: `_RockDetail2`/`_RockDetail1`, or a cave-surface
  model's maps made tileable by a script beside `make_backfill.py`.
  - It must read as a distinct, dense grey stone, never as the bedrock shelf or the concrete walls.
  - The user picks from **A/B variants in the lamp-lit Ground Lab**.
- **Ground Lab:** a geode bay, with one whole geode within reach.

### 110.2 Crystals

- **Crystal finds:** a new catalog source, `art/geode-crystals/catalog.json`.
  - It holds real-mineral crystal types from Crystal Caverns models. Candidates:
    - quartz cluster, pyrite cluster and fluorite for zone 2;
    - beryl, cobalt-blue crystal, ruby-red gemstone and prism for zone 3.
  - The names stay distinct from the common ore ladder (no second "ruby").
  - Each type has a fixed price, several times the common minerals at the same depth; prices live in
    the catalog.
  - Every crystal type is flagged `geode` (like `junk`), so geode seats take only crystals and
    crystals appear nowhere else.
  - The final roster is agreed with the user before the sync.
- **Lining:** each hollow holds about five to eight crystals spread over its inner surface (most on
  the floor and lower walls, a few on the walls and ceiling).
  - **Orientation:** seats gain an orientation, and each crystal points into the hollow.
  - **Anchoring:** each is sunk so its base sits inside the shell, and stays anchored until the wall
    around it is dug.
  - **Population:** seats draw from the finite population (the `SeatChests` pattern), so nothing is
    added on top.
- **Everything that looks like a crystal is a find.** There is no non-collectible decoration, so the
  player never tries to take something that refuses.
- **Collection:** crystals are exposed in the hollow, so aiming and holding dig collects them, and
  proximity collection works once the player is inside. They are seen in the lamp's light first.
- **Materials:** project-owned URP Lit prefab variants under `Content/Discoveries/Geode/`, built from
  the pack's crystal textures (`BuriedPropsSetup` pattern). They have high smoothness for sparkle
  under lamps, and `ExcavationDaylight` swaps them like every buried prop.
  - **A/B variant:** none vs a faint emissive glow. Real quartz does not glow, but Meltopia's
    sparkle shows the pull. The user names the winner; the loser is removed.
- **Art card:** `art/pure-nature-crystal-caverns/README.md` lists every derived material, prefab and
  texture.

## Edge cases

- **Uniques and pits:** a unique's space or a pit never overlaps a shell; generation rejects that
  candidate.
- **The player digs into the hollow from above** and drops in: the hollow is about 2 m, and landing is
  harmless (no fall damage).
- **A crystal loosened by digging** falls and is collected like any find. One seated at the very top
  falls when its base is freed.
- **Full bag:** crystals stay anchored or loose in the hollow, and nothing is lost.
- **Save inside a half-opened geode:** voxels, finds and their states restore, and the break-in dust
  does not replay.
- **Ground Lab session:** the site's geodes return exactly afterwards.
- **Performance:** each geode materializes its chunks up front, and five geodes plus the stash
  hollows stay within the first-dig budget (`088`).

## Tests

- **Generation:**
  - geodes sit in zones 2–3 inside the footprint, clear of uniques' spaces, pits and each other;
  - every hollow is fully enclosed by shell (no seeded air touches soil);
  - generation is deterministic per seed.
- **Grid:** hollows are closed seeded air and count no removed volume.
- **Break-in** fires once per geode, and not again after save and load.
- **Daylight:** a sealed geode stays unlit until opened (existing pattern).
- **Seats:** every crystal seat is filled from the population with a crystal type and anchored. The
  population total is unchanged.
- **Response:** GeodeShell digs at 15–35% of soil's rate at every tool level.

## Acceptance criteria

1. **110.1:**
   - New Game holds the geodes. The shell reads as distinct stone in a lamp-lit wall and digs clearly
     harder than soil, but takes seconds.
   - Breaking in shows a dark, sealed hollow with a small dust drift.
   - The user picks the shell texture.
2. **110.2:**
   - Every geode holds its crystals, anchored and pointing in, and they sparkle under a lamp.
   - Prices sit well above the surrounding finds.
   - The user names the glow A/B winner.
3. For each step: compiles warning-free, tests pass, and the build and its playtest note are
   delivered.

## Playtest notes (`docs/playtests/110-geodes.md`)

- **110.1:**
  1. Use Ground X-ray to find a geode (or the lab bay).
  2. Dig into its shell with the expected tool, then break in and light it with a lamp.
  3. Compare the shell variants.
- **110.2:**
  1. Break into a geode and light it.
  2. Collect the crystals and compare the glow variants.
  3. Sell them.
- **Good feels like:** "a hard ball of rock... something is in there", then darkness, a lamp, and a
  sparkle that pays.
