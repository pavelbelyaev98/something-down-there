# Asset: Ground Textures — Soil

- **Purpose:** Freshly cut topsoil on the dig ground (default and two compared options), via a muted colour copy.
- **Source & License:** Original Blender MCP bakes (`GroundTextures.blend`, `trials/A-Sunny-r8/`, etc.); [LICENSE.txt](LICENSE.txt) (free commercial use, no attribution).
- **Unity Path:** `unity/Assets/Content/GroundTextures/` (soil Albedo, Normal and Surface Masks; `Soil_Albedo_Muted.png`, the albedo with its saturation reduced by `GroundTextureSetup.MutedSoilAlbedo`; retired custom turf imports deleted).
- **Setup / Tooling:** Applied via triplanar material `GardenGround` (`Content/GroundTextures/GroundTriplanar.shader`).
- **Status:** In use as topsoil on `ReservoirSediment` (tinted per option in `LakebedSiteSetup.DigGround.cs`); the standalone `GardenGround` material stays unbound.
