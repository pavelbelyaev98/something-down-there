# 116 — Great Caves, Crystal Trophies and Cave Rock

**Status:** complete: each zone holds one great cave, a hall across the middle of the site with pillars and stalagmites,
crystals of one kind and its one crystal trophy (a unique marked anywhere, set upright at camp, dug free with aim-local
assist), in the demo's cave rock, plus three mini caves of a few crystals in geode shell. Breaking in opens a hollow at
once; light from its hole pools under it; crystals light only the dark and keep some glow in daylight.

## Objective

The user, after `115` iteration 4 (2026-10-07):
- "I expected you to create one cave that is actually very wide, like almost a whole level wide, with plenty of
  stuff ... like in the demo. You just made larger backfill pockets with geode for crystal and called them caves;
  those are not caves. I did not ask for small caverns but for larger caves."
- "I do want the big crystals also, but instead of breaking them I want to extract them with the crane like uniques."
- "Multiple caves, each cave has one crystal to excavate and it becomes a unique people look at. So one per zone."
- Fill: small crystals to pick up, big crystals to crane out, rock pillars and arches, the demo's colour areas.
  A recovered big crystal is displayed at camp. Big crystals only in the great caves.
- "I do like mini caves that are small ones with just some pickables here and there; I didn't like the medium-sized
  caves."
- "I don't think the geode texture is good for crystals ... use the textures from the demo where the rocks were."
- Bugs: artifacts that don't disappear after digging (screenshot: a grey piece hanging in a shaft into a cave);
  the Ground Lab has a stable frame rate but stutters while walking around.

Goal: digging down, each zone holds one great cave, a sealed hall almost as wide as the site, with pillars,
arches and four glowing colour areas like the Crystal Caverns demo, and in it one big crystal formation the player
digs free and sends up with the crane, to stand at camp for good. Small caves with a few crystals sit between.

## Concept reference (what changes)

- **`03` §5 caves:** today "a few in every zone ... a small grotto of two or three joined chambers", one kind to a
  cave. Becomes: one great cave per zone (almost the site's underground width, walkable, pillars and arches,
  four colour areas) and a few mini caves per zone (one small chamber, a handful of one kind). Still sealed, still
  never connected to each other; the player always breaks in. "The hole is yours" holds: every passage between
  places is dug.
- **`03` §5 stone:** caves get their own ground, cave rock (the demo's rock), instead of the geode's shell.
  Geodes keep their shell.
- **`05` uniques:** today every unique is hand-placed and a computer. A unique may now be a cave trophy: one big
  crystal formation per great cave, seated by the generator, recovered by rope, worth nothing in money, displayed.
- **`07` camp:** recovered crystals stand upright in a display row beside the camp's set-down spots.
- **`15` anti-patterns:** "Generate cave networks" stays: great caves are single sealed halls.

## Live codebase analysis

- **Caves (`TerrainGround.Caverns.cs`):** `Cavern` holds up to 10 chambers (`FixedList128Bytes<float3>`), pillars
  as vertical columns, a flattened floor and a shell; `CavernJob` (Burst) samples its hollow or outer face;
  `FillCavern` writes `GeodeShell` into the material lattice; `ExcavationGrid.CarveCavern` carves the air.
  `Caverns()` places `CavesPerZone` (3) medium caves per zone under the plot.
- **Seeded air meshing (`TerrainVolume.SeededAir`):** every chunk in every hollow's box is meshed (and its
  collider baked) synchronously at session start. Fine for small hollows; a 44 x 9 x 24 m great cave box is
  about 1,400 chunks, four of them about 5,500.
- **Ground materials:** `TerrainMaterialId { Soil, Backfill, GeodeShell }`; the mesher writes ground weights to a
  vertex stream with two free channels (`TerrainChunkMesh.Job.SurfaceMaterials`); `GroundTriplanar.shader`
  blends; responses in `EquipmentProgression`; names, X-ray, debris colours, tool motion family and the
  tunable-ground list each switch on the id.
