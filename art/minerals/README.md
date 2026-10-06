# Asset: Minerals (8 Depth Tiers)

- **Item:** The sellable minerals, shallow to deep: Coal, Copper, Iron, Silver, Gold, Emerald, Ruby, Diamond (`catalog.json`).
- **Source/License:**
  - Coal: the approved photo rock's model and collider ([card](../photo-rock/README.md)) with a coal-black map made from the rock's own maps; `make_coal.py` writes `coal/` (maps and model copies). `Minerals.blend` and `create_minerals.py` keep only the retired Blender placeholders ([LICENSE.txt](LICENSE.txt)).
  - Copper, iron, silver, gold: the bought Mining Tools, Ore & Ingots ore chunks in a plainer, duller ore style ([card](../mining-pack/README.md)).
  - Emerald, ruby, diamond: the bought Crystal Caverns beryl, ruby and quartz crystals, soil-caked and in project colours ([card](../pure-nature-crystal-caverns/README.md)).
- **Unity Path:** `unity/Assets/Content/Minerals/`; the bought looks come from `Content/BuriedProps` (Configure Buried Props).
- **Setup:** `Tools > Something Down There > Sync Mineral Models`.
- **Status:** In the game: the lineup of `114`, a pair per zone. Only the rock is a signed-off design; the rest await the user's playtest.
