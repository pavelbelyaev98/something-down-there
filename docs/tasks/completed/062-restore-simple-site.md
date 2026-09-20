# 062 — Restore the Simple Playable Site

## Objective
Remove the rejected environment work and restore the pre-environment excavation and surface stations. The user will design the surroundings manually.

## Concept reference
Chapters 03 and 09 retain the long-term reservoir direction. Aboveground scenery is user-authored; this iteration restores the plain working prototype.

## Live code analysis
The pre-environment revision `c9c5d87` contains the existing menu, player, stations, excavation, purchased grass and save ownership. Subsequent scene changes are the rejected reservoir compositions. Their terrain assets, material overrides, preview camera and editor recipes are separate from gameplay systems.

## Changes
- Restore MainGame through Unity's scene APIs from the verified historical scene, retaining its asset GUID and existing gameplay references.
- Restore the prior ground shader, grass renderer/material, sunlight presentation and scene validation. Retain the approved purchased pack and its compatibility patches.
- Delete the project-owned reservoir/preview assets and their setup scripts through AssetDatabase. Remove the environment benchmark branch and obsolete asset rules.
- Keep native Terrain modules available for the user's manual authoring. Preserve unrelated project settings, the recovery scene and player saves.
- Update current architecture, baseline, authoring guidance and asset cards; retain task history with the rejected work marked superseded.

## Edge cases
- Do not run setup tools that create a second gameplay scene or discard existing saves.
- Restore the original permanent rim and perimeter together with the spawn, preventing unsupported walking edges.
- Retain shader/material GUIDs used by the excavation and keep underground daylight attenuation.
- Do not remove vendor demo assets or user recovery content when deleting project-owned previews.

## Acceptance criteria
- MainGame contains the original functioning player, menus, stations, digging volume and simple surface; no reservoir scenery or preview camera.
- Relevant existing scene, startup, station, terrain and grass checks pass; no compiler warnings/errors from changed code.
- Inspect the clean playable site and rebuild `builds/windows/SomethingDownThere.exe` successfully.
- Document that future surface layout is authored by the user.

## Verification
- MainGame and its ground, grass and sun assets match the pre-environment revision; its scene GUID is unchanged. Removed 45 project-owned environment assets/scripts, with the purchased pack intact.
- Existing checks: scene 1/1, startup 8/8, terrain 21/21, stations 7/7, grass 1/1 and underground daylight 2/2. Compiler status is clean.
- Inspected the simple site in Play Mode with save ownership disabled. Returned the Editor to clean MainGame in Edit mode.
- Windows build succeeded with zero errors. Its sole notice reports the intentionally absent Pipeline runtime configuration; Editor automation is not shipped in the player.
