# 106 — Buried Stashes: Backfill Pits, the Old Chest and Old TVs

**Status:** complete. The site is soil plus six backfill pits in the recent fill (`SiteLayout.Ground`), stash and rubbish alternating: each stash holds an old chest, opened where it lies with one tool strike once its lid has room, whose seeded hollow holds three silver or gold pieces seated from the population; each rubbish pit holds two old TVs ($40-100), the first junk finds, found only there; backfill draws from its own texture set built from the packs' soil and rubble.

## Objective

Bring the first ground back to the site: backfill pits in the top zone of the plain-soil site, each
holding something someone buried on purpose. A stash pit ends in an old chest that opens where it
lies with one tool strike once its lid has room, revealing a few silver and gold pieces. A rubbish
pit ends in old TVs, the first junk finds. Feel:
"why did it just get easy? Someone dug here before me", then the lid swinging open.

## Concept reference

- `03` §4 disturbed ground: when something was buried a hole was dug and filled again; the pit of
  loose, mixed backfill cuts across the ground, digs faster, and something waits at the bottom; only
  some finds get one; every pit holds something, even only rubbish. Tells are seeded world data,
  felt as well as seen.
- `03` §3: zone 1 (recent fill) is soil with rubbish pits; mixed spots exist only with a job.
- `05` §3 finds inside finds: a few authored containers hold other finds; opened in the world with
  the same tool; no lockpicking, keys or inventory search; contents become visible before normal
  collection; contents belong to the save's finite population and never reroll; full-bag overflow
  follows the normal rules.
- `05` §1: commons sell and take one bag slot; junk belongs to the recent fill.
- `03` §9: no hazards; a falling chest never harms the player.
- User decisions (chat, 2026-10-05): grounds return to the site one at a time, each justified;
  backfill first. Pits only in zone 1 for now (a refilled hole deep in clay or rock needs a story),
  the first one shallow near the plot centre. Things buried on purpose leave a pit (stashes and
  rubbish); things that sank or were lost are scattered. The chest opens where it lies: no key, no
  crane, its rusted lock breaks under the tool once the lid has room; the empty chest stays as a
  solid object. A stash holds a small prospector's stash (silver, gold). The LeatherTrunk asset was
  dropped (AI-made); the drowned village is gone from the design.

## Live codebase analysis

- `SiteLayout.LayeredGround` (false) switches the site between uniform soil and the whole layered
  generator (`TerrainGround`): zones, places, channels, odd spots and pits together. There is no way
  to admit one ground at a time.
- `TerrainGround.Pits` places 6/4/3 pits in zones 1–3, randomly under the plot; `PitSeats` gives
  each one or two seats that `DiscoveryCatalog.Generate` fills with "the next find whose depth band
  covers the seat", today rocks, coal or ore.
- `BuriedFind` is one mesh, one convex `MeshCollider`, radius at most `DiscoveryField.MaximumFindRadius`
  (0.5 m); the chest (1.34 × 0.75 × 0.55 m, skinned, legacy `ChestAnim` dropping the lock and
  swinging the lid to about 95°) fits none of that.
- `FpsPlayer` already strikes non-terrain `IDigTarget`s at once (no scoop) and shows their
  `DigPrompt` when `CanDig` is false; `IInteractionTarget.GetPrompt` drives the crosshair prompt.
- Sealed rooms carve seeded air in `ExcavationGrid.Reset` and build their chunks at session start
  (`MaterializeSeededAir`); odd spots reach the grid as serialized `TerrainVolume` data copied by the
  discovery sync.
- `ExcavationDaylight.Register` swaps URP Lit materials onto `ExcavationLit`, whose `_SPECULAR_SETUP`
  is a `shader_feature` (strippable in builds): buried props must use the metallic workflow.
- Saves: `WorldSaveCodec` version 16 appends sections after the worksite marks.

## Design

### Ground features (`TerrainGround`, `SiteLayout`)

- `TerrainGround.Features` flags (zones, places, channels, odd spots, pits; `All` for tests and the
  layered generator). `SiteLayout.Ground` replaces `LayeredGround` and currently admits only pits:
  the site is soil everywhere except backfill pits. `Generate`, `Layout`, `ExcavationGrid` and
  `TerrainVolume` take the features; without zones the ground below the first metre is soil.
- Pits: zone 1 only, six of them, kinds alternating stash/rubbish from the first. The first is a
  stash near the plot centre (±3 m) whose bottom lies 4–6 m down and which leans little. Stash pits
  are wider (1.0–1.2 m radius) so the chest lies inside loose fill. Rubbish pits end at least 8 m down,
  where junk starts. Pits keep clear of every unique's space whether or not odd-spot ground is on.
- A stash (`TerrainGround.Stash`): the chest's pivot a little above its pit's bottom, at a seeded yaw.
  When the volume knows the chest's hollow (`TerrainVolume` serialized hollow centre and half size,
  copied from the chest prefab by the discovery sync), `ExcavationGrid.Reset` carves that box of air
  inside each stash, reaching into the chest's walls, floor and lid so no soil face shows inside. Its
  chunks are built with the session like a sealed room's. Rubbish pits keep their seats; stash pits
  have none.

### The old chest (`BuriedChest`, `IDigTarget`, `IInteractionTarget`)

- Prefab (`ChestSetup`, part of Sync Discovery Models): a variant of the URP old chest with a
  kinematic `Rigidbody`, box colliders for floor and four walls on the root and one on the lid bone
  (it swings with the lid), and measured geometry: the hollow, content seats on the floor, lid-space
  samples (the arc the lid sweeps, up to about 0.75 m above it) and footing samples under the base.