- **Uniques:** policy `DiscoveryCatalog.cs:119-123` requires `AuthoredPlacement`; authored uniques become odd
  spots that caves keep clear of. Import caps: `DiscoveryContentSetup.CentreModel` 0.5 m, `PropBake` 1.0 m,
  `DiscoveryField.Generate` 1.0 m placement radius. Crane: `BuriedFind.CanMark` (exposure), `SalvageCrane.TryMark`
  needs a free set-down spot; 3 spots (`SalvageCraneSetup.cs:31`, maximum 16); a load hangs from its lifting eye
  and lands as it hangs. `DiscoveryField.UseGroundLab` puts every unique into the crane scenes. Saves require
  every catalog unique exactly once.
- **Thin leftovers:** cuts take each sample's own ground's bite, so hard shell beside soil survives as fins; at a
  thin cave roof a shovel leaves a lace of shell one sample thick joined by strands. The support check removes
  only fully detached pieces and the remnant pass only pieces under 0.5 m or paper-thin, so the lace stays
  (reproduced in the lab: sheets 0.125 m thick spanning 4.4 m).
- **Stutter:** in the Editor no game-side spikes; `FpsHud` allocates about 1.2 KB a frame; crystal lights
  (10 shadowed point lights) switch as the player walks the lab, sealed caves included.

## Design

### 116.1 Great caves

- One per zone (`TerrainGround.GreatCaves`), generated before mini caves and geodes, after pits and odd spots.
- **Size:** chambers on a jittered grid across the underground (about 7 along x, 3 along z), each an ellipsoid
  about 7-9 m wide and 5-7 m high, some dropped for an irregular outline with bays, the rest smoothly joined:
  a hall of roughly 40 x 18 m, floor flattened with a gentle roll. The middle lies under the plot so digging
  down meets it; the rest runs out under the bank.
- **Depth:** zone 1's below the computers (floor about 30-34 m); zones 2-4 in the middle of their zone.
- **Avoids:** pits (with their chests) and uniques' spaces: a chamber that would meet one is dropped.
  Mini caves and geodes keep clear of great caves.
- **Rock features**, all cave rock and diggable: 6-9 pillars from floor to roof, 2-3 arches (a half ring standing
  on the floor, fitting under the roof), 6-10 stalagmites (cones from the floor).
- **Struct:** `Cavern` gains `Great`, chamber lists of 42 (`FixedList512Bytes`), arches and stalagmites lists.
- **Colour areas:** the hall's x extent in four bands, one crystal kind each (seeded order), like the demo.
- **Contents:** about five small crystals per area (pickable, glowing, seated on floor, walls, roof and on
  pillars), and the zone's crystal trophy on the floor.

### 116.2 Mini caves

- Three per zone, one small chamber (about 3.5 m across, 3 m high), three or four crystals of the depth's kind,
  cave rock shell. The medium two-to-three-chamber caves go.

### 116.3 Cave rock

- New `TerrainMaterialId.CaveRock`: the demo's cave wall rock (`CaveWall_0` albedo, normal, mask) made seamless
  by `GroundTextureSetup`; digs like the geode shell (hard); X-ray marks it; its debris takes its colour; name
  "Cave rock: hard stone around a cave". Shells, pillars, arches and stalagmites of every cave use it; the Ground
  Lab's cave markers too. Shader: the fourth ground weight in the free vertex channel.

### 116.4 Crystal trophies (uniques)

- Four catalog uniques, one per zone: the demo's big crystal formations (quartz, amber cube blocks, green hex
  columns, ruby), 1.6-2.2 m tall, glowing, convex hull, lore, value 0, rope recovery.
- `Entry.CaveTrophy` (placement by the generator) instead of `AuthoredPlacement`; the unique policy accepts
  either; trophies make no odd spots; the 1.0 m radius cap does not apply to them.
- **Seat:** upright on the great cave's floor in its zone's area, sunk about a third, clear of pillars and arches.
- **Recovery:** dig its base free (exposure as for computers); the lifting eye always goes on its crown, so it
  hangs and lands upright.
