# Asset: Stylized Western Shovel
- **Purpose:** The shovel in first person at tool levels 1-6.
- **Source/License:** User-purchased [PijayArt Asset Store pack](https://assetstore.unity.com/packages/3d/props/tools/stylized-western-shovel-185312) (2026-10-04), Unity Asset Store EULA (single entity); modifying for the game is allowed, redistributing the model is not; keep source access restricted.
- **Unity Path:** `unity/Assets/StylizedShovel/` (vendor mesh and maps, untouched; the unused demo scene, prefab and Built-in materials are dropped); project copies in `Content/ToolRig/WesternShovel`.
- **Integration:** Configure Tool Rig places the vendor mesh as bought, and a URP Lit `WesternShovel.mat` on the vendor's maps as imported except metallic, halved in a project copy (`make_half_metal.py`) so the steel reads grey in the game's light; it also points the model's own material slot at `WesternShovel.mat`.
- **Status/workflow:** Vendor files and metadata in private Git, binaries in LFS. Tune the project copies; reimporting the pack changes nothing of ours (delete its demo files again).
