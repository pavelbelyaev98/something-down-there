# 110 — Geodes

**Status:** complete:
- Five geodes, sealed hollows a few metres across (an ellipsoid smoothly joined to side lobes and warped) in a
  shell of hard stone at about a quarter of soil's dig rate, lie in zones 2–3. The first way in opens each one
  once, with crumbs and dust.
- Ten crystals of one kind line each hollow, from the population: celestine and fluorite in the sediment,
  amethyst and citrine deeper.
- The shell is Crystal Caverns' porous rock detail graded dark grey, which won its A/B. The crystals don't glow.

Plan and research: [107](107-asset-only-grounds.md).

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

## User direction

- 2026-10-06: "Implement some more tickets from what is planned so I have more to review."
- 2026-10-06: "For things you need me to decide A or B, create both and make admin variations."
- 2026-10-06 (`114`): gems in chests and "at the bottom area somehow"; geodes hold crystal hollows in
  zones 2–3, the crystal cavern (`115`) the bottom.
- Earlier: the minerals must look like the user's photo rock (plain, matte), never too detailed or
  reflective.

## Concept reference

- **`03` §1 "No caves or tunnel networks":** every passage is one the player dug. The only pre-existing
  air is a few small sealed pockets the player breaks into: the chests' pockets and now geode hollows.
  They are small, never connected, never a network.
- **`03` §2:** "tough but diggable" never looks like an eternal wall. The shell must never read as the
  bedrock shelf or the retaining walls.
- **`03` §3 zone rules:** never a wall, no zone feels like a restart, and mixed spots only with a job. A
  geode is optional and small; a shaft that meets one goes around it or through it.
- **`03` §4 tell rule:** different ground means you are on to something. The shell is the tell: felt as
  very hard and seen as a curved face of distinct rock. It never relies on colour alone (grain and the
  curve differ too). Tells are world data, identical after reload.
- **`03` §5 geodes:**
  - A few lie in zones 2–3, small and off the main descent.
  - The drill crawls through the shell in seconds.
  - It is always sealed and dark until the player's opening or a lamp lights it.
  - Its crystals are finds worth far more than the ground around them, richer the deeper the geode.
  - Geodes never connect into passages.
- **`03` §7 true darkness:** sealed air stays dark. Daylight only travels through connected air.
- **`03` §8:** generated from the seed. Every geode is closed until broken into and reachable with
  baseline equipment.
- **`03` §9:** nothing harms, buries or traps the player. The break-in drops dust, nothing else.
- **`04` §8 C4:** geode shells are C4's natural target (its reaction lands in `026`).
- **`05` §1:**
  - Depth pays through the mix, never a price bonus.
  - Each type has a fixed price, and deeper geodes hold richer crystal types.
  - Since `114` the ground's gems are dirty beryl (emerald), ruby and quartz (diamond), and pyrite is
    fool's gold. The chests hold the clean ones. Geode crystals are other minerals.
- **`05` §3 exposure rule:** every find is seen before it is collected. Crystals in a lit hollow are seen
  first.
- **`06` §3:** a rare find affords one big upgrade, not half the tree.

## Live codebase analysis (2026-10-06)

- **Ground generation (`TerrainGround`):**
  - A Burst `GroundJob` writes soil and the backfill pits.
  - `FillShell` then writes each chest pocket's backfill shell on the main thread, so the job struct
    stays unchanged (stale Burst caches have bitten before).
  - `GroundLayout` holds `Pits` and `Stashes`; `Layout()` and `Generate()` derive both from the seed.
  - `Features` (`[Flags]`, `Pits`) gates what the site holds through `SiteLayout.Ground`.
- **Seeded air (`ExcavationGrid`):**
  - `Reset` carves the chests' pockets (`CarvePocket`: a signed-distance box in the chest's frame with
    lumps) and the Ground Lab's `LabCarve` boxes and tubes.
  - `TerrainVolume.MaterializeSeededAir` builds the chunks around both up front.
