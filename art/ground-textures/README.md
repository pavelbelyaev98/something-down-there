# Asset: Ground Textures — Soil

- **Purpose:** Original soil art for the standalone `GardenGround` material; the dig ground's topsoil is now the Mountains clay loam.
- **Source & License:** Original Blender MCP bakes (`GroundTextures.blend`, `trials/A-Sunny-r8/`, etc.); [LICENSE.txt](LICENSE.txt) (free commercial use, no attribution).
- **Unity Path:** `unity/Assets/Content/GroundTextures/`: the current bake `Soil_*` (albedo = `trials/A-Sunny-r8`), the first bake `SoilFirstBake_*` (restored from git, 2026-09-09) and `Soil_Albedo_Muted.png` (the current albedo with its saturation reduced, restored from git, 2026-09-26). The two grass turf bakes (`trials/A-Sunny-r6` and the first) are not imported.
- **Setup / Tooling:** Applied via triplanar material `GardenGround` (`Content/GroundTextures/GroundTriplanar.shader`).
- **Pour debris:** `SoilCrumbs.mat`/`SoilDust.mat` here draw the gravel pour's crumbs and dust with the original procedural `Soil Break` shader (`Runtime/Terrain/SoilBreak.shader`); no textures.
- **Soil look variants:** "Original soil", "First soil bake", and the earlier topsoil tints "Loam", "Dark loam" and "Olive silt" on the muted albedo, in `SoilLooks.asset` (Developer admin **Soil look**, built by **Configure Soil Looks**).
- **Status:** Not the dig ground's default since clay loam replaced it; `GardenGround` and the soil look variants reference it.
