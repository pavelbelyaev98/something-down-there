# Asset: Pure Nature 2: Mountains
- **Purpose:** Surface grass and the dry reservoir's cliffs, edge stones, conifers, debris, clouds and distant mountains.
- **Source/License:** User-purchased [BK Asset Store pack](https://assetstore.unity.com/packages/3d/environments/pure-nature-2-mountains-269088) v2.2, Unity Asset Store EULA; keep source access restricted.
- **Unity Path:** `unity/Assets/BK/`; project material overrides in `unity/Assets/Content/Nature/`.
- **Integration:** `SurfaceGrassSetup` binds soil-aware grass; `ReservoirEnvironmentSetup` authors scenery and enables triangle collision baking on its seven rock model imports. MainGame owns lighting; BK supplies wind only.
- **Publisher patch:** Eight matching shaders updated from the [6000.4.0 shader archive](https://www.bk-prod.fr/readme); imported GUIDs retained. Use this update after reimporting the original pack.
- **Workflow:** Preserve vendor files; tune project copies/variants. Commit the full pack plus metadata to private Git with scoped binary LFS rules; developer guide covers setup.
- **Status:** Integrated in MainGame with LODs, project material overrides and a local rock waterline shader; no vendor water or demo scene imported into gameplay.