- **Grounds:**
  - `TerrainMaterialId { Soil, Backfill }`, with `TerrainMaterialSnapshot.Last`.
  - `EquipmentProgression.AuthoredResponse`/`HardnessOrder`/`GroundEffect`.
  - `FpsPlayer.TunableGrounds` (admin dials).
  - `ToolRigPresenter.Family`: `MotionFamily.Hard` exists unused, and the spin share follows the dig rate.
  - The X-ray marks (`TerrainVolume.XrayGround`).
  - `SalvageCrane.DebrisColors`.
  - The Ground Lab bays (`GroundLab.Bays`).
- **Look:**
  - The mesher writes one weight stream, `(free, free, free, 1 − backfill)` (`TerrainChunkMesh.Job.SurfaceMaterials`).
  - `GroundTriplanar.shader` draws backfill through `DepositSurface` from its own texture set, which
    `GroundTextureSetup` assigns from `make_backfill.py`'s output.
- **Break-in:**
  - `BuriedChest.CheckBreach` (109): the first excavation change whose open way reaches the pocket
    (the segment from the change to the pocket's nearest point is air) breaks in once.
  - Its effects use `SalvageCrane.EmitGroundBreak`. It is saved as `ChestSnapshot.Breached`.
  - `DiscoveryField` already listens to `terrain.Changed`.
- **Population:**
  - `DiscoveryCatalog.Generate` takes seats from the population.
  - `SeatChests` gives each chest seat the next unseated instance of a drawn type whose band covers the
    chest; unseated instances fall back to ordinary placement in their band.
  - Seat rotations ride in a dictionary (`chestTurns`).
  - Ordinary finds keep out of `DiscoveryReservation` spheres (the chests' `PocketReserves`).
- **Anchoring:** `BuriedFind.CanReleaseFromSoil` holds a find while its pivot or an exposure sample is
  inside solid ground, so a crystal sunk into the shell holds until the shell around it is dug.
- **Crystal art:**
  - `BuriedPropsSetup.FromCrystal` makes URP Lit copies (clean or dirty) of Crystal Caverns crystals.
  - `PropFindSetup` imports prop finds from source catalogs (`ground.json`).
  - The pack's beryl, ruby, quartz and pyrite are taken (`114`). Fluorite, cobalt, gemstone and prism
    are unused.
- **Shell textures:** Crystal Caverns' cave surfaces (`CaveSurfaceFlat_0/1`, `CaveWall_0–2`; mauve-grey
  fractured stone, 4K) are UV atlases, not tileable (seam steps about 3x the inner pixel step).
  `_RockDetail1/2` are near-white detail overlays.

## Design

### 110.1 Shell, hollow and break-in

- **Ground:**
  - `TerrainMaterialId.GeodeShell` is appended, taking the mesher's free `z` weight.
  - It also gets an X-ray mark (cyan), debris colours, a Ground Lab bay and an admin dial row.
- **Hardness:**
  - About **a quarter of soil's dig rate** at every level (`MaterialToolResponse(.68, .68, .62, 1.15)`),
    with the `Hard` motion family. The drill's bit turns that much slower in it.
  - Going through 0.6–0.9 m of shell takes seconds with the drill, never minutes.
- **Generation:**
  - `TerrainGround.Features.Geodes`, on the site.
  - `GroundLayout.Geodes`, derived from the seed like the pits.
  - Two geodes in zone 2 and three in zone 3 (`GeodesPerZone`); zone 4 is `115`'s.
  - **Shape:**
    - the hollow is an ellipsoid, 0.9–1.3 m in horizontal radius, flattened to 75–90% in height (big
      enough to crouch inside);
    - it gets a seeded yaw and a tilt of up to 15°;
    - the shell is 0.6–0.9 m thick;
    - the outer surface is lumpy (low-frequency noise, about 0.15 m), the inner one nearly smooth.
  - **Clearance:**
    - inside the find footprint;
    - wholly inside its zone;
    - clear of pits and stashes (1.5 m), uniques' spaces (1 m) and each other (centres 6 m apart
      beyond their shells).
