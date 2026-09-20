# 059 — Direct demo lake preview

> Superseded by task 062: rejected scenery, project preview assets and assembly tools removed; the user now authors the environment manually.

**Rejected after review:** the complete demo was copied instead of assembling only the
selected reservoir sections, and its fly-camera build replaced the functional game.
Task 060 restores the playable entry. This scene is reference material, not accepted art.

## Objective and source
Use the user's selected Mountain_Demo lake as a static, directly copied reservoir surface.
Preserve its height field, bed textures, shore grass/flowers, rock placements and distant
mountains. Remove water in the copy. Add a coherent copied river cliff/tree section at a
back boundary, preserving the source group's internal arrangement. No invented terrain,
scattered replacement scenery, stations or excavation work in this visual review pass.
References: concept 03 and 09; user-selected lake, river-bank and flower-bank screenshots.

## Existing ownership and implementation
- MainGame and its excavation/save systems remain intact while the surface is reviewed.
- Extend ReservoirEnvironmentSetup with an explicit demo-copy operation that creates
  ReservoirPreview and independent project-owned TerrainData. Retain vendor prefab links.
- Copy the entire source terrain so lake relief, painted foliage and sightlines remain
  identical; copy source lighting into this separate preview only.
- Assemble the river-bank selection under one editable parent using a common rigid
  transform. Keep its source positions recorded in editor code for verification.
- Add a small Input System inspection camera, replacing the demo's legacy-input camera
  in the preview. No world saves, generated finds, gameplay HUD or stations are loaded.
- Extend the existing WindowsBuild entry point with an explicit preview build menu that
  uses the same clean output-folder handling and produces the current playtest executable.

## Edge cases and acceptance
- [x] Original vendor scene, TerrainData, assets and recovery scene remain unchanged.
- [x] Lake terrain, layers, details and existing placements match the source exactly.
- [x] No water is active; the original lake bed is visible without height/paint changes.
- [x] River boundary preserves its internal rock/tree placement and sits outside the bed.
- [x] Shore flowers and mountain sightlines remain visible in lake and flower-bank captures.
- [x] Preview navigation works, compilation is clean, and a fresh Windows preview is built.
- [x] Document the preview's temporary scope and manual editing/build workflow.

## Validation and limits
Full source comparison found zero differences in height samples, texture weights or any of
the 23 detail maps. Retained tree instances and all protected shore trees match the source.
The three cliff prefabs and 21 supported river trees retain exact relative placements and
connected prefab links. Only 297 original forest instances buried inside the added wall were
removed from the copied TerrainData; the original terrain asset remains untouched.
Play Mode verified terrain rendering, no water/save/gameplay owners, forward camera movement
and exact reset. Lake and flower-bank captures inspected; evidence is in ignored `Logs/Task059/`.
C# and shaders compiled cleanly. Windows preview built successfully; build warnings concern
automatic collision baking for four vendor meshes and intentionally absent player Pipeline
configuration. The entire demo terrain/backdrop is retained for fidelity; no FPS claim is made.

This is an environment composition for user review. Making the lake bed excavatable and
restoring stations require a later explicitly guided integration pass.
