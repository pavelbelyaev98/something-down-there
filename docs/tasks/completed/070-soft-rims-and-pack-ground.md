# 070 — Soft rims and pack ground

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
