# Asset: Pure Nature 2: Highlands
- **Item:** BK Pure Nature 2: Highlands — terrain, cliffs, peaks, boulders, rubble, ruins, trees, plants, water and waterfalls.
- **Purpose:** The drained lakebed surroundings: MainGame recreates the demo's river canyon around the dig area.
- **Source/License:** User-purchased BK Asset Store pack, Unity Asset Store EULA; keep source access restricted.
- **Unity Path:** `unity/Assets/BK/PureNature_Highlands/` (shared `Pure_Common`); generated terrain data, rim and water meshes in `Content/Lakebed`.
- **Integration:** `LakebedSiteSetup` reads the unmodified demo; scenery stays vendor prefab instances. Project-owned lake/river materials and `LakebedWater.shader` correct doubled refraction lighting and unsafe foam math; regenerate the shader through `ConfigureWaterShader()`. The rim shares the meadow soil material. Highlands grading and baked probe projection use the project URP setup.
- **Status/workflow:** Approved and in use. Opening the demo lets Unity auto-upgrade a few vendor files (URP asset, particle materials); discard those diffs.
