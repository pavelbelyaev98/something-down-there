# 056 — Pure Nature integration and surface grass

## Objective
Use the purchased Pure Nature 2: Mountains pack as an approved environment art source;
replace the prototype grass with its vegetation while preserving excavation behavior.

## Concept reference
- `09_FEEL_ART_AND_AUDIO.md`: cohesive stylized surface, readable ground, comfortable wind.
- `03_WORLD_AND_SITE.md`: voxel excavation remains authoritative; vegetation follows soil.
- User approved the purchased pack and requested setup, replacement grass and asset workflow.

## Live code analysis
- MainGame already uses URP with a project-owned pipeline and renderer; the pack is imported under `Assets/BK`.
- Publisher shaders in the import report obsolete URP keyword/G-buffer warnings; an official shader update is available.
- `SurfaceGrassRenderer` already batches deterministic roots, reacts to local soil changes,
  and regenerates from restored terrain. Its small prototype footprint and uniform-scaling
  shader assumption need updating for the pack's wider alpha-tested grass cards.
- `SurfaceGrassSetup` and scene/play-mode checks currently reference the old Blender export.
- BK's existing environment manager supplies wind/fade globals but defaults to overriding lighting.
- Runtime/editor assembly definitions cannot directly reference BK's Assembly-CSharp types.
- The imported pack is approximately 1.65 GB; existing LFS rules cover source art only.

## Architecture and changes
- Keep vendor paths and GUIDs. Apply only the eight matching publisher shader updates,
  retaining imported metadata; retain the downloaded archive and verification evidence in ignored Logs.
- Update the existing surface renderer with configurable clump scale/wind bounds,
  mesh-sized soil support sampling and conservative instancing limits for vendor shaders.
- Reuse the low-poly plain grass mesh from the Mountains pack throughout the site; no artificial distance LOD
  is needed for this small mesh. Use a project-owned material copy referencing BK textures/shader.
- Refactor `SurfaceGrassSetup` to bind the purchased mesh/material, configure the current URP
  assets, and add/configure BK's manager via editor type discovery. Disable its lighting/cloud
  overrides so the existing sun, tunnel darkness and post-processing retain ownership.
- Remove retired Unity grass assets after rebinding; preserve editable historical art under `art/ground-grass`.
- Update existing integration checks for the new art and retain core cut/restore/culling checks.
- Record a local asset card, update art direction and developer guidance; scope Git LFS rules
  to vendor binaries while keeping metadata, scenes, prefabs, materials and code as ordinary text.
- Keep the full imported pack for reproducible setup/future use; do not stage, commit or push.
- `WindowsBuild` clears output contents while retaining the root directory, allowing a terminal
  or Explorer to hold that directory open without blocking a clean rebuild.

## Edge cases
- Grass disappears over removed soil, remains cleared after load, and returns after terrain reset.
- Broad cards must not visibly bridge excavated lips; animated tips stay inside culling bounds.
- Vendor updates must not overwrite our material tuning, pipeline settings or scene ownership.
- Shader globals must initialize in a player without a demo scene or Unity Terrain component.
- No recovery-scene or existing player-save changes; gameplay verification uses temporary state.
- Purchased source assets belong in restricted project storage; unreferenced demo scenes are not added to the player build.

## Acceptance criteria
- MainGame uses BK grass mesh/textures/shader; old prototype grass is absent from the Unity project.
- Publisher URP requirements are configured; project and used shaders compile without warnings/errors.
- Existing grass cut/restore/reset/culling checks and affected scene/rendering checks pass.
- Play Mode captures confirm surface coverage, wind and clean excavation edges with no pink materials.
- Windows player is rebuilt at `builds/windows/SomethingDownThere.exe`, with build result checked.
- Asset usage/update/Git workflow and baseline are accurate; spec archived and task marked complete.

## Validation
- Edit Mode: 230/230; selected Play Mode: 74/74. Final scene and grass integration checks rerun after plain-grass/proportion tuning, both passed.
- Publisher shaders report no diagnostics; C# recompilation completed without errors or warnings.
- Temporary additive MainGame review had no save owner. Surface and excavated-edge captures show plain grass, wind and cleared lips; restored terrain behavior passed the existing integration test.
- Fresh Windows build succeeded with no errors; native player startup and grass rendering verified. One package notice states that the player-side Unity CLI server is disabled; this is intentional, and enabling it solely to suppress the notice is inappropriate.
- BK binaries match scoped LFS rules, metadata remains ordinary Git, and existing LFS filters/pre-push hook are installed. Nothing staged, committed or pushed.
