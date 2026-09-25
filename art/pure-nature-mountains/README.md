# Asset: Pure Nature 2: Mountains
- **Purpose:** Ground textures for the dig plot and lakebed, the sky, and lakebed plants and pebbles. Surroundings come from Pure Nature 2: Highlands (`art/pure-nature-highlands`).
- **Source/License:** User-purchased [BK Asset Store pack](https://assetstore.unity.com/packages/3d/environments/pure-nature-2-mountains-269088) v2.2, Unity Asset Store EULA; keep source access restricted.
- **Unity Path:** `unity/Assets/BK/`; project copies under `Content/Nature`, `Content/Environment` and `Content/Lakebed`.
- **Integration:** Project copies: ground textures in `Content/Nature/GroundTextures` (gravel is the plot cap and lakebed sediment layer, mud the dig soil); `DryTurf.terrainlayer` (tinted `Grass01`), `DryReeds.mat` and `DryRushes.mat` with prefab variants in `Content/Lakebed`.
- **Shader patches:** Eight matching shaders came from the [6000.4.0 publisher archive](https://www.bk-prod.fr/readme). Nine shared BK shaders use URP's current cluster-light-loop macros and GBuffer output API in place of deprecated aliases; GUIDs are retained. Reapply after pack reimport; this declaration update also covers the project water copy.
- **Status/workflow:** Full vendor import and metadata preserved in private Git, vendor binaries in LFS. Tune project-owned copies; the vendor demo remains a reference.
