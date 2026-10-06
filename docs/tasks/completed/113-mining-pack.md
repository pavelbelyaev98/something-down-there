# 113 — Mining Pack: Ores, Crystals, Treasure and a Second Shovel

**Status:** complete:
- Copper is the bought Mining Tools, Ore & Ingots ore chunks at the pack's quality; iron, silver and gold are
  native nuggets on its rocks; emerald, ruby and diamond are Crystal Caverns crystals in project colours, two looks each.
- The old chests hold only the pack's bronze, silver and gold ingots, heaped at the back and richer the deeper
  the chest, taken one at a time with Interact; digging and walking never take them.
- The Ground Lab sets out every find's looks to compare.
- Only the pack files the game uses are committed; its installers are ignored. The pack's coins and its shovel
  were tried and dropped.

## Objective

Bring the bought pack (`unity/Assets/REAL_DEDICATED/`) into the game.
- Its ores become the metal minerals.
- The old chests hold real ingots and coins, worth plenty, taken by hand.
- Its shovel becomes a comparison look.
- The game and the repository stay small: the pack is 3.9 GB on disk.

The same request also added the crystals (deep finds), and asked that any bought pack's textures may dress new
items. AGENTS.md "Bought Before Made" now says so.

## User direction (chat, 2026-10-06)

- "Use those [ores and ingots] in game; if needed use the textures and apply them elsewhere."
- "For the items inside the chest I want to click E to collect instead of autopick; actual ingots and coins
  inside... they might cost plenty."
- "Enable an option in the admin menu to switch between this and the shovel I am already using."
- "It is so many gigabytes, I don't want my game huge... I aim towards light low-poly stylized, not too
  cartoonish but not too real; more LOD is nicer; the round gold coins are meh, squared ones are better."
- "The crystals can be found at the lower levels or/and inside the chests." The mineral lineup itself moves to
  `114`.

## Why the pack was 3.9 GB

- **Installers:** two nested installer packages for the other pipelines (`*_BRP.unitypackage`,
  `*_URP.unitypackage`), 948 MB each. They are never needed once imported.
- **Textures:** 69 uncompressed 4K/2K `.tga` maps, about 2 GB, most of them the rock sets.
- **The build:** a Unity build takes only the textures the game references, at their import size. The used maps
  are capped at 1024 px, so the build grows by a few MB.
- **Committed:** only the 65 files the game uses (310 MB of source maps, in LFS). The rest stays local and
  untracked until the user decides.

## Design and implementation

- **Materials (`BuriedPropsSetup`):** the pack's loose materials are HDRP and draw as the error shader. Configure
  Buried Props builds URP Lit copies and prefab variants in `Content/BuriedProps/MiningPack`:
  - `FromHdrp` reads the saved properties with a `SerializedObject`, so it works without the HDRP shader;
  - the HDRP mask (R metallic, G occlusion, A smoothness) feeds URP's metallic-gloss and occlusion maps unchanged;
  - coins keep the vendor's per-metal tint.

  Crystal Caverns crystals get `FromCrystal` copies in `Content/BuriedProps/CrystalCaverns` (colour map, our tint,
  normal, glassy, not metal). The pack's crystal shader is not used, since buried finds draw through Excavation
  Lit.
- **Finds from props (`PropBake`, `PropFindSetup`):**
  - `JunkSetup` became `PropFindSetup`, one importer for every find made from a bought prop: the TVs and the
    treasure (`art/mining-pack/catalog.json`).
  - `PropBake` merges a prop's LOD0 meshes, centred. Its hull is the coarsest vendor LOD (convex), or a box for
    props without LODs. The find's own detail levels come from `GenerateDetailLevels`.
  - Further looks (`prop_looks`) get their own save keys (`*_b`).
  - Minerals take a `prefab` and `prop_looks` in place of a Blender model.
- **Taking by hand:**
  - `BuriedFind.handPicked` comes from the catalog (`hand_picked`) and refuses `TryCollect` (the dig action) and
    `TryCollectNearby` (proximity, also skipped by `FindProximityCollection`).
  - `TryInteract` takes it with the shared aimed-reach path.
  - Its prompt reads "E to take".
- **Treasure:**
  - Coins: copper, silver, gold (octagon A/B).
  - Ingots: copper, silver, gold (flat, with thin as the second look).
  - Prices are in the catalog, well above anything loose in the soil around the chest.
  - 15 instances across three chests of five floor seats (`ChestSetup` seats: middle and towards each corner). The
    chest's contents are those six types weighted by count, so the treasure lies only in chests.
- **Shovel A/B:**
  - `ToolRigSetup` adds `L01-06_Blade__Mining`: the pack's shovel turned to stand on its blade's tip, at the Western
    blade's tip, drawn 1.6x so its blade is about as wide on screen.
  - The presenter treats `__<Look>` suffixes as looks of one part and shows `FpsPlayer.ShovelLook`
    ("Western" unless Developer admin **Shovel** picks "Mining").
  - The toggle is session-only and reset with the other admin overrides.

## Rejected or changed during the work

- **Gemstones:** the pack's cut gemstones (a cube and a cut diamond) read as toys. The gems take crystal clusters
  instead: beryl tinted emerald green, its ruby darker than the pack's flat red, quartz a clear white.
