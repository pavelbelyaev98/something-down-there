# 111 — Zone Main Grounds

**Status:** complete, awaiting the user's picks. Zone 2 is old lake sediment and zone 3 the old riverbed, now a
pebbled stone in the cave rock's style, each a pack-derived ground behind warped, patchy borders, checked against the
levels players own on arrival; three looks per ground, a dig-feel preset and zone 4's ground switch live in the
Developer admin until the user names winners.
Plan and research: [107](107-asset-only-grounds.md).

## Objective

Going deeper changes the ground. Below the recent fill (zone 1, soil), zone 2 turns into **old lake
sediment** and zone 3 into an **old riverbed of sand and gravel**. That is the real order under a
drained lake: recent mud, then older grey lake silt and clay, then the sands and gravels of the river
that ran there before. Each ground is built from an owned pack texture, learned over a whole zone and
blended into the next over a few metres. Arriving in a new zone is an event, never a restart.

## Concept reference (rules this task follows)

- **`03` §3, one main ground per zone:** a zone that is mostly one ground lets the player learn how it
  digs, what it hides and what it looks like; the old thin repeating stack made materials meaningless.
  Each ground is built from an owned pack texture; until a zone's ground is in, the zone is soil.
- **`03` zone rules:** no zone feels like a restart (at the tool level a player typically owns on
  arrival, the zone's ground digs no slower than the previous zone felt near its end; Meltopia's blue
  snow); never a wall; variety inside a zone and no glaring pale surfaces (Keep Digging's white
  layer); blended borders in noisy patches over a few metres (Minecraft's deepslate band), never a
  flat line; mixed spots only with a job (a zone border is one).
- **`03` §2:** a diggable ground never looks like the bedrock shelf or the retaining walls; the
  riverbed must not read as geode shell either.
- **`03` §4:** hardness shows mostly as bite size and a little as rhythm; grounds read by grain, not
  colour alone; resistance belongs to the ground inside the cut.
- **`03` §6/§8:** grounds come from the seed as the same one-byte material IDs; zone layout and main
  grounds are authored, shapes randomized per save.
- **`02` §3:** four zones over 3–5 hours; the drill arrives around the riverbed.
- **`05` §1:** host ground is a soft bias with scatter; prices stay fixed per type.
- **`03` §1:** each zone gets its own kind of buried trail, decided with its main ground.

## Live codebase analysis

- **Generation:** `TerrainGround.GroundJob.Material` returns soil, or backfill inside pits; stashes,
  geodes and caverns overwrite afterwards on the main thread (`FillShell`, `FillGeode`, `FillCavern`).
  `ZoneBorders` (37.5, 75, 112.5 m) are plain depth bands; grids shallower than 37.5 m are all zone 1.
  `SiteLayout.Ground` admits features to the site; other grids stay soil.
- **IDs:** `TerrainMaterialId { Soil, Backfill, GeodeShell, CaveRock }`, appended only; `Last` bounds
  save validation. The save format is `WorldSaveCodec.Version` 24; earlier ground changes bumped it.
- **Mesher:** one weight stream in UV2 `(free, cave rock, geode shell, 1 − backfill)`; only one slot is
  free, so two new grounds need a second stream.
- **Shader:** `GroundTriplanar.GroundSurface` lerps each deposit (`DepositSurface`) over soil.
- **Responses:** `EquipmentProgression` (soil 1, backfill ≈ 0.73 of soil's rate, geode shell and cave
  rock ≈ 0.25); `HardnessOrder` must be strictly slower class by class (`TerrainMaterialTests`).
  The old comment "a zone's main ground is matched two purchases later" has no levels behind it.
- **Admin:** `FpsPlayer` session toggles with buttons in `GameMenuView.BuildAdmin`; ground tuning
  overrides live in `EquipmentProgression` (static, session-wide).
- **Material at runtime:** chunks share `TerrainVolume.soilMaterial` (an asset); the X-ray makes a
  `DontSave` copy. Setting textures on the asset in Play Mode would persist in the Editor.
- **Ground Lab:** bays fill slots of a 6 × 3 grid by index; the find gallery covers the middle row
  completely (its mineral row runs to x ≈ 13.8 m) and the north row west of x ≈ 1.8 m; a mini cave
  sits under the south row's free east slot.
- **Owned candidates:** zone 2 — Highlands `Mud` (grey-brown silt with pale specks), Mountains `Mud02`
  (smooth puddled relief; its albedo is half moss), Crystal Caverns `GroundDirt` (smeared relief,
  normal and mask only). Zone 3 — Highlands `Sand_rubble` (sand with clear pebbles), Highlands `Sand`
  (pale; must be darkened), Mountains `Gravel` (fine grey gravel; the lakebed's packed sediment layer).
  Soil itself is Mountains `Mud01`, backfill is `Mud01` + Highlands `Mud_rubble`.

## Design

### Grounds (IDs appended: `LakeSediment` = 4, `Riverbed` = 5)

| Ground | Zone | How it digs (rate vs soil) | Look (default variant) |
| --- | --- | --- | --- |
| Lake sediment | 2 | A little firmer: same-width bites a touch shallower, a slightly slower stroke (≈ 0.86) | Grey silt: Highlands Mud graded cool grey-brown, thin bedding in walls |
| Riverbed | 3 | Firmer still, stony: smaller bites, the tool's short "bite" motion (≈ 0.66) | Sand and gravel: Highlands Sand_rubble, the sand darkened to damp ochre, grey pebbles |

Hardness order becomes soil < lake sediment < backfill < riverbed < geode shell = cave rock. Backfill
stays in zone 1 and the riverbed in zone 3, so the two stony grounds never meet.

### Look variants (Developer admin, session-wide)

Each zone ground ships three texture sets, all derived from owned pack textures by
`art/pure-nature-highlands/make_zone_grounds.py`:

- Lake sediment: **A grey silt** (Highlands Mud, its relief), **B blue-grey clay** (the same colour
  bluer, Crystal Caverns GroundDirt's smeared relief, stronger bedding), **C puddled mud** (Mountains
  Mud02's mud with its moss recoloured to mud, its smooth relief, warmer grey-brown).
- Riverbed: **A sand and gravel** (Highlands Sand_rubble), **B gravel** (Mountains Gravel warmed to a
  sandy grey-brown, kept apart from the geode shell's dark grey), **C gravel in sand** (Highlands Sand
  matrix with Gravel's pebbles churned in, as backfill churns its stones).

Variant A is the authored default in the material. `GroundLook` records (ground, name, maps, tint,
tile metres, relief, bedding) are serialized on `TerrainVolume`, wired by `GroundTextureSetup`.
Choosing a look makes a `DontSave` copy of the ground material for the session (the X-ray copy follows
it), so the asset never changes in Play Mode. The choice is static for the session (survives Ground
Lab restarts); **Restore normal rules** returns to the authored looks. Losers are deleted, with their
textures, once the user names a winner.

### Bedding in the shader

Lake silt is laid down a film at a time, so its walls show thin horizontal layers. The shader reads
the layer's own albedo down one texture column against world height (irregular bands, like real
varves), gently warped, filtered by the height derivative so distant walls never shimmer, and applies
it as a brightness ratio (average 1). Floors show one band at a time. The riverbed gets weaker, coarser
beds. Amount per look; zero switches it off.

### Mesh stream

A second stream in UV3, `(lake sediment, riverbed)`, from the same eight-corner material halo as the
first; a mesh without it reads (0, 0). UV2 keeps its meaning (one slot still free for `039`). The
shader blends the zone grounds first, then backfill, shell and cave rock over them.

### Borders

`TerrainGround.ZoneAt(p, depth, borders, offsets)`: each border depth is warped by broad simplex noise
(±1.5 m over about 20 m), and within ±1.5 m of the warped border (a 3 m band) a sample takes the lower
zone's ground where a patch noise (≈ 1 m blobs) falls below its position across the band: patches of
the deeper ground thicken downward. Generated per sample in the Burst ground job, so cuts show a
gradual change, identical after reload (the IDs are saved). Pits stay in zone 1; geodes and caverns
keep their stone in whatever ground holds them.

**Zone 4:** soil until `039` (concept rule). The admin toggle **Zone 4 ground (next New Game):
soil / riverbed** generates the riverbed down to the bottom instead, for comparison. The ground is laid when
MainGame loads, so the choice applies after a reload: set it in the Ground Lab, Leave Ground Lab, New Game.

### No-restart rule and expected levels

`EquipmentProgression.ZoneArrivalLevels = { 1, 4, 7, 9 }`: the tool level a player typically owns on
arriving in each zone (shovel 4 in the sediment, the drill at the riverbed, as `02` §3). "Near the end
of the previous zone" is one purchase earlier. Test: at every zone border, the zone ground's sustained
output at its arrival level is at least the previous ground's at one level below (measured with the
same scoop/drill cuts as the existing output tests). This replaces the unbacked "two purchases later"
comment.

### Dig-feel variants (Developer admin)

**Zone grounds dig:** authored / like soil / firmer (sediment ≈ 0.72, riverbed ≈ 0.5), applied as
session ground-tuning overrides, so the existing Ground tuning sliders and table show and refine them.

### Find family (host ground)

Each zone's mineral pair prefers its zone's ground (weight 2): iron, silver and pyrite in lake
sediment; gold and emerald in the riverbed. Inside a zone this barely moves anything (almost every
sample is that ground); it matters in border bands and, with `112`, in lenses. New families come with
`011`/`012`, container styles with `014`.

### Trails per zone (`013`, decided here)

Zone 2's trails are **mooring chains and drowned fence wire** from the lake's shore; zone 3's are
**pipes and narrow-gauge rails of old gravel workings** in the riverbed. Recorded in the `013` queue
entry and concept `03` §1.

### Other wiring

- `ToolRigPresenter.Family`: riverbed takes the short bite motion; lake sediment scoops.
- Crane break and break-in debris colours per ground (`SalvageCrane.Feedback`).
- Ground X-ray marks no main ground (they are everywhere); `GroundEffect` texts; admin ground tuning
  covers both grounds.
- Saves: `WorldSaveCodec.Version` 25 (New Game; the ground and population change).

### Ground Lab

Three new bays: **Lake sediment** (south row, east slot; the mini cave there moves south-east beside
the crane shafts), **Riverbed** and **Zone borders** (north row, the two east slots, clear of the
gallery). The border bay is soil to about 3.5 m, the band into lake sediment, lake sediment, a second
band at about 8 m and the riverbed to the bay's 12 m, using the same border function with lab depths.
Bays get explicit slots instead of slot = index.

## Edge cases

- **The 150 m site:** zone 4 below 112.5 m is soil (or the riverbed with the toggle); the 112.5 m
  border keeps its warped, patchy band, so it reads as a border, not as zone 3 leaking down.
- **Backfill vs zone ground:** backfill exists only in zone 1, so its stony look never meets the
  riverbed's.
- **Borders and the warp:** borders are 37.5 m apart and the warp is ±1.5 m, so bands never cross.
- **Admin overrides:** the hardness order only holds for authored responses; feel presets are session
  overrides and do not touch tests.
- **Material copy and X-ray:** toggling X-ray after a look change copies the look material; a look
  change while X-ray is on updates both.
- **Saves:** new IDs fail validation in older builds; New Game is required.

## Tests

- Zone borders: in each zone most samples (outside pits, geodes and caverns) are its main ground;
  samples farther than band + warp from a border are never the other zone's; inside the band both
  occur. Deterministic per seed.
- The no-restart rule holds at every zone border at the arrival levels.
- Existing hardness-order and per-tier improvement tests cover the new grounds through `HardnessOrder`.
- The mesher publishes the zone stream: (1, 0) for uniform lake sediment, (0, 1) for riverbed,
  (0, 0) for soil and backfill, continuous across chunk seams.

## Implementation notes (2026-10-08)

- The user asked for both grounds in one build ("finish it fully"), with open look and feel questions shipped as
  Developer admin toggles and the grounds added to the Ground Lab.
- **Bedding, first try rejected:** reading one texture column at the screen's own mip drew the Highlands mud's pale
  specks as bright white rings round a shaft. Bands now average four columns at mip 3 or coarser and stay within
  ±22 %.
- **Riverbed stones, first try rejected:** separating Sand_rubble's stones by saturation failed (its stones are as
  warm as its sand, 0.22 vs 0.29); its brightness is bimodal, so the mask splits at grey 0.6-0.66.
- **Brightness:** the first grading made both grounds about half again as bright as soil under lamps (glaring
  next to it); every look is graded to about soil's luminance. Puddled mud's moss takes the mud's own colour, with
  little hue kept, so no green fringes remain.
- **Ground Lab:** the gallery's mineral row covers the whole middle row of bay slots, so the zone bays take the free
  east slots (the south-east mini cave moved south beside the crane shafts). The border bay leaves out the site's
  broad warp, which only shifts a 3 m bay's borders.
- **Review:** a New Game generates soil to about 37 m, lake sediment to about 75 m, the riverbed to about 113 m and
  soil below; lamp-lit chambers across both borders show the patchy band (`Logs/111`).

## Acceptance criteria

1. Zone 2 is lake sediment and zone 3 the riverbed on a New Game, lamp-lit and distinct from soil by
   grain and colour, each border a gradual patchy band, never a flat line.
2. Three looks per ground and the feel presets switch live from the Developer admin; the zone 4 toggle
   applies to the next New Game; Restore normal rules returns to the authored state.
3. Ground Lab shows both grounds and a border bay; the aim prompt names them.
4. Compiles warning-free; the session's closing test run passes; a fresh Windows build and the playtest
   note are delivered. The user's pick of looks and the dig-feel verdict follow in chat.

## Iteration 1 (playtest, 2026-10-08)

- User: zone 2 "is fine I guess"; zone 3 "is ugly, it has weird colors and too many rocks inside"; the cave rock
  "is the vibe I want". The sand-and-pebble looks (Highlands Sand_rubble, Mountains Gravel, sand with gravel churned in:
  ochre sand against grey stones) are deleted.
- The riverbed's three looks are now squares of the Mining pack's rounded rocks, made seamless like the cave rock
  (`art/mining-pack/make_riverbed.py`, sharing `seamless.py` with `make_cave_rock.py`): A conglomerate (the large
  rock's stone, small water-worn pebbles set in it, warm grey-brown), B brown conglomerate (the small rock's, browner)
  and C grit (the small rock's gritty top). Each stays lighter and greyer than the cave rock, so a great cave's stone in
  zone 3 still stands out; faint bedding.
- The Mining pack's layered rock was tried as a cross-bedded sandstone: its atlas is broken by padding and moss
  specks, with no clean square big enough.

## Iteration 2 (playtest, 2026-10-08): texture layers

- User, with shots of a shaft floor at 1.5 m and 2.8 m showing the same stones in the same places: digging straight
  down "the texture just moves down and doesn't feel like digging". The cause: a floor takes the top projection, which
  maps by x and z only, so every depth showed the same part of the texture (a tunnel's end face likewise along its axis).
- `GroundTriplanar`: every projection of the soil and of every deposit takes another part of its texture every
  0.35 m along its own axis (`GroundLayer`: a hashed shift per layer; the layer's edge wanders by a third of a layer
  with a value noise across the face; over the lowest 30 % of a layer the layer below gives way by brightness,
  `LayerBlend`, so the next layer's stones come through instead of two patterns ghosting). Only texture coordinates
  shift; derivatives come from the position, so mips stay steady. Cost: a second set of samples only inside the band.
- User, after that build: "it is better but still seems repetitive". A shifted copy of an evenly pebbled texture still
  looks alike, so each layer is now also turned to one of eight orientations (quarter turns, mirrored or not; its normals
  turned back with it) and shaded up to 7 % lighter or darker, like soil horizons, and its edge wanders by up to 1.2
  layers across a face, so one face shows patches of several layers.
