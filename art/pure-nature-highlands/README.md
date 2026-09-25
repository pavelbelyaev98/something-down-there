# Asset: Pure Nature 2: Highlands
- **Item:** BK Pure Nature 2: Highlands — terrain, cliffs, peaks, boulders, rubble, ruins, trees, plants, water and waterfalls.
- **Purpose:** The drained lakebed surroundings: MainGame recreates the demo's river canyon around the dig area.
- **Source/License:** User-purchased BK Asset Store pack, Unity Asset Store EULA; keep source access restricted.
- **Unity Path:** `unity/Assets/BK/PureNature_Highlands/` (shared `Pure_Common`); the generated terrain, rim and water meshes in `Content/Lakebed` have their own card (`art/lakebed-site`).
- **Integration:** `LakebedSiteSetup` reads the unmodified demo; scenery stays vendor prefab instances. Project copies in `Content/Lakebed`: tree variants and foliage materials (`Trees`), dry grass variants (`DryGrass.mat`), bare rock (`LakebedRock.mat`), lake/river/trickle water materials and the adapted `LakebedWater.shader` (regenerate through `ConfigureWaterShader()`). The vendor `Watersplash.mat` lacks its textures, so splash particles are omitted.
- **Status/workflow:** Approved and in use. Opening the demo lets Unity auto-upgrade a few vendor files (URP asset, particle materials); discard those diffs.
