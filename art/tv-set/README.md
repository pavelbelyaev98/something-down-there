# Asset: TV Set
- **Item:** Four televisions by decade (`TV_70` portable, `TV_80`, `TV_90`, `TV_00` flat screen), 100-800 triangles each.
- **Purpose:** Junk finds for the recent fill.
- **Source/License:** User-imported free [Dmitriy Dryzhak Asset Store pack](https://assetstore.unity.com/packages/3d/props/electronics/tv-set-26193) (2026-10-05), Standard Unity Asset Store EULA (Extension Asset); modifying for the game is allowed, redistributing the models is not.
- **Unity Path:** `unity/Assets/_Television_set/` (vendor files untouched; its `_pic` prefabs, picture and Shader Forge shader are unused); project copies in `Content/BuriedProps/TVSet`.
- **Integration:** Configure Buried Props builds `Tv_1_2.mat` and `Tv_3_4.mat` (URP Lit on the vendor's colour and normal maps, plus masks whose smoothness `make_masks.py` derives from the pack's Shining maps as its shader read them) and a prefab variant per TV that swaps them in.
- **Status:** In the game as four junk finds lying loose in the soil like any find (106, 113): `catalog.json` holds their find policy (price, count, `model_scale`: the wood-cased, CRT and flat-screen sets are scaled up to real sizes); Sync Discovery Models builds them into `Content/Discoveries/Junk`.
