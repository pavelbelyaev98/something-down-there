# 107 — Asset-Only Grounds: Remove the Invented Grounds

**Status:** complete. Only soil and backfill, both built from owned pack textures, exist; every invented
ground with its features (zone grounds, places, sealed rooms, channels, cracks, odd-spot lenses), the three
releases, their textures and Ground Lab bays are gone, and the mesher and shader carry one weight stream with
free slots for later grounds. This spec also holds the plan and research for `108`–`112`.

## The ground plan (107–112)

| Ticket | What the player gets | Spec |
| --- | --- | --- |
| `107` Remove the invented grounds | The same site, no collapses, a clean Ground Lab | [107](107-asset-only-grounds.md) |
| `108` Rubble backfill | Pits that dig at about half soil's speed and show their stones | [108](108-rubble-backfill.md) |
| `109` The chest by hand | Hold E to force the lock; an emptied chest goes away | [109](109-chest-by-hand.md) |
| `110` Geodes | Hard rock balls with crystal-lined hollows to break into | [110](110-geodes.md) |
| `026` Sticky C4 | C4, whose natural target is a geode shell | queue |
| `111` Zone main grounds | A new main ground per zone, one per playtest | [111](../111-zone-main-grounds.md) |
| `112` Secret areas | A distinct ground around each unique | [112](../112-secret-areas-around-uniques.md) |

Specs `108`–`112` were written ahead at the user's request (2026-10-05). Each is re-checked against
the code when it starts.

## User direction (chat, 2026-10-05)

