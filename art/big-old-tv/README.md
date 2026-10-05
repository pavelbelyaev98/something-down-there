# Asset: Big old TV
- **Item:** One CRT television (about 0.7 m wide, 242 triangles).
- **Purpose:** A junk find for the recent fill.
- **Source/License:** User-imported free [Maximalist Asset Store pack](https://assetstore.unity.com/packages/3d/props/electronics/big-old-tv-186170) (2026-10-05), Standard Unity Asset Store EULA (Extension Asset); modifying for the game is allowed, redistributing the model is not.
- **Unity Path:** `unity/Assets/JustPlay/Old TV/` (vendor files untouched; its demo scene and lighting settings are unused); project copies in `Content/BuriedProps/BigOldTV`.
- **Integration:** Configure Buried Props builds `BigOldTV.mat` (URP Lit on the vendor's colour, normal and AO maps, plus `BigOldTV_Mask.png`, smoothness inverted from its roughness map by `make_mask.py`) and the `BigOldTV.prefab` variant that swaps it in for the vendor's Built-in Autodesk Interactive material.
- **Status:** In the game as the junk find "Big old TV", found only in rubbish pits (106): `catalog.json` holds its find policy; Sync Discovery Models builds it into `Content/Discoveries/Junk`.
