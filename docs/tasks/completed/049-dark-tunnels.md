# 049 — Darkness in Deep and Lateral Tunnels

**Status:** complete. Removed the ambient brightness floor and applied the connected-air daylight field to direct sun, sky fill and reflections, so sustained descents and long covered branches become near-black while local lamps stay effective.

## Objective and concept

Restore the lighting intent in concept §03.7 and §09: daylight fades through the
excavated route, and deep or long covered branches become near-black. Preserve the
bright surface and entrance. This is the requested playtest fix ahead of task 006.

## Live code analysis

- `ExcavationDaylightGrid` already transports sky light only through connected air,
  attenuates lateral travel faster, rejects solid partitions and rebuilds after digs/restores.
- `ExcavationDaylight` and its shared HLSL remap this field to a minimum 45% ambient
  brightness. This prevents darkness even when the transport field reaches zero.
- Both `GroundTriplanar` and `Excavation Lit` apply the field only to indirect
  occlusion. The main directional light can still illuminate underground surfaces
  when real-time shadows miss a blocker or fall outside their coverage.
- The existing component adapts URP Lit finds/boundaries; no new lighting owner,
  scene changes, saved state, external assets or personal lamp are needed.

## Changes

1. Remove the ambient minimum from CPU sampling and the shared shader sample.
2. Use a shared URP lighting include to attenuate the main sun by the same field
   before the existing PBR calculation. Keep additional point/spot lights and
   material emission independent, so actual lamps can still illuminate dark ground.
3. Keep the existing continuous falloff, dirty-volume invalidation and frame budget.
4. Extend existing daylight tests for long/deep routes and rendered darkness;
   update the concept and baseline with the corrected behavior.

## Edge cases

- Surface samples remain fully lit; opening or closing a roof updates the field.
- Sealed chambers and lateral site boundaries admit no sky light.
- Unshadowed sunlight must not re-light deep surfaces; lit finds follow terrain.
- Disabling the component restores original materials; restoring old excavations
  derives current lighting without modifying save formats or player data.

## Acceptance criteria

- Surface/entrance retain daylight with smooth darkening along open shafts.
- Long lateral passages and deep shafts reach negligible daylight; sealed rooms
  have no ambient floor, including reflective finds.
- Rendered terrain/finds stay dark with an unshadowed sun and brighten under a
  local point light. Inspect saved render captures as well as numerical assertions.
- Relevant EditMode and PlayMode checks pass; code/shaders compile without warnings.
- Fresh Windows player is built through the open Editor to the standard build path.

## Validation

- EditMode: 221/221 passed; daylight PlayMode: 2/2 passed, including GPU checks for
  soil, metallic finds, unshadowed sunlight and a local lamp.
- Actual `MainGame` terrain reviewed in a disposable additive play session:
  bright surface/entrance, shaded middle shaft, black deep shaft and near-black
  lateral passage. Captures are under ignored `Logs/Task049/`; no player save used.
- C# and both shaders compile cleanly. Windows build succeeded; its sole warning
  is the existing optional Pipeline runtime configuration notice (Editor tooling
  is available; the player does not include a Pipeline server).