- **Writing it:**
  - `TerrainGround.FillGeodes`, on the main thread after the job (the `FillShell` pattern), writes
    GeodeShell inside each outer surface, including the hollow's samples, so its inner face reads as
    shell.
  - `ExcavationGrid.Reset` carves each hollow as seeded air (`CarveGeode`, an ellipsoid signed
    distance).
  - `MaterializeSeededAir` covers geodes.
- **Finds keep out:** each geode reserves a sphere of its outer radius, so ordinary finds never sit in a
  shell.
- **Break-in:**
  - `DiscoveryField` keeps an opened flag per geode.
  - On each excavation change near an unopened geode, it checks whether the open way from the change
    reaches the hollow (the `CheckBreach` test against the ellipsoid). If so, the geode opens once:
    crumbs and a slow dust drift fall into the dark (`EmitGroundBreak`, light).
  - Nothing caves in: a geode is hard rock, and the chest's collapse A/B is the fill's.
  - The opened flag is derived from the grid on load and New Game: a geode with air just outside its
    hollow's surface counts as opened. The save format needs no flag. The save version still rises
    (20) because the ground changed.
- **Shell look (A/B):**
  - `art/pure-nature-crystal-caverns/make_shell.py` makes two sets from the pack's tiling rock details:
    - A, `_RockDetail1`'s porous stone graded dark grey;
    - B, the same stone with a fifth of `_RockDetail2`'s banding, graded pale cool grey (a chalcedony crust);
    - both keep `_RockDetail1`'s normal map; each is blended with its half-offset copy where its edges meet;
    - colour, normal and occlusion, each into `Content/GroundTextures`, tiled every 2 m.
  - The shader samples A or B on a global (`_GeodeShellLook`), so no material asset changes at run
    time. `GroundTextureSetup` assigns both sets.
  - Admin: **Geode shell: A / B**.

### 110.2 Crystals

- **Roster:** a prop find source, `art/pure-nature-crystal-caverns/geode.json`. These are real geode
  minerals from the pack's unused crystal families, clean and glassy (they grew in air, unlike the dirty
  ground gems). Two looks each:

  | Crystal | Model | Zone (geodes) | Price |
  |---|---|---|---|
  | Celestine | Cobalt, pale sky blue | 2 | $40 |
  | Fluorite | Fluorite, sea green and violet | 2 | $55 |
  | Amethyst | Prism, purple | 3 | $80 |
  | Citrine | Prism (quartz, like amethyst), honey gold | 3 | $110 |

  - Several times the minerals at their depth (zone 2: iron $8, silver $12; zone 3: gold $16, emerald $30).
  - One geode of six pays $250–$650, one good upgrade (`06` §3).
- **Only in geodes:** entries flagged `geode` (`DiscoveryCatalog.Entry.Geode`). Their bands are their
  zone, and their instances add up to the geodes' seats: zone 2 has 2 × 6 seats (celestine 7, fluorite
  5), zone 3 has 3 × 6 (amethyst 10, citrine 8). Counts never change; a seat failing to place falls back
  to the band like the chests'.
- **Lining (`SeatGeodes`):**
  - Six seats per geode on the hollow's surface: four on the floor and lower walls, two higher
    (seeded directions).
  - Each crystal points into the hollow (its up along the inward normal, a seeded twist) and is sunk
    a third of its height into the shell, so it stays anchored until the shell around it is dug.
  - Seat rotations ride in the same dictionary as the chests'.
- **Collection:** ordinary finds (not hand-picked). Aiming and digging take them, and proximity takes
  them once the player is inside. A full bag leaves them anchored.
