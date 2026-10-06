# Soil debris
- **Item:** soil clod meshes (four once-subdivided icosahedra with jittered, flattened corners, faceted normals and per-face soil-grain UVs) and the soil-break particle look (clods and crumbs as those meshes, dust and shaft motes from the Crystal Caverns dust flipbook).
- **Purpose:** the earth the salvage crane's rope tears out: clods tumble and crumble where they land, with crumbs, dust and a trickle off the broken face.
- **Source/License:** original procedural geometry, generated at runtime by `SalvageCrane.Feedback.ClodMesh`; shading by `Runtime/Terrain/SoilBreak.shader` with the backfill albedo's grain and the Crystal Caverns `Fx/Dust_a` flipbook (approved pack, see its card).
- **Unity path:** materials `Assets/Content/GroundTextures/SoilClods.mat`, `SoilDust.mat`, wired by **Configure Salvage Crane**.
- **Status:** integrated in MainGame.