- **Camp:** four display spots for crystals beside the three set-down spots (7 in all); a trophy shows its card
  (name, depth, lore) like a computer. Its glow stays; it carries no light at camp.
- **Import:** `CrystalTrophySetup` (editor) builds each from the pack's prefab meshes, keeping emission, without
  the find size caps.
- **Ground Lab:** trophies stay out of the crane scenes; the lab's great cave seats all four.

### 116.5 Meshes on approach

- Great caves' chunks are meshed when the view comes within about 15 m of their box (or a cut enters it), nearest
  first, within a small time budget a frame; all at once if the view is already inside. Small hollows keep meshing
  at session start.

### 116.6 Crystal lights

- Only crystals in an opened hollow light (geodes as now, caves derived the same way from probes on load and set
  by break-ins); walking past sealed hollows costs nothing. A trophy's light reaches further.

### 116.7 Thin roofs give way

- When a cut opens into a hollow (cave, geode, chest pocket), solid ground thinner than 0.25 m within about 0.9 m
  of the opening caves in with the break-in debris (`TerrainVolume.CollapseThin`): no lace or hanging pieces.

### 116.8 Ground Lab

- One great cave under the whole lab, its roof about 13 m down (below the bays and crane scenes), with a shaft
  from the surface to a metre above its roof ("Great cave: drop down, dig the last metre"), all four trophies.
- Four mini caves about 2 m down under cave rock patches, one per kind, named by the prompt.

### 116.9 HUD

- `FpsHud`/`GameHudView` stop allocating every frame (text rebuilt only on change).

## Trade-offs

- A great cave takes 6-8 m of a 37.5 m zone across the whole footprint: fewer ordinary finds there; its crystals
  and trophy stand in. Depth-pacing tests get new expectations for cave bands.
- Load and memory: four great caves add about a second of generation; meshing on approach avoids thousands of
  chunks at start; a fully visited cave holds tens of MB of meshes.
- 1.6-2.2 m loads: the crane tears narrow shafts wider on the way up (existing behaviour).
- Saves: new uniques and ground; older saves need New Game.

## Edge cases

- A unique space or pit in a great cave's band: its chambers drop; the hall keeps a bay there.
- A trophy whose area has no free floor: the next area's floor; none at all: the cave's largest chamber.
- The player inside a great cave box before meshing finished: finish at once.
- Display spots full: cannot happen (seven uniques, seven spots); `TryMark` still refuses as now.
- Collapse near a find or chest: finds are not ground; the collapse only removes thin ground.

## Tests

- Great caves: one per zone, inside the grid, clear of pits/spots/each other, size bounds (EditMode).
- Trophies: each seated once in its zone's great cave, upright, policy valid; catalog quotas updated.
- Thin collapse: a one-sample sheet over a hollow is removed, a 0.6 m roof stays.
- Existing find-pacing tests updated for cave bands.

## Acceptance criteria

1. Each zone has one great cave about 40 x 18 m, walkable, with pillars, arches, stalagmites and four colour
   areas of glowing crystals; three mini caves per zone with a few crystals.
2. Cave walls show the demo's rock; geodes keep their shell.
3. A trophy can be dug free, marked, hauled up and stands upright at camp with its card.
4. No hanging shell pieces after digging into a cave; walking the lab costs no crystal lights.
5. New Game load time and walking frame times not worse than before by more than about a second / no new spikes.

## Rejected or changed during the work

- **Room for standing rock:** the first check measured the hall's shape with its floor, which caps the distance at
  the height above it, so no pillar and few stalagmites found room. It measures the walls and roof only
  (`CavernWalls`): 7-9 pillars, 2-3 arches and 8-10 stalagmites a cave.
- **Mini caves:** spread over each zone's slices as before, a slice inside the great cave's band held none (8 of 12).
  They now spread over the zone's depths outside the great cave.
- **New Game failed** ("discovery density too high" at 96.5 m): finds aimed for depths the great hall fills across
  the footprint. Placement now leaves the great caves' depths out of every band (`DiscoveryField.Generate`
  `depthGaps`).
