# Asset: Pure Nature 2: Mountains
- **Purpose:** Approved meadow plants, turf/soil/sediment/rock/gravel textures and sky for the round dig area. Surroundings come from Pure Nature 2: Highlands (`art/pure-nature-highlands`).
- **Source/License:** User-purchased [BK Asset Store pack](https://assetstore.unity.com/packages/3d/environments/pure-nature-2-mountains-269088) v2.2, Unity Asset Store EULA; keep source access restricted.
- **Unity Path:** `unity/Assets/BK/`; project copies under `Content/Nature`, `Content/Site` and `Content/Environment`.
- **Integration:** Meadow setup configures eleven plant layers and pack ground throughout, with a soft turf/soil transition. Ground deposits reuse fine gravel as tinted compacted sediment and the tileable rock detail set; linear masks retain alpha, with close-range filtering.
- **Publisher patch:** Eight matching shaders updated from the [6000.4.0 shader archive](https://www.bk-prod.fr/readme); imported GUIDs retained. Reapply after original-pack reimport.
- **Grass adaptation:** Project-owned `Content/Nature/ExcavationGrass.shader` adds hole/perimeter clipping through `Runtime/Terrain/SurfaceGrassSupport.hlsl`; regenerate via `SurfaceGrassSetup.ConfigureGrassShader()` after vendor shader updates. Vendor source stays intact.
- **Status/workflow:** Full vendor import and metadata preserved in private Git, vendor binaries in LFS. Tune project-owned copies; the vendor demo remains a reference.