- **Sky reflections:**
  - Mirror-smooth silver at the bottom of an open shaft read sky blue: 88% daylight reaches 4 m down a narrow
    shaft, and URP's environment reflection is the sky.
  - Two changes: coins and ingots are tarnished (55% of the pack's smoothness, reading as old silver and copper).
    `ExcavationLighting.hlsl` now also fades the environment reflection by the route's daylight, so metal deep in
    tunnels stops reflecting the sky (the chest's iron bands too).
- **Three items per chest** looked sparse (one ingot and two small coins in a 1.3 m chest). Five seats, with the
  10-slot starting bag in mind.
- **Saving baked meshes:**
  - Baking over the old Blender minerals' mesh assets by `CopySerialized` scrambled the A looks (scattered
    triangles); the old vertex layout survived the copy.
  - `PropBake` now clears and refills an existing mesh in place, keeping its GUID.
  - The seven replaced Blender minerals' models, maps and materials, and their exports in `art/minerals`, are
    deleted; coal stays.

## Acceptance

1. Copper, iron, silver and gold show the pack's chunks and emerald, ruby and diamond the crystals, two looks
   each. They are checked in previews and in a wall under the game's lighting.
2. Each chest holds five ingots or crystals and nothing else; the treasure lies nowhere else
   (`EveryStashChestHoldsItsContentsAndNothingElse`). E takes each piece; digging and walking never do
   (`ItsTreasureIsTakenByHandOnePieceAtATime`, `WalkingCollectsEveryUncoveredAppearanceWithoutAimOrDig`).
3. The shovel A/B ran and ended (the Western shovel stays); the Ground Lab gallery shows every find's looks.
4. The used pack maps import at 1024 px; installers are ignored and only used files are committed.

## Iteration (user, 2026-10-06, after a first look)

- **Shovel A/B:** "I prefer the Western shovel; the other one is bad, remove it and the admin option." The pack
  shovel part, `FpsPlayer`'s look flag and toggle, and the presenter's look suffixes are gone. Its files are no longer
  committed.
- **Coins:** "Remove the individual coins, this is messy." Chests hold only ingots and crystals (see also `109`).
  - The crystals are the gem crystals at chest size, a new treasure source on the Crystal Caverns card
    (`art/pure-nature-crystal-caverns/catalog.json`), taken by hand.
  - The counts still match the chests' seats.
  - The coin assets are deleted and their vendor files untracked.
- **Ground Lab gallery:** "Add all minerals as non-pickable somewhere in the Ground Lab so I can look at them."
  - `DiscoveryField.SpawnGallery` copies every look of every common find onto the lab's surface north of the bays:
    a row of minerals, a row of chest treasure and a row of TVs.
  - It strips each copy's `BuriedFind` and `FindPhysics`, so it stays solid but is never taken.
  - `LabExhibit` names the look on aim, with its price and depth.
- **Bigger ingots, more per chest:** ingots at 1.1x the pack's size; six floor seats in two rows of three; a
  smaller random turn (±12°) so they keep clear of each other; 18 pieces across three chests.
- **Lighting** ("the crystal is pure white; the ingots' light is also off"):
  - Fully metallic ingots showed only the reflected sky, a flat glowing colour. They are now aged metal: colour map
    at 62%, half metal, smoothness 0.42, keeping the mask's occlusion. They shade like the wood around them.
  - The crystals took darker tints (diamond a cool grey-blue instead of near-white) and at most 0.75 smoothness.
  - These were compared on the Ground Lab chest in full sun and checked in the first stash under its shaft.
- **Ground Lab chest:** "also add an openable chest with valuables inside." `DiscoveryField.SpawnLabChest`
  stands an old chest on the surface west of the open pit, its lock to the south, its base 2 cm into the ground so
  its footing holds. It holds one each of the chest contents' most valuable kinds, as real finds taken by hand.

## Iteration 2 (user, 2026-10-06)

- "When opening, items are placed with equal spacing and some are hard to see; I'd prefer to clutter items at the
  back of the chest." The six seats sit at the back: three along the back wall, two over the gaps between them, one
  before those. `DiscoveryCatalog.ChestHeap` sets each piece on the pieces already under it (by footprint), tipped
  up to 20°, no higher than the chest's rim (`BuriedChest.Rim`). One that would stand higher (the chest is only about a
  crystal deep inside) takes the nearest place on the floor inside the walls (`BuriedChest.FloorHalf`) where it fits.
  Pieces never start inside each other: a first fallback laid one level over another, and pushed apart it went through
  the chest's floor. Physics
  settles them into a pile. Tried first: a second row lifted 12 cm in front of the first. It was too far apart to
  land on it, so it lay flat in a 2 x 3 grid.
- "Earlier chests have bronze primarily, some silver and maybe some gold, while deeper chests have more gold and
  crystals." Each content type has a shallow and a deep weight (`old-chest/catalog.json`), blended by the chest's
  depth through the recent fill. Shallow chests run bronze 6, silver 3, gold 1. Deep chests run bronze 1,
  silver 3, gold 4, emerald 2, ruby 2, diamond 1.
- Bronze ingots replace copper ones: copper read too close to gold.
- "It's weird smaller crystals cost more than bigger crystals." The chests' clean crystals are now cheaper than the
  big, dirty ground gems of `114`.

## Iteration 3 (user, 2026-10-06: "in chests I want only ingots, no crystals")

- The chests hold ingots only: the crystal contents, their catalog (`art/pure-nature-crystal-caverns/catalog.json`),
  prefabs, materials and find assets are gone. Bronze 8, silver 6 and gold 4 instances fill the three chests'
  eighteen seats.
- The Ground Lab's buried chest moved into the 3 x 3 m backfill bay, 3 m down: the 1 m pit was too tight to dig in.

## Iteration 4 (user, 2026-10-06)

- "Add more ingots": ten seats per chest (four more toward the middle), so three chests hold 13 bronze, 10 silver and
  7 gold. The lab's chests are full, the kinds in turn.
- Copper ore "a bit higher quality, like silver and gold": the pack's own map size, relief and gloss on the
  copper-rich maps; the plainer ore style is gone.
