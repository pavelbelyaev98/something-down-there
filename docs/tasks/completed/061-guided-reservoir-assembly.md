# 061 — Selected demo sections in the playable reservoir

> Superseded by task 062: rejected scenery, project preview assets and assembly tools removed; the user now authors the environment manually.

## Objective and references
Replace MainGame's rejected mathematical ground with actual selected Mountain_Demo data.
Use the lake bed and its flowered shore, join a copied river cliff/tree assembly as the
back boundary, and keep selected mountain silhouettes. References: the user's lake,
flower bank and river screenshots; concept 03 and 09. Keep menus, walking and excavation.

## Live analysis
MainGame owns the working FpsPlayer, FpsHud, WorldSaveController, TerrainVolume and stations.
The rejected ReservoirGround recipe generates relief/paint and must leave the scene.
The original demo has a single large TerrainData containing authored heights, texture
weights, detail layers and trees. Copying it whole was rejected. ReservoirPreview is
reference only; its camera/build must never replace gameplay again.

## Architecture and exact changes
- Extend the existing editor setup with a one-time MainGame assembly operation. Crop an
  aligned height/paint/detail rectangle around the selected lake from original TerrainData
  into a new project asset, preserving source sample spacing, terrain layers and prototypes.
- Copy only original placed scenery inside that rectangle, plus selected distant mountain
  prefabs. Keep prefab links and relative transforms. No randomized scatter or water.
- Copy the selected river cliffs and their supported source trees as one rigid assembly;
  remove only crop trees physically buried by the transplanted bank.
- Replace MainGame's surrounding Terrain with the crop. Keep the existing voxel grid,
  save coordinates and system references. Make the smallest local terrain join required
  around that excavation opening and service anchors; leave the rest of the crop intact.
- Match the excavation cap to the source bed textures through the existing ground material;
  remove generated grass from the dry patch. Preserve underground lighting ownership.
- Keep one game camera/player and the existing menu/save flow. Adapt surface light/sky
  presentation through project-owned settings only if required for source fidelity.
- Keep all assembly in the Editor; aboveground content is static and editable by hand.

## Edge cases
Preserve vendor GUIDs, recovery scene, user saves/preferences and unrelated changes.
TerrainCollider must share the cropped data; the Terrain hole must not cover voxel cuts.
Keep tree/rock roots and seams supported, avoid copied cameras/water/duplicate lighting.
Do not change digging dimensions or claim the entire lake is already excavatable.
Refuse to overwrite an existing assembled scene or asset during routine tool use.

## Acceptance and validation
- [x] The playable MainGame visibly contains the exact selected demo lake/shore and river-bank
  placements, with the old generated surroundings removed and unrelated demo areas excluded.
- [x] Compare height/paint/detail samples to source outside the explicit local excavation join.
- [x] Verify prefab links, copied transforms, no water, one player/HUD/save owner and walkable
  terrain collision. Inspect ground-level screenshots from the actual game camera.
- [x] Run relevant ownership, menu/player and excavation checks using isolated profiles.
- [x] C# compiles cleanly and a fresh Windows MainGame executable is ready for playtest.

## Results and limits
Source comparison found no differences in painted weights, detail maps, copied object
positions/rotations or heights outside the local join. Connected prefab links remain.
The cropped terrain retains authored shore planting; only trees buried by the transferred
river bank are removed. MainGame contains one player, HUD and save owner, with no water.
All 32 relevant scene, startup, excavation and daylight checks passed. The lateral-tunnel
fixture now sets its heading explicitly instead of assuming the authored spawn direction.
Save-free Play Mode verified grounded walking on the copied shore and an accepted voxel cut.
The Windows build succeeded with zero errors. C# and shader compilation are clean; build
notices concern automatic baking for three vendor collider meshes and disabled player
Pipeline automation. Vendor files and recovery data remain unchanged. Existing digging
dimensions and save coordinates are preserved; full-lake excavation is not implemented.
Pack LODs and detail culling remain in use; no minimum-hardware FPS claim is made.
