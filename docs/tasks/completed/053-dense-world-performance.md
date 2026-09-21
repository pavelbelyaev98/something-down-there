# 053 — Dense World Performance

**Status:** complete. Conservative soil occlusion suppresses buried find rendering and anchored physics sleeps until terrain edits or explicit handling; startup is frame-capped before preferences load.

## Objective
Restore responsive excavation with the accepted find distribution intact, and ensure startup has a frame limit before preferences load.

## Concept reference
Discovery density in concept 05; smooth movement and comfort in concept 10. Hidden contents must not consume the same rendering/physics budget as discoveries in the hole.

## Live code analysis
- `DiscoveryField` instantiates the catalog and scans every renderer bound per cut.
- `BuriedFind` keeps fully buried renderers and colliders enabled; every `FindPhysics` receives fixed updates even while anchored.
- `GamePreferences` already defaults to a 144 FPS limit, but applies only when the player awakens. Graphics defaults supersample the scene with high MSAA.
- Placement, save identities and physical release are working contracts to preserve.

## Implementation
- Measure steady rendering and physics cost in a disposable scene, comparing identical views with hidden finds suppressed.
- `TerrainVolume.MayExpose` checks complete world bounds against the pristine surface and edited sample halo. `BuriedFind.RefreshExposure` suppresses rendering only when soil conservatively encloses the mesh, preserving slivers between exposure samples. Original colliders remain enabled for targeting and contact.
- Suspend anchored physics callbacks until terrain changes. Preserve dynamic bodies, collisions, holding, falling and soil-reset behaviour.
- Retain the existing per-cut query: profiling identified hidden mesh submission as the dominant regression, not the event-driven scan.
- Apply the default frame cap before scene load, then respect explicit saved settings. Confirm the actual runtime cap and graphics cost.
- Extend the disposable `SurfacePerformanceFixture` with an opt-in discovery A/B benchmark, real cuts and startup/capped pacing checks. Keep the same device graphics settings and population in both measurements.

## Edge cases
Terrain restore/reset, moved and collected saved finds, rotated terrain, partial exposure, disabled components and held or settling bodies. Existing saves and recovery scenes remain untouched.

## Acceptance criteria
- Accepted placement and full-size populations remain unchanged.
- Buried objects stop incurring avoidable per-frame rendering/physics work; repeatable before/after measurements show the effect.
- Digging activates visible finds promptly, collection and falling work, and checkpoints retain exact records.
- New startup is capped at 144 FPS; VSync and explicit frame-limit preferences still work.
- Relevant tests pass, scripts compile cleanly, gameplay is checked and a fresh Windows player is built.

## Verification
- Native Windows renderer A/B at 2560×1440, existing 150% render scale / 1× MSAA, RX 9060 XT: surface median 21.8 → 5.0 ms; excavated pit 28.3 → 7.4 ms. Repeated alternating states retained identical terrain, graphics and population. GPU samples confirm rendering occurred.
- Startup and loaded preferences both select 144 FPS. Capped frame median 6.945 ms, p95 6.947 ms. Sixteen real cuts averaged 7.4 ms total, with a 12.4 ms maximum.
- Native captures reviewed with exposed and fallen finds intact. Evidence: ignored `Logs/Task053/native-rendered.json`, `surface.png`, `pit.png`. Empty renderer-list and non-rendering window samples were discarded; benchmark guards now reject those states.
- EditMode 228/228; discovery checks 25/25 verified, including conservative surface bounds, slivers, excavation wakeup and full-population restore. Physics/handling suite passed 29/30 initially; the aiming setup failure did not reproduce in a focused rerun (4/4 cases passed). The extended drop/settling stress test passed.
- Windows player rebuilt successfully; no compilation errors or C# warnings. The existing Pipeline runtime-config notice remains (editor automation is disabled in players).