- **Ground Lab:** the great cave's floor is 22 m down so its pad clears the 12 m bays and the 11 m crane shaft; the
  shaft stops a metre above the roof of the chamber nearest the west side.
- **Trophies:** sunk 45 % they show 26-57 % exposure, so the base is dug before marking (in the lab a ring round the
  amber cubes took it to 91 %); the crane carried it tilting about 30 degrees and set it upright at the first spot.
- **Cave rock:** `CaveWall_0`'s maps are continuous (unlike the cave surfaces' atlases that failed for the geode
  shell) but low in contrast and not tiling; `make_cave_rock.py` stretches the shading and blends the half-offset
  copy at the edges.
- **Floating pieces:** a shovel through a thin roof of hard stone left a lace one sample thick joined by strands
  (no detached island, so the support check kept it). The collapse round each opening removes ground thinner than
  0.25 m; the support and sliver shortcuts no longer treat ground beside seeded air as untouched deep ground.
- **Stutter:** no game-side spikes in the Editor; sealed hollows' crystals stopped lighting (up to ten shadowed lights
  switched as the player walked the lab) and the HUD and the lab prompt stopped allocating every frame.

## Iteration 1 (user, 2026-10-07, after a playtest)

"The light from crystals is still bad, especially in light places ... it shouldn't emit light when it is light, it is
not a lamp. The first break-in into a cave or geode is unsatisfactory: make it actually remove a larger area of ground
immediately. Far away crystals just appear red and I don't see anything else in the dark, super unnatural; once I drop
into a cave it is absolute darkness, which is not correct since there is a hole above me and crystals and ground reflect
light. Some shapes make no sense, like a croissant. No need for effects when digging the cave itself; particles are for
the break-in and extracting; remove the admin option to turn off dust. I want ONE crystal to be extracted per cave and
each cave MUST have only one colour. Extracting forces one place for the hook; I want to place it anywhere. The caves can
be a bit smaller."

- **Crystal light only in the dark:** each crystal's light and its own glow (a property block scaling its emission)
  fade out between ambient 0.5 and 0.8 (`ExcavationDaylight.SampleAmbient`, just above the hollow floor), so a geode
  dug open near the surface shows plain coloured crystals and unlit walls, and a trophy at camp doesn't glow. A
  crystal in daylight gives up its place among the `LitCrystals` (its light used to stay on at zero, still rendering
  shadows).
