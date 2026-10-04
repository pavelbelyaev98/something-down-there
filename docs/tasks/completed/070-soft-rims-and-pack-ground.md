# 070 — Soft rims and pack ground

**Status:** complete. Feathered the turf/soil transition, corrected noon shadow bias and unified active ground on pack soil; custom soil art remains stored but unbound.

## Objective
Give excavated meadow edges a softer, natural transition and use approved pack ground throughout the active top layer. Retain custom soil art for later use without binding it to the active terrain.

## Concept reference
`09_FEEL_ART_AND_AUDIO.md`: dry matte ground, continuous meadow, readable natural excavation edges.

## Current implementation
- `GroundTriplanar.shader` uses a very thin, almost binary height cutoff for turf. Its contour follows the surface-net triangles, producing pointed steps around cuts.
- `GroundTextureSetup.ConfigureMeadowMaterials` binds custom soil to the comparison slots and enables the west/east split. `ReservoirSediment` owns active terrain and its preview; `GardenGround` retains custom soil assets.
- Excavation meshes and collision share `TerrainChunkMesh`; this iteration first corrects the material transition without changing excavation authority or saved density.

## Changes
- Use a bounded, softly feathered turf transition with texture-driven variation; keep intact ground fully grassy and lower walls fully soil. Preserve continuous top projection and avoid a dark root outline.
- Adjust the noon sun's depth bias to prevent terrain self-shadow contours; retain ordinary terrain, foliage and find shadows.
- Make meadow setup use pack soil everywhere, disable the comparison and clear inactive custom texture references on the active material. Retain custom textures, their source art and the inactive material.
- Update the setup menu/name, scene configuration regression and current design/baseline/asset cards to match the selected ground.
- Test automation prepares an empty scene before starting the runner, discarding unsaved scene experiments under the user's standing permission. Save authored task changes first; never save or overwrite recovery scenes.

## Edge cases
Repeated shallow cuts, connected cuts, close and grazing views, the former comparison seam, uncut meadow, foliage over holes, matte soil and underground darkness. Setup reruns must not restore the comparison.

## Acceptance
- Close-up and ordinary views show a softer grass/soil transition without sharp sawtooth colouring or a dark outline.
- Both former comparison halves use the pack ground; custom art remains available and is not referenced by active terrain materials.
- Shader and relevant scene/grass checks pass, Odin validation is clean, and the fresh Windows executable builds and launches.

## Verification
- Reviewed connected cuts from standing and close viewpoints with live meadow foliage and ordinary shadows. The feathered edge and adjusted sunlight bias remove the sharp material cutoff and self-shadow outline.
- Both scene checks and all three grass integration checks passed. Odin reported 14,490 valid checks and no issues; both ground and foliage shaders compiled without messages.
- Deliberately dirtied MainGame and ran `test-changed.ps1`: the preflight discarded the temporary edit without prompting, all three grass checks passed, and the scene file hash remained unchanged. Recovery and vendor assets were untouched.
- Active MainGame dependencies exclude custom soil textures/material; source art and textures remain stored. Windows build succeeded and passed startup smoke testing. Its only warning is the intentionally disabled Pipeline player bridge.

## Iteration: flat surface up to the rim (2026-10-03)
- Feedback: holes looked "pixelated and blurry" around their rims, with blurred dark spikes on the light top
  layer (narrow lab shaft). Cause: a surface-net cell where the surface meets a hole's wall averaged the two
  crossings, sinking its vertex 1-9 cm by a different amount per cell; the ring of slanted triangles fanned
  out round the rim, and contact shading (SSAO, half resolution) darkened each crease into a blurred spike.
  The lighting looked flat (the cap normal is forced up), so only the AO showed it; at AO 0 it vanished.
- Tried first: snapping only vertices within 3.5 cm of the surface. The rim cells sit deeper, so nothing
  changed. SSAO variants (full resolution, more samples, blue noise, depth-normals) all kept the spikes.
- Now any cell with a surface crossing within 3.5 cm of the top keeps its vertex on the surface
  (`TerrainChunkMesh` `FlatTop`): the rim is a clean edge and the spikes are gone even at AO 1.5. Rims are a
  little scalloped by the 12.5 cm cells; the 070 texture feather at the edge is unchanged.

- Next feedback (same day): still "far too artificial" - a weird blend and thickness at every rim. Zoomed in
  (narrow-FOV camera at eye height), two causes: the cap is the pack's soft painted mud mapped over 20 m, a
  blurry smear beside the crisp clay-loam walls; and the cap was projected from above onto steep faces at the
  rim (the `smoothstep(-0.2, -0.05, n.y)` gate let it onto walls), so a step face read as a thick pale slab.
- Now the cap covers only near-flat faces (`smoothstep(0.35, 0.65, n.y)`; possible since rims are clean edges)
  and carries the clay loam's grain up close as a brightness ratio (`_CapGrain` 0.8 at a 2 m tile, fading out
  across the collar so it meets the terrain unchanged). The pack mud itself at 5x finer tile only added a few
  specks: every Highlands surface texture is soft painted blobs at any scale.
- Rejected while hunting the cause (none reproduced the report): bloom (adds nothing to the cap), texture
  streaming (off), depth of field (off), X-ray, tool level, crane dust. See unity/readme.md, "Reproducing a
  visual report".

- Next feedback: "you didn't fix anything": rough edges, blur, an unnatural mix and "a straight vertical cut when
  I wanted a more gradual mix". The FlatTop edge had made every rim a vertical cut, and its smoothed rim normals
  still projected the cap from above onto the top 10 cm of walls (vertical streaks). Asked how a mouth should look;
  the user wants switchable variations to choose from.
- `TerrainChunkMesh.Edge` (Developer admin **Hole edge**, session only, every session starts on the default):
  Sloped lip (default; the mesh widens a mouth by 0.3 m at the surface back to the cut 0.25 m down, the cap
  fading into the soil over that depth, `_RimMix`), Rounded edge (0.12 m quarter round), Soft colour (vertical
  cut, the cap's mean colour fading 0.25 m down the wall, `_RimBleed`), Vertical cut (FlatTop). The lip is
  shaped in the mesh from the density only (`ShapeMouths`: distance to air one cell down, so a tunnel under an
  intact roof never opens the surface); density, digging and saves are untouched, so switching reshapes every
  hole at once (`TerrainVolume.RebuildSurface`), and a cut rebuilds chunks that far further out.

- Verdict (2026-10-03): Rounded edge ("sloped lip was decent but I liked rounded edge the most"). Sloped lip,
  Soft colour, Vertical cut, the admin switch and the FlatTop vertex snap are removed: every hole's mouth is a
  0.12 m quarter round (`TerrainChunkMesh.MouthRound`) and the cap fades over the same depth (`_RimMix` on the
  ground materials, set by `ConfigureDigGround`).
