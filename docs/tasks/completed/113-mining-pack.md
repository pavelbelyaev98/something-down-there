# 113 — Mining Pack: Ores, Crystals, Treasure and a Second Shovel

**Status:** complete:
- Copper, iron, silver and gold are the bought Mining Tools, Ore & Ingots ore chunks; emerald, ruby and diamond
  are Crystal Caverns crystals in project colours, two looks each.
- The old chests hold the pack's coins and ingots, taken one at a time with Interact; digging and walking never
  take them.
- Developer admin switches the level 1–6 shovel between the Western one and the pack's.
- Only the pack files the game uses are committed; its installers are ignored.

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
2. Each chest holds five coins or ingots and nothing else; the treasure lies nowhere else
   (`EveryStashChestHoldsItsContentsAndNothingElse`). E takes each piece; digging and walking never do
   (`ItsTreasureIsTakenByHandOnePieceAtATime`).
3. Developer admin switches the shovel look for levels 1–6 (checked in first person) (`ToolRigTests`).
4. The used pack maps import at 1024 px; installers are ignored and only used files are committed.