- **Materials:** `BuriedPropsSetup.GeodeCrystalProps` makes one clean URP Lit material per mineral, at
  smoothness 0.8 for sparkle under a lamp. Emission stays on at 1% of the tint, invisible beside any light.
  URP's material validation switches emission off when its colour is black.
- **Glow (A/B):**
  - Admin **Geode glow: off / faint**.
  - Faint gives each geode crystal a dim emission in its own colour through a property block
    (`DiscoveryField`), so a crystal reads in the dark before the lamp reaches it.
  - Real crystals do not glow; Meltopia's sparkle shows the pull.

### Ground Lab

- A **Geode shell** bay: solid shell to 3 m, to dig and feel.
- A **Geode** bay: a whole geode 2.6 m down (`GroundLab.Geode`, the lab grid's only seeded layout) with its six
  crystals (one of each kind, then the two dearest again), reached by digging down.

## Rejected or changed during the work

- **The cave surfaces as the shell:** `CaveSurfaceFlat_1` and `CaveWall_2` are UV atlases. The gaps between their
  islands showed as black holes, like crazy paving.
- **`_RockDetail2` alone for B:** its lines read as white marble with black veins.
- **Gemstone models for citrine:** they are cut stones (a cube, brilliant cuts), and one rendered as a flat orange
  slab. Citrine is quartz, so it takes the prism clusters, as amethyst does.
- **Crystal scale:** one model scale (0.3) made celestine and amethyst clusters 0.7 m tall and citrine 0.14 m.
  Each mineral has its own scale for about 0.3–0.37 m.

## Edge cases

- **Overlaps:** uniques' spaces, pits and other geodes never overlap a shell; generation rejects the
  candidate. A failed geode leaves its crystals to their bands (tests require all five on the site).
- **The player digs into the hollow from above** and drops about 2 m: landing is harmless.
- **A crystal loosened by digging** falls and is collected like any find.
- **Full bag:** crystals stay anchored or loose in the hollow, and nothing is lost.
- **Save inside a half-opened geode:** voxels, finds and their states restore, and the opened flag is
  derived again, so the dust never replays.
- **Ground Lab session:** the site's geodes return exactly afterwards (the grid regenerates).
- **Performance:** five geodes add a few thousand material writes and five small carves at session
  start, and their chunks are materialized up front.

## Tests

- **Generation (EditMode):**
  - geodes sit in zones 2–3 inside the footprint, clear of uniques' spaces, pits and each other;
  - each hollow is enclosed by shell (every sample within 0.2 m outside the hollow is GeodeShell);
  - generation is deterministic per seed.
- **Grid:** hollows are closed seeded air (the carve leaves the shell solid) and count no removed
  volume.
- **Response:** GeodeShell digs at 15–35% of soil's rate at every tool level.
- **Seats:** every geode holds six geode crystals of its zone, pointing inward and sunk into the shell.
  Geode crystals lie nowhere else, and the population total is unchanged.
- **Break-in (PlayMode):** it opens once, and stays opened after save and load.

## Acceptance criteria

1. New Game holds five geodes. The shell reads as distinct stone in a lamp-lit wall and digs clearly
   harder than soil, but takes seconds.
2. Breaking in shows a dark, sealed hollow with a small dust drift, crystals pointing in from its walls.
3. The crystals sparkle under a lamp and sell well above the ground around them.
4. Both admin A/Bs work (shell A/B, glow off/faint).
5. Compiles warning-free, tests pass, build and playtest note delivered.

## Playtest notes (`docs/playtests/110-geodes.md`)

- Use Ground X-ray to find a geode (cyan), or the Ground Lab's geode bay.
- Dig into its shell, break in and light it with a lamp; collect the crystals and sell them.
- Compare the shell A/B and the glow A/B.
- **Good feels like:** "a hard ball of rock... something is in there", then darkness, a lamp, and a
  sparkle that pays.

## Iteration (user, 2026-10-06, after a first look)

- "Geode shell A is beautiful": B (the same stone pale and cool, faintly banded) and the shader global that switched
  them are gone; the shell has one texture set, `GeodeShell_*`.
- "The glow makes no difference": the glow A/B is gone (the crystals' property block, their trace of emission, the
  admin toggle). A crystal shows in the lamp's light, as concept `03` §5 has it.
- Emission had first failed to show at all: URP's material validation switches a material's `_EMISSION` off when
  its emission colour is black, and the excavation daylight's copy kept that.

## Iteration 2 (user, 2026-10-06: "make them larger and with one crystal type inside and weirder shape")

- **Shape:** the hollow is a main ellipsoid (radius 1.3–1.6 m) smoothly joined (polynomial smooth minimum, 0.4 m)
  to one or two side lobes (half to three quarters its size, offset 0.6–0.9 of its radius, rising or dipping a
  little, the second at least 90 degrees round from the first), bent by a broad warp (0.22 m of simplex noise at
  0.55/m) shared with the shell, which is the same shape grown by 0.6–0.8 m with its outer lumps. `Geode.Reach` is
  now a stored field covering lobes, blend, warp, lumps and 0.1 m slack.
- **Cost:** a geode's box is about 7 m across, some 175,000 samples at 0.125 m: `TerrainGround.GeodeField` samples
  it in a Burst job (z slices in parallel) for both the material fill and the hollow carve; the managed loops were
  six times the old work and would have added seconds to New Game.
- **Crystals:** `TerrainGround.HollowFace` marches rays from the centre to the hollow's face (0.08 m steps, then
  halving) and takes the normal from the distance's gradient, shared by seating (`GeodeSeat`) and the break-in
  probes (`DiscoveryField.HollowFace`). Each geode takes `GeodeCrystals` = 10 of the kind its band covers with the
  most instances left (a tie drawn); six ring the floor and lower walls, four higher. Counts: celestine and
  fluorite 10 each (the sediment's two geodes), amethyst 20 and citrine 10 (the riverbed's three); each crystal
  cheaper ($30, $40, $55, $75), a geode worth a little more than before. Crystals are 1.6 times larger
  (0.4–0.6 m clusters), so ten still fill a bigger hollow.
