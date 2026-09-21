# 065 — Matte excavation ground

## Objective and concept
Remove the crystal-like white glare from the reservoir surface and fresh cuts.
Ground must read as dry, rough earth with visible colour and normal-map relief.
Reference: `concept/09_FEEL_ART_AND_AUDIO.md`, visual style and material behaviour.

## Live code analysis
- `GroundTriplanar.shader` expects original Blender masks: roughness/contact/stone coverage in RGB.
- `ReservoirEnvironmentSetup.ConfigureSediment` assigns BK mud/gravel masks to that shader.
- BK masks store metallic/occlusion/smoothness in R/G/A. Reading their metallic channel as
  roughness makes non-metallic dirt mirror-smooth; B is not authored stone coverage either.
- The same shader serves `GardenGround` and `ReservoirSediment`, including rebuilt voxel chunks.
- Lighting and excavation darkness belong to the existing URP/excavation lighting path.

## Changes
- Decode both mask layouts explicitly in the existing shader, before projection blending.
- Keep original RGB decoding as the default; opt the sediment material into packed R/G/A.
- Treat the packed mask as non-metallic ground and omit unsupported stone coverage.
- Limit ground smoothness to a dry response, retaining textures, normal relief and contact shading.
- Persist the material choice through Unity and in both existing setup tools.
- Update the living art direction and baseline; retain vendor files and scene ownership.

## Edge cases
- Decode both top crust and excavated soil, including the transition and vertical slopes.
- Original camp textures must retain their RGB contact/stone channels.
- Material setup/rebuild must reproduce the fix, and fresh chunks must use it automatically.
- Keep ambient shading, cast shadows, underground daylight loss and local lamps functional.
- Never touch player saves or the recovery scene; use a disposable, unsaved play session.

## Acceptance
- No mirror-like white patches or crystal sparkles on the flat bed or a fresh hole in Play Mode.
- Ground relief and colour remain visible from overhead and oblique views.
- Ground shader compiles without errors/warnings; relevant existing scene/lighting checks pass.
- Fresh `builds/windows/SomethingDownThere.exe` produced by the normal build menu.
- Complete this spec, update baseline/concept, and record the iteration in `tasks.md`.

## Verification
- 231/231 EditMode checks and 2/2 excavation daylight PlayMode checks passed.
- Play Mode surface and three fresh scoops visually verified: matte ground and cut walls,
  retained relief/shadows, no white glare; evidence under `Logs/Task065/`.
- C# compilation and ground shader have no warnings/errors. Windows build succeeded;
  build-only warnings concern vendor boulder collision pre-baking and disabled player Pipeline.
- MainGame restored in the Editor; review used additive loading without a save session.