- Closed: the crosshair reads "Old chest | Dig out the space above its lid" until at most 15% of the
  lid-space samples are solid, then "Hold Dig to break the rusted lock". The dig action on the chest
  opens it (`TryDig`), costs one stroke's charge and plays the vendor animation. Digging aimed at the
  chest never cuts soil; open or closed, the soil around it is dug by aiming at the soil.
- Physics: anchored while any footing is solid soil beyond two samples; undercut, it falls and settles
  (its contents, loose inside, fall with it); an administrative reset that buries it anchors it again.
- Never sold, bagged or craned; the detector ignores it; the player stands on it.
- `DiscoveryField` owns the chests: New Game places one per stash, saves capture pose, released and
  opened, and the Ground Lab clears them.

### Contents and junk (`DiscoveryCatalog`)

- `art/old-chest/catalog.json` names the chest prefab, three items per chest and weighted contents
  (silver ×2, gold ×1). Each chest's floor seats take, like any seat, the next find of the drawn type
  whose depth band covers the chest (another content type when none does), lying level at a seeded
  turn, so counts never change; ordinary finds keep out of a sphere around the whole hollow.
- Contents lie in the carved hollow: free, they settle on the chest floor at New Game. The closed
  chest's colliders block aimed and nearby pickup; once open they are ordinary finds.
- Junk: `art/tv-set/catalog.json` and `art/big-old-tv/catalog.json` add five TVs (portable, wood-cased,
  CRT, flat screen, big old TV) as commons (`junk`), centred meshes on the project URP materials with
  box hulls, depth 6–36 m, host soil. Rubbish-pit seats take only junk; ordinary placement scatters
  the rest.

### Save

- `WorldSnapshot.Chests` (`ChestSnapshot`: terrain-local position and rotation, released, opened),
  appended after the worksite marks; codec version 17; at most 64 chests.

## Edge cases

- Lid space partly dug: refused with the prompt; held dig repeats the hint, never cuts the chest.
- Chest dug out from below before opening: falls with its contents inside, stays closed.
- Full bag after opening: contents stay in the chest (normal overflow).
- Contents exposed by soil cuts through a closed chest: they are already loose; the walls hold them.
- Admin terrain reset: the hollow is carved again; finds and chests keep their state; a fallen chest
  left in soil anchors again.
- Saves from before this change do not load (format 17); New Game.
- Ground Lab: no stashes, chests cleared.

## Acceptance criteria

1. A New Game site is soil except six backfill pits in the top 37.5 m; the first lies near the plot
   centre with its chest 4–6 m down.
2. Every stash pit holds a chest with three silver/gold pieces in its hollow; every rubbish pit seat
   holds a TV; deterministic per seed.
3. The chest refuses to open until its lid space is dug, then opens with one strike, plays its
   animation, and its contents become collectible and sell.
4. Opened state, pose and contents survive save and load.
5. TVs turn up scattered in the recent fill, render correctly underground and sell.
6. Tests for generation, catalog placement and save pass; Windows build delivered with a playtest note.

## Results (2026-10-05)

- Shipped seed: three stashes and three rubbish pits; the first chest lies about 5 m down near the plot
  centre with its three pieces; every rubbish seat holds a TV. Total population unchanged by the
  chests (15,723 with the TVs).
- In play: the backfill reads as a pebbly column against the soil; the chest refuses until its lid
  space is dug, opens with the vendor animation (lid up, lock dropping) on its three pieces, and the
  open chest keeps them visible and collectible. Tests cover generation, the hollow, catalog seats,
  the save record, the opening, the fall and save/load of an open or fallen chest.
- Measured chest: 1.34 × 0.75 × 0.55 m, floor top 0.24 m below its centre, inner walls at 0.32 m
  across and 0.66 m along, hinge at the back top edge, lid reach 0.71 m.

### Rejected and corrected along the way

- Chest contents added on top of the population broke every count; they are seats now.
- TVs from 2 m could not find room in the dense entry layer (New Game failed); junk starts at 6 m.
- A 3 cm hollow margin let the ground's rounded corners grip an undercut chest; 9 cm lets it fall.
- The lid-space check sampled only above the lid; it now follows the lid's thickness through its
  turn, so soil behind the hinge counts and the open lid no longer clips.
- The chest's specular material would have lost its specular setup through the excavation daylight
  shader in builds; it is metallic now (`art/old-chest/make_mask.py`).
- The three simplest TVs (under 300 triangles) take no extra detail levels; the catalog test asks
  for them only from 300 triangles.

## Iteration (user, 2026-10-05, after the first look)

- TVs were far too cheap for a find that waits at the end of a tell ("a rock costs almost this much"):
  junk is now found only in rubbish pits, two to a pit (six a world: portable $40, CRT $60, wood-cased
  $70, flat-screen $80, two big old TVs $100), and the wood-cased, CRT and flat-screen models are
  scaled up to real sizes (1.4-1.5x). Seated junk may reach `DiscoveryField.MaximumLargeFindRadius`;
  it must fit the narrowest rubbish pit (`TerrainGround.RubbishRadius`). Rubbish pits end at least 8 m
  down so both seats sit in junk's band.
- The backfill look mixed other grounds' textures in hard 40 cm cells (gravel twice, the "clay" layer
  tinted orange), which cut sharp orange sheets through every wall. The user asked for a base from the
  textures they own instead of an invented one: `art/pure-nature-highlands/make_backfill.py` turns
  Mud_rubble's mud and stones into the site's soil (Mud01 with the dig ground's tint) as one tileable
  set, and the shader draws backfill from it alone. Tried and dropped: soft blended normals (the fill
  read smoother than the soil around it) and relief 1.3 (black creases under a lamp); whiteout-blended
  normals at relief 1.0 keep it lumpier than soil without them.