- **Ground Lab:** its geode is as large as the site's (lobes east under the unused slot and south, clear of the
  shell bay), about 2 m down, lined with the kind most geodes hold.
- **Lighting:** breaking in flashed bright, then went dark (user). The instant patch lit fresh nodes from any open
  neighbour, while the rebuild's links need clear air between the nodes' standing points; the patch now checks the
  same clearance. Scripted pinholes (on and between node columns) lit the same at once and after the rebuild, so
  the user's exact case is not reproduced; it is in the playtest note.

## Iteration 3 (user, 2026-10-06: "could be slightly bigger still")

- Main hollow radius 1.5–1.85 m (lobes scale with it). The Ground Lab's geode moved under its bay's centre (the
  backfill pit bay went, so the geode bay is the fourth slot), with lobes east and north into unused slots.
- The drill's lip rule missed the user's case: looking down a shaft through a hole into a geode, the ray reached the
  hollow's floor beyond dig reach, so no target was found and the lip test never ran. The lip test now runs before
  the reach check against the ray's hit however far (`TryGetDigTarget`), with a wider ball (0.4 of the bite). In the
  lab, aimed 8 cm inside the rim from 1.6 m above it, the drill cut the rim and left the floor 4 m away untouched.

## Iteration 4 (user, 2026-10-06: "this time it digs what I don't want")

- The wide ball for the drill's lip caught ground right beside the player, about 14 degrees off the crosshair, because
  up close a 0.2 m ball spans a wide angle. Two rings of 12 rays now look 1.75 and 3.5 degrees round the aim, and the
  nearest hit across an edge wins, so only ground visually at the crosshair counts. In the lab, aimed 14 degrees off a
  trench's near edge, nothing beside the player was cut.