- Grounds return one at a time, each with a reason and a base in the owned assets ("Bought Before
  Made", `AGENTS.md`).
- One main soil, then areas of a different ground with secrets or uniques inside.
- Backfill is harder than soil: about half soil's dig rate.
- Remove the releases (backfill slump included), rock and clay, and every texture and Ground Lab bay
  the dig ground does not use.
- The chest opens by holding E and plays its animation; once emptied it can despawn (first version,
  open to change).
- Wanted later: a harder ground, cave-like spots, things so hard to drill that C4 is the better tool,
  main grounds per zone or more variety deeper down.
- Uniques stay collectibles at camp; no sale, no depth payout.
- One plan in testable steps. Pure Nature 2: Crystal Caverns is bought for the geodes.

## Research: what to take, what to avoid

Sources: the four `.research` review analyses and a survey of Deep Rock Galactic, Astroneer,
Hydroneer, Minecraft, Valheim, SteamWorld Dig 1–2, Motherload, Dome Keeper, Terraria, Spelunky and
Mr. Driller (chat, 2026-10-05).

| Idea | Seen in | Used in |
| --- | --- | --- |
| A tougher, distinct ground around a prize says "something is in here"; paying the toll is the player's choice | Dome Keeper (gadgets under tougher patterned tiles), Mr. Driller (hard blocks around air capsules), Minecraft geodes, DRG Ebonuts | `108`, `110` |
| Hardness is read before it is felt: its own look and grain on the cut face | Dome Keeper, Astroneer, Hydroneer | `108`, `110`, `111` |
| Hard ground is slow, never closed; seconds, not minutes | DRG granite ("x0.5 holes", tedious), Dome Keeper's deep layers ("mind-numbing"; the developer doubled the drill) | `108`, `110` |
| Hold Interact to open a container | DRG cargo crate | `109` |
| Explosives buy speed and reach, never act as keys; finds survive the blast | Terraria, SteamWorld Dig 2, Spelunky, Minecraft (blast-proof ore) | `110`, `026` |
| Zone grounds blend over a few metres instead of a flat line | Minecraft deepslate | `111` |
| Each depth band has its own find family and container style | Terraria biome chests, DRG biome minerals | `111`, `014` |
| Deep secret finds are worth far more than the ground around them | Motherload artifacts, Astroneer samples | `110` |
| Pockets look natural, not like a puzzle someone laid out | Super Motherload (criticised) | `110` |
| Sparkle and glowing crystal in the dark are what players remember | Meltopia ("I very much enjoy the sparkles") | `110` |

**Avoided:**
- tier gates on the way down (One Man's Trash's 20 m blocker, Terraria's Lihzahrd bricks, Meltopia's
  Tesla-only snow);
- maze-like pockets and identical tunnels (Meltopia, Astroneer caves, DRG Dense Biozone);
- explosives that destroy loot or bounce (Motherload, A Game About Digging a Hole);
- empty or underwhelming chests (AGADH's empty finale chest, One Man's Trash's partial car);
- finds reachable only through the wiki (Hydroneer relics).

**Not taken now:**
- a proximity ping near secrets (the detector stays frozen until `099`);
- opening chests at camp (the user rejected crane trips for chests);
- keys;
- a surface dip over each settled pit (a later candidate).

## 107 Objective

Only **Soil** (Mountains Mud01) and **Backfill** (soil turned over with Highlands Mud_rubble) remain.
Every other ground goes, with every feature built on it and every texture, tool, test and doc that
serves it. The site plays as before, because it is already soil with backfill pits. Undercutting a
pit no longer collapses it, and the Ground Lab shows only what exists.

## Concept reference (rules this ticket changes)

- **`03` §3–5 (zones, materials, tells, places, sealed rooms):**
  - zones keep their depths but lose their main grounds, edge bands and places;
  - the tell rule "easier ground means you are on to something" becomes "different ground means you
    are on to something" (look, grain and feel; harder or softer; never a wall);
  - cracks, gravel channels, the pour, odd-spot lenses, places and sealed rooms leave the concept
    until their tickets bring grounds back.
- **`03` §1 "No caves":** the only pre-existing air is the chests' hollows, until `110` adds geodes.
- **`03` §6 and §9:** no releases. **§8:** layout validation keeps only what exists.
- **`04` §8 (C4) and `05` §1 "Where finds sit":** they lose the removed reactions and hosts.
- **`02` §3–5:** the arc and anti-straight-down rules lose cracks, channels and places, and keep
  pits, stashes and (later) geodes as the sideways reasons.
- **`01`:** "drowned workshops" goes (the user rejected the drowned village).
- **`09`, `13`, `14`, `15`:** they lose the removed looks, sounds, closed ideas and anti-patterns,
  where those name removed grounds.

## Live codebase analysis (sweep of HEAD `566009e`)

- **The site:** `SiteLayout.Ground = Features.Pits`, so the site already holds only soil, backfill
  pits and stash hollows. Everything below is generated only for the Ground Lab and the tests.
- **The enum is the risky part:**
  - `TerrainMaterialSnapshot.TerrainMaterialId` is stored as bytes in saves.
  - It also appears in `DiscoveryCatalog.asset` (`HostGrounds`): the minerals list Rock,
    FracturedRock, Crack and Gravel (`art/minerals/catalog.json`).
  - `DiscoveryCatalog.Validate` rejects ids above `Last`.
- **Break-in:** sealed-room break-in (`TerrainVolume.Rooms.cs` `CheckBreakIn`/`EmitBreakIn`) is generic
  in method but typed on `TerrainGround.Room`. Its dust uses the release particle pool in
  `TerrainVolume.Release.cs`. Its opened state is session-only.
- **Seeded air:** `CarveHollow`, `LabCarve` and `MaterializeSeededAir`/`MaterializeAround` serve the
  stash hollows and the lab, and stay.
- **Mesher and shader:** `TerrainChunkMesh(.Job)` sends two `Vector4` weight streams (UV2/UV3) for
  eight families. `GroundTriplanar.shader` blends them, plus zone tints, strata
  (`_StrataStrength` .13 on `ReservoirSediment.mat`, which also tints soil) and fracture and crack
  lines.
- **Ground X-ray:** still used on the site to find pits (playtest `106`).
- **Original soil art:** the `Soil_*` textures, `LICENSE.txt` and `art/ground-textures` are referenced
  only by `GardenGround.mat`. That material is used by:
  - the "Configure Original Soil" menu (`GroundTextureSetup` 57-61, 222-281);
  - two tests (`MainGameSceneTests` 125-135, `ExcavationDaylightIntegrationTests` 68);
  - the protected recovery scene `_Recovery/0.unity`.
- **Catalog tests:** `DiscoveryCatalogTests` helpers build `Features.All`, not the site ground.

## Design

### 1. Materials and saves

- **Enum and save version:**
  - `TerrainMaterialId` becomes `{ Soil, Backfill }` and `Last = Backfill`.
  - `WorldSaveCodec.Version` goes from 17 to 18; old saves are refused cleanly, so play New Game.
  - No gap-tolerant ids.
- **Catalog:**
  - `art/minerals/catalog.json` loses its removed `host_grounds`/`host_weights`; Sync Discovery Models
    rewrites `DiscoveryCatalog.asset`.
  - The host-ground mechanism (fields, `HostWeight` centre lookup, validation) stays for `111`/`112`.
  - The concrete-wall probe in `HostWeight` (285-291) and `Axis` go.
- **`EquipmentProgression`:**
  - Soil and Backfill responses only; `HardnessOrder` stays `{ Backfill, Soil }` (`108` flips it).
  - `GroundEffect` keeps the two arms. Backfill reads "Disturbed ground: loose fill" until `108`.
  - The Crack remap goes.
  - The override and admin tuning machinery stays.
  - `FpsPlayer.TunableGrounds` becomes `{ Soil, Backfill }`.

### 2. Generation (`TerrainGround`, `ExcavationGrid`, `TerrainVolume`)

- **`Features` becomes `{ None, Pits }`** (All = Pits). `SiteLayout.Ground` and `GroundFor` stay.
- **Stays in `TerrainGround`:**
  - `ZoneBorders` and `ZoneAt` (plain depth bands);
  - `SurfaceSoil`, the pit top and bottom margins (named constants replace `PlaceTop`/`EdgeBand`);
  - `Pits`, `PitSeats`, `Stashes`/`Stash` and `Inside`;
  - `OddSpot` as the unique space (Centre, Half, Min, Max; its `Ground` field goes);
  - `GroundLayout`;
  - `GroundJob` with Size, CellSize, Offsets, Pits and Output (`Material` returns Backfill in a pit,
    else Soil).
- **Deleted from `TerrainGround`:**
  - `PlaceKind`, `Place`, `Room`, `Rooms()`, the `Room` overload of `Seats()`, `Quotas` and `Places`;
  - the channels;
  - cracks, veins and gravel lenses;
  - `InPlace`, `InChannel`, `Cracked`, `GravelLens` and `Vein`;
  - `BorderWarp`;
  - the room seat constants (`SeatSink` stays: pit seats use it).
- **`GroundLayout`:** keeps `Pits` and `Stashes`. It also keeps `KeepOut()` as the reservation hook
  (empty until `110`) and `Seats()` for pits.
- **`ExcavationGrid`:**
  - delete `Rooms`, the `CarveRoom` loop and the method;
  - delete the per-material bore and shovel shapes (rock facets, concrete squares, gravel `Grain`);
  - keep the superellipse soil profile, `CarveHollow`, `LabCarve`, `Carve` and `LabBounds`;
  - delete `ExcavationGrid.Release.cs`.
- **`TerrainVolume`:**
  - delete `TerrainVolume.Release.cs` and `ReleaseGround`;
  - delete the room parts of `TerrainVolume.Rooms.cs` (`BrokeIntoRoom`, `Rooms`, `roomsOpened`,
    `CheckBreakIn`, `EmitBreakIn`);
  - keep `GroundLayout`, `MaterializeSeededAir` (minus rooms) and `MaterializeAround`; rename the
    file to fit (`TerrainVolume.SeededAir.cs`);
  - collapse `LastCutVolume` into `LastRemovedVolume`;
  - remove the pour particle fields from the scene (`MainGame.unity` 63660-63661) and
    `GroundTextureSetup.ConfigurePourFeedback`;
  - the shared `SoilCrumbs`/`SoilDust` materials stay (crane).
- **Break-in is deleted, not kept dormant.** `110` rebuilds it for geodes from this commit's
  `TerrainVolume.Rooms.cs` (`CheckBreakIn`, `EmitBreakIn`) and the particle pool in
  `TerrainVolume.Release.cs`. It generalizes them to a pocket record and saves the opened state.

### 3. Mesher and shader

- **One weight stream:** `TerrainChunkMesh(.Job)` carries one `Vector4` stream of up to four non-soil
  ground weights, with soil as the remainder.
  - `107` uses one slot (backfill); `110` and `111` take the next slots.
  - Keep the trilinear halo weights and the `DensityCache` material identity.
- **`GroundTriplanar.shader`:**
  - Keep: soil (with its cap and the plot band), backfill, `DepositSurface` (generic) and the X-ray.
  - Delete: the clay, rock, concrete, gravel and pond-clay branches, zone tints, `_ZoneDepths`,
    strata, and the fracture and crack lines.
  - Removing strata changes soil's colour slightly with depth; the playtest note says so.
- **`GroundTextureSetup`:** keep the soil and backfill binding and `PackTexture` (lakebed). Delete the
  rest and `RockDetail`.
- **Delete:** `DepositTextures.cs` and `art/deposit-textures/`.
- **Clean stale slots** from `ReservoirSediment.mat` (sweep list: textures 51-59, 75-83, 91-99,
  103-111; floats and colours 141-204).

### 4. Assets

- **Delete** (references checked against `.meta` GUIDs):
  - `Content/GroundTextures/Concrete_*` and `Gravel_*` (generated);
  - `Content/Nature/GroundTextures/_RockDetail_*` (copies; the vendor original stays).
- **Keep:** `Content/Nature/GroundTextures/Gravel_{a,m,n}`, which the lakebed's
  `PackedSediment.terrainlayer` uses.
- **Original soil art:** this is the one question for this ticket. Deleting `Soil_*`, `LICENSE.txt`,
  `GardenGround.mat`, the "Configure Original Soil" menu and `art/ground-textures` means the two tests
  switch to the dig ground's material, and the protected recovery scene `_Recovery/0.unity` then
  shows a missing material. **Recommended:** delete; the recovery scene file itself is not touched.
- **Art cards:** edit `art/pure-nature-mountains/README.md` (clay deposit) and `art/soil-debris/README.md`
  (gravel pour).

### 5. Tools

- **Ground Lab:**
  - Bays: Soil, Backfill and Backfill pit (in soil). The thin-roof bays and the bay `Cavity` flag go: with no collapses left there is nothing for them to show (`LabCarve` stays for the crane scenes).
  - The lab ground below the bays becomes soil.
  - The crane scenes stay.
  - Delete the `Crack` helper.
- **Ground X-ray:** marks Backfill only (legend, colours, class; context clearance goes).
- **Tool and crane presentation:**
  - `ToolRigPresenter` loses `crisp`, and `Family` drops removed ids (keep `MotionFamily.Hard` for
    `110`);
  - `SalvageCrane.Feedback` debris colours lose the removed arms;
  - `sink` stays until `108`.

### 6. Tests

- **Delete:**
  - `GroundReleaseTests.cs`.
  - `TerrainGroundTests`: zones, soft path, cracks, channels, sealed rooms (two tests) and places.
  - `TerrainMaterialTests`: `OneLevelOutpacesTheNextZonesMainGround` and
    `HeldDrillRetainsDistinctContours`.
  - `DiscoveryCatalogTests.HostGroundHoldsMoreOfItsTypes`.
  - `FindPhysicsIntegrationTests.PouredGravelDropsItsFinds`.
  - `TerrainIntegrationTests.BreakingThroughASealedRoomWallOpensItOnce` (`110` re-creates it for
    geodes from history).
- **Edit** to Soil/Backfill (sweep list):
  - `TerrainGroundTests`: pit tests and the site test;
  - `TerrainMaterialTests`: test cases, weight layout, material-only restore, motion cases,
    mixed-boundary cuts;
  - `DiscoveryCatalogTests`: helpers use `SiteLayout.Ground`; room seats and odd-spot ground parts
    are dropped, the keep-out parts kept;
  - `TerrainIntegrationTests`: detached column without `Released`, fresh scene, tool contact ids;
  - `DiscoveryIntegrationTests.InSealedRoom` and its uses;
  - the two `GardenGround` tests, if the soil art goes.

### 7. Docs

- **Concept:** rewrite as listed in the concept reference. In `05` §2, drop "drowned workshop". In
  `03` §1, drop the waterworks and drowned theming.
- **Snapshot docs:** baseline, architecture and `unity/readme.md`:
  - rewrite the ground, Ground Lab and X-ray parts;
  - fix the stale "v16" save note in the baseline.
- **Completed specs** `006`, `095`, `096`, `097`, `098` and `100`: each header gains one line:
  "Removed by 107 (asset-only grounds); the body is kept as history so it is not re-run."
- **Playtests:**
  - Delete the notes for removed features: `006`, `095`, `096`, `097`, `098` and `100-ground-lab`.
    These features no longer exist, so the notes are not "played", just obsolete.
  - Edit `001` (line 7), `104` (line 15) and `106` (X-ray still marks backfill; no slump).
  - `099` waits for `112`.
- **Queue:**
  - The standing decision changes to "the site holds soil plus backfill pits; grounds return through
    108–112".
  - `026`: a blast in soil and backfill; geode shells after `110`; a preview of the carve volume
    (DRG).
  - `099`: the odd-spot part waits for `112`; a detector-off pass no longer auto-removes the detector,
    the user decides.
  - `012`: hosts that exist.
  - `013`: trail types to be re-decided per zone in `111`.
  - `088`, `011` and `077`: drop sealed rooms, pours and soft paths.
  - `028`, `030`, `031`, `081`, `039`, `080` and `023`: drop removed grounds, rooms and pours.
  - The pending spec `docs/tasks/099-*.md` is rewritten the same way.

## Edge cases

- Lab and test grids that passed `Features.All` now mean Pits; uniques are still computed for pit
  avoidance.
- Seeded air (stash hollows, lab carves) still counts as modified for chunk rebuilds, and
  `RemovedVolume` stays 0 for it.
- A Ground Lab session still restores the site exactly afterwards (`UseGroundLab`).
- Catalog validation never meets an id above `Last` after the sync.

## Acceptance criteria

1. `TerrainMaterialId` is `{ Soil, Backfill }`. No code, test, shader branch, texture, art card or
   doc names a removed ground except completed spec bodies.
2. New Game works and the site plays as before; a v17 save is refused cleanly.
3. Undercutting a pit removes only what was cut; no release exists.
4. The Ground Lab shows three bays over soil plus the crane scenes, and the X-ray marks backfill.
5. Compiles warning-free; `tools/test-changed.ps1` passes. The build is delivered and the playtest
   note is updated.

## Playtest (`106` note, updated)

New Game, dig the plot centre down to the first chest, then undercut a pit's wall: nothing lets go.
The soil looks the same with depth, or flatter, which is expected without the old strata. The Ground
Lab shows soil, backfill and the pit, plus the crane scenes.

## Results

- Built as designed. The Ground Lab keeps three bays (soil, backfill, a backfill pit) and the crane scenes; the
  thin-roof bays went too, since nothing collapses any more (user, 2026-10-05).
- The drill's cross-section is now round for every ground, stretched by each ground's bite width and length, as
  the shovel's already was.
- The old original soil art went with `GardenGround.mat`; the scene builder's step that also set up the
  excavation lighting became `GroundTextureSetup.ConfigureExcavationLighting`.
- After the job structs changed, the open Editor kept running stale Burst code (garbage material bytes, empty
  meshes, a hung PlayMode run). Clearing `unity/Library/BurstCache` with the Editor closed fixed it; no code
  change was needed.
