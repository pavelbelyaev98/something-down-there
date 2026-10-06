# Asset: Animated Old Chest
- **Item:** Old wooden chest (about 1.3 m long) with a lock, a strap and a legacy `ChestAnim` clip (about 3 s) that drops the lock and opens the lid.
- **Purpose:** A buried stash container, opened where it lies.
- **Source/License:** User-imported free [NOTLonely Asset Store pack](https://assetstore.unity.com/packages/3d/props/animated-old-chest-20179) (2026-10-05), Standard Unity Asset Store EULA (Extension Asset); modifying for the game is allowed, redistributing the model is not.
- **Unity Path:** `unity/Assets/NOT_Lonely/OldChest/` (vendor files untouched; its demo scene is unused); project copies in `Content/BuriedProps/OldChest`.
- **Integration:** Configure Buried Props builds `OldChest.mat` (URP Lit on the vendor's colour and normal maps plus `OldChest_Mask.png`, metallic from its grey specular map and smoothness from its alpha, written by `make_mask.py`) and the `OldChest.prefab` variant. Sync Discovery Models turns that into the stash chest (`Content/Discoveries/Chest/OldChest.prefab`: body, box colliders, measured hollow, the pocket of air it stands in, six seats at the back, its rim and lid space) from `catalog.json`, which also names what it holds (the Mining pack's ingots and the Crystal Caverns gem crystals).
- **Status:** In the game: one chest per stash pit (106).
