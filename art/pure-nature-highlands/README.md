# Asset: Pure Nature 2: Highlands
- **Item:** BK Pure Nature 2: Highlands — terrain, cliffs, peaks, boulders, rubble, ruins, trees, plants, water and waterfalls.
- **Purpose:** The drained lakebed surroundings: MainGame recreates the demo's river canyon around the dig area.
- **Source/License:** User-purchased BK Asset Store pack, Unity Asset Store EULA; keep source access restricted.
- **Unity Path:** `unity/Assets/BK/PureNature_Highlands/` (shared `Pure_Common`); generated terrain data, rim and water meshes in `Content/Lakebed`.
- **Integration:** `LakebedSiteSetup` reads the unmodified demo; scenery stays vendor prefab instances, with project tree variants under `Content/Lakebed/Trees` for distant-only LOD switches, backdrop shadow removal and narrow-band leaf-card hiding (`Trees/Materials` copies; Inspector tuning is kept). The vendor `Watersplash.mat` lacks its textures, so splash particles are omitted. Project-owned lake/river materials and `LakebedWater.shader` correct refraction/foam; regenerate through `ConfigureWaterShader()`. The rim shares meadow soil; Highlands grading and baked probe projection use the project URP setup.
- **Status/workflow:** Approved and in use. Opening the demo lets Unity auto-upgrade a few vendor files (URP asset, particle materials); discard those diffs.