- **Opened hollows are never pitch black:** the daylight grid keeps a floor (`ExcavationDaylightGrid.Floor`, 0.45)
  through an opened cave's or geode's box, set when it opens and on load, kept by rebuilds and patches. The route's
  light still fades with depth as tuned; the floor stands for light the stone and crystals throw back. 0.08, 0.2 and
  0.3 rendered near black (the cave rock is dark); 0.45 shows the hall's shape dimly and still asks for a lamp. Far
  crystals had read as lone red dots (the ruby's emission) in black; with the floor their stone shows round them.
- **Breaking open:** the first cut into a cave or geode drops a ball 1.1 m round of its roof or wall (centred 0.4 m out
  from the face) with a bigger fall of clods and dust; later cuts into any hollow (and a chest's pocket) throw no debris,
  only the thin-roof collapse. The admin break-in toggle went; a chest always caves in.
- **One colour, one trophy:** a great cave seats `GreatCaveCrystals` (12) of its depth's kind round its chambers; colour
  areas went. The lab's great cave holds the shallowest kind and its trophy only.
- **Arches removed** (a half ring standing on the floor read as a croissant); pillars and stalagmites stay.
- **Smaller:** 5 x 3 chambers 6 m apart across the middle of the site (radii 3.4-3.9 x 3-3.8 x 3.2-3.6), 3 left out.
- **Crane:** the lifting eye goes where the player aims; a trophy is still set level on its base at camp
  (`BuriedFind.StandsUpright`).
- **Tests (session close, before this iteration):** the full run showed geodes not all placed (great caves now come
  first, then geodes, then mini caves), crystals piled inside standing rock at a chamber's heart (the heart now moves to
  open air), a crowded amethyst geode (32 tries for a geode seat), camp spots too far out (moved within 11.5 m of the
  opening), and tests assuming every unique is a buried computer.

## Iteration 2 (user, 2026-10-07, after a playtest)

"Normal minerals automatically uncover while extractable items do not: aim at a location of the item and the dirt around
that location uncovers; no dirt in view and hovering at the item stops it; it assists only, so the player still moves
around. The mini cave ground is not one of the Ground Lab's ground types; why switch it from the geode's, which was fine?
The crystals are too dark when there is light and blend with the new ground. Underground they are better, but the cave
is way too light: I see the full cave though there is no light source. Remove the lamp inside the crystals."

- **Aim-local uncovering for uniques:** a stroke aimed at a unique digs its covering soil within `AimedSoil` (0.45 m)
  past the tool's radius of the aimed point, the soil nearest that point; with none there it digs nothing. Commons keep
  the whole-find assist. (`UniqueUncoveringDigsRoundTheAimedPointEvenWithAFullBag` replaces the test that forbade it.)
- **Mini caves in geode shell again**, their lab marks too; cave rock stays the great caves' stone and gets a Ground Lab
  bay ("Cave rock").
- **The uniform hollow floor (0.45) is rejected:** it lit the whole hall with no source. In its place an opened hollow's
  box is marked in the daylight grid: light that reaches its hole falls straight down undimmed and spreads sideways with
  3 quarter tallies a node instead of 4 (`HollowReach` 4 m to a third). In the lab: 0.37 under the hole, 0.2 at 3 m,
  0.09 at 6 m, nothing at 12 m. Deep site caves under a dark shaft stay dark but for crystals and lamps.
- **Crystals in daylight keep 40% of their glow** (`DayGlow`); their light still goes out, now between ambient 0.3 and
  0.7, so a crystal in the pool under a hole still glows.
- **No lamp inside a crystal:** crystal lights light only the ground's rendering layer, so a crystal no longer shows its
  own light as a hot spot.
- **Frame rate (30 FPS screenshot looking down the hall):** in the Editor about 1,100 shadow casters a frame come from
  the sun's cascades over the ground above, about 350 more from the crystal lights; not changed in this iteration.

## Iteration 3 (user, 2026-10-07, after a playtest)

"In light some rocks are hard to see. Breaking in makes a larger hole than I need, reduce it a bit. The eye hook doesn't
attach exactly to the crystal. The caves are really good now; maybe add a tiny bit more crystals, and on the roof as
well."

- **Crystals in daylight:** `DayGlow` 0.4 -> 0.75; the glow crystals are glass, never metal (the amber cubes are cut
  from the pack's pyrite and had kept its 0.6 metallic, which darkened them to brown in daylight).
- **Break-in:** `BreakOpenRadius` 1.1 -> 0.8 m, an opening about 1.4 m across instead of 2.
- **Lifting eye on the crystal:** a trophy's collider is a convex hull that spans the air between its spires, and the
  mark landed on it up to 0.8 m off the crystal. Marking now carries the aim on to the visible mesh's first triangle
  (or, between spires, the mesh vertex nearest the hull's hit).
- **More crystals:** 16 a great cave (every fourth hanging from its roof, elevations 55-85 degrees from the chamber's
  heart), 5 a mini cave; 31 instances of each kind.
- **Tests (session close):** the seed sweep found two celestines in one geode on seed 2: a crystal whose own slot round
  the geode is crowded now tries seats anywhere round the hollow before keeping an overlapping one. A lakebed scene
  check compared shader render queues, which read as Geometry without a graphics device (batchmode), so it runs only
  with one.

## Iteration 4 (user, 2026-10-07: "3-5 stutters or microfreezes" dropping into a cave and jetpacking out; crystals that shine only up close)

Reproduced in the Ground Lab: a shaft through the cave-rock bay into the great cave, the player dropped down it and
then lifted out at jetpack speed, profiled.
- **Great cave built at once:** entering its box before the stream finished built the rest in one frame (194 ms, 30 MB
  of density caches and chunk objects, then 20 ms of PhysX taking the colliders). The stream now starts at 40 m
  (`GreatReach`), so it is done before anyone digs or falls that far, and from inside the box keeps a 6 ms budget,
  re-sorting by the eye every 2 m. Released chunks hand their density cache arrays to the next chunk built: the fall's
  allocations went from 57 MB to under 1 MB.
- **Daylight catch-up patch:** the patch that re-lights cuts made during a route rebuild covered whatever was pending;
  after a restore (the whole grid) it ran for 0.9 s, and the opened-hollow reroute of iteration 2 stretched it from a
  cut to the cave's middle. It now runs only up to 64 cubic metres (`PatchVolume`); a reroute is its own request.
- **Crystal lights without shadows:** each shadowed light that came on or went out (with distance, or entering view)
  made URP reallocate its trimmed shadow atlas, a 20-40 ms GPU stall; climbing out of the cave showed 7-8 of them,
  none with the lights off. They cast no shadows now, so they can all light: up to 48 within 40 m, full to 28 m and
  fading beyond, instead of the nearest 10 within 25 m popping in ("from a distance a rock is not shining, but when I
  get closer it shines"). Their 2.5 m reach can show through a thin wall beside an opened hollow.
- After: the fall's slowest frame about 28 ms (was 194 ms and 0.9 s), the climb's only Editor overhead.

## Iteration 5 (user, 2026-10-08: the cave rock "looks like it is from an apartment"; a crystal "light pink" in its pickup, "bluish" before)

- **Cave rock:** every Crystal Caverns rock texture is the same flaky purple-grey stone, and the mountain packs' are
  stylized beige or grey. The Mining pack's jagged small rock is a scanned brown stone: a 1200 px square from inside the
  largest island of its 4K atlas (clear of the islands' stretched padding), its colour, normal and occlusion made seamless
  and the colour darkened (`art/mining-pack/make_cave_rock.py`, moved from the Crystal Caverns card).
- **Pickup copy:** the flying copy of a collected find left out the find's material property block, so a crystal whose
  glow daylight had dimmed flew at full glow, and it took no shadows, so the sun could light it through the ground; warm
  light on purple read pink. It now takes the find's shadows, probes, rendering layers and property block
  (`FindPickupPresentation`).

## Iteration 4 (playtest, 2026-10-08): puddles

- User: the cave rock "is AMAZING ... the vibe I want"; "add water inside the caves like puddles ... when digging they
  could disappear? we can see if they are good or not".
- `CavernScenery` lays up to five puddles in each great cave's lowest floor spots and one in half the mini caves,
  planned on a worker thread from the cave's shape so nothing is saved and nothing hitches: the lowest floor in a search
  grid, flooded cell by cell up to 9 cm deep while it stays within 2.2 m (shallower when it would spill or run off a
  drop). Water: a level mesh, `Content/Environment/CavePuddle.mat` (URP Lit, dark, 0.93 smooth, Crystal Caverns'
  water normal), adapted by `ExcavationDaylight` and on the crystals' light layer. A cut reaching its bed drains it (it
  shrinks away over a second); a reload with its bed dug leaves it out. A/B: Developer admin **Cave puddles**.
- Concept `03` §2's "water never enters the excavation" is about the lake flooding the dig; a puddle is old water in
  a sealed cave, harmless and only ever drained.
- Puddles, first build: the water was a URP Lit material adapted at runtime (`ExcavationDaylight.Register`), so the
  player kept only its opaque variant and drew it near-black, invisible on a dark floor (user: "I saw no cave puddles in
  the Ground Lab"; the Editor compiles every variant and showed it right). `CavePuddle.mat` now uses `Excavation Lit`
  directly with its transparent keywords, so the build keeps them. The water's edge is traced along the floor's contour
  (marching squares over the floor's heights) after a grid of cells showed straight edges where the rendered floor sat a
  centimetre below the computed one; it is see-through (half) so the floor shows under it. The lab's prompts point to
  the puddles (bring a lamp: in the dark, water shows only by what it reflects).
