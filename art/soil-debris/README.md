# Soil debris
- **Item:** soil clod mesh (a once-subdivided icosahedron with jittered, flattened corners and faceted normals) and the soil-break particle look (crumbs, dust, clods).
- **Purpose:** the earth the salvage crane's rope tears out: clods tumble, land on the dug floor and sink away, with crumbs, dust and a trickle off the broken face.
- **Source/License:** original procedural geometry, generated at runtime by `SalvageCrane.Feedback.ClodMesh`; shading by `Runtime/Terrain/SoilBreak.shader`. No external content.
- **Unity path:** materials `Assets/Content/GroundTextures/SoilClods.mat`, `SoilCrumbs.mat`, `SoilDust.mat` (shared with the gravel pour), wired by **Configure Salvage Crane**.
- **Status:** integrated in MainGame.
