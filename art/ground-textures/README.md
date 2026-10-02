# Asset: Ground Textures — Soil

- **Purpose:** Original soil art for the standalone `GardenGround` material; the dig ground's topsoil is now the Mountains clay loam.
- **Source & License:** Original Blender MCP bakes (`GroundTextures.blend`, `trials/A-Sunny-r8/`, etc.); [LICENSE.txt](LICENSE.txt) (free commercial use, no attribution).
- **Unity Path:** `unity/Assets/Content/GroundTextures/` (soil Albedo, Normal and Surface Masks; albedo = `trials/A-Sunny-r8`).
- **Setup / Tooling:** Applied via triplanar material `GardenGround` (`Content/GroundTextures/GroundTriplanar.shader`).
- **Pour debris:** `SoilCrumbs.mat`/`SoilDust.mat` here draw the gravel pour's crumbs and dust with the original procedural `Soil Break` shader (`Runtime/Terrain/SoilBreak.shader`); no textures.
- **Status:** Not used by the dig ground since clay loam replaced it (confirmed again by a soil look A/B, task 104); only the unbound `GardenGround` material references it.
