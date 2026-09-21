# 060 — Restore the playable game entry

**Status:** complete. Removed the preview override and restored MainGame's menu, walking, excavation and saves; preview assets were subsequently removed in 062.

## Objective and concept
Restore the normal Windows game after the environment preview replaced its executable.
Follow concept 03 and 09: construct one reservoir from selected lake/shore and river-bank
sections; the complete vendor demo is not the requested level. Surface work must retain
menus, first-person movement, excavation and persistence.

## Live code analysis
MainGame still owns FpsPlayer, FpsHud, WorldSaveController and TerrainVolume. Its last
surface prototype remains rejected. ReservoirPreview contains the full demo TerrainData,
scenery and a fly camera, without gameplay. WindowsBuild's preview method currently
writes to the same executable as MainGame, causing the reported regression.

## Changes
- Remove the preview build entry and scene override from WindowsBuild; the Windows
  artifact always starts MainGame, regardless of the Editor's active scene.
- Restore MainGame as the active Editor scene after checking for unsaved work.
- Retain the rejected preview as a reference only. Do not merge its whole terrain or
  redesign the surface during this repair.
- Correct concept, baseline, architecture and asset guidance to distinguish the desired
  selected-part reservoir from both rejected prototypes.

## Edge cases
- Preserve user saves, preferences, recovery scene and unrelated working-tree changes.
- Tests use their existing isolated temporary profiles; never initialize test gameplay
  against the user's profile.
- Do not discard dirty Editor scenes or overwrite a running player executable.

## Acceptance
- [x] Startup menu, first-person movement, pause and digging checks pass.
- [x] C# compiles without new warnings or errors.
- [x] Fresh Windows build targets MainGame; no preview menu can replace it.
- [x] Surface limitations are explicit; this repair does not claim reservoir art completion.

## Validation
All 49 existing Play Mode checks passed: eight startup/save/menu checks, twenty player
checks and twenty-one MainGame excavation checks. Tests used isolated profiles. Windows
build succeeded with zero errors and only the existing notice that player Pipeline
automation is disabled. The Editor is in MainGame, outside Play Mode. Vendor and recovery
files remain unchanged. No scenery or gameplay-system changes were made in this repair.
