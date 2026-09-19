# Asset: Pure Nature 2: Mountains
- **Purpose:** Purchased environment library; its plain grass supplies the excavatable surface vegetation.
- **Source/License:** User-purchased [BK Asset Store pack](https://assetstore.unity.com/packages/3d/environments/pure-nature-2-mountains-269088) v2.2, Unity Asset Store EULA; keep source access restricted.
- **Unity Path:** `unity/Assets/BK/`; project material overrides in `unity/Assets/Content/Nature/`.
- **Integration:** `SurfaceGrassSetup` binds the vendor mesh and wind manager to the existing soil-aware instanced renderer; MainGame retains lighting ownership.
- **Publisher patch:** Eight matching shaders updated from the [6000.4.0 shader archive](https://www.bk-prod.fr/readme); imported GUIDs retained. Use this update after reimporting the original pack.
- **Workflow:** Preserve vendor files; tune project copies/variants. Commit the full pack plus metadata to private Git with scoped binary LFS rules; developer guide covers setup.
- **Status:** Integrated surface grass; remaining assets are an approved library, not placed in MainGame.
