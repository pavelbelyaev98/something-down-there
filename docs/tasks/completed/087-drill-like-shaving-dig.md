# 087 — Drill-Like Shaving Dig Action

Held digging uses controlled, thin voxel cuts; collection and full bags never interrupt the normal shaving or scoop cadence. Development admin retains a session-only shaving/scoop comparison for playtest evaluation.

## Objective

Make held digging continuously shave the contacted surface with frequent shallow cuts. Provide the user's requested session-only admin comparison button: shaving ON by default, OFF for the existing scoop action. This comparison is for playtesting; it is not a required gameplay mode or a second tool.

## Concept reference

- **04 §2–3:** hold and accessibility toggle operate one evolving machine; no mashing or manual material switching. Material resistance remains the next task, and the visible rig belongs to 001.
- **09 §4:** replace the chunky-bite feel with smooth, steady surface removal. Keep the camera stable, preserve readable silhouettes and upgrade benefits. Final subjective approval belongs to the user's comparison playtest.
- **14 §3–4:** test downward and lateral cuts, first-tool progress, increasing tier output, cleanup, collision agreement, battery use, recognition, saves and frame pacing.

## Live code analysis

- `FpsPlayer.TryPrimaryAction` owns cadence and pickup priority; `TryDig` resolves blockers, spends battery only after acceptance and forwards terrain strokes. Existing menus/focus/handling suppress held actions.
- `TerrainVolume.TryDig` validates collider hits, calls `ExcavationGrid.RemoveScoop`, synchronously rebuilds changed mesh/collider chunks and notifies finds, grass and daylight.
- `ExcavationGrid.RemoveBrush` owns density mutation, detached-component and thin-remnant cleanup, removed-volume accounting and revisions. Extend this shared path rather than duplicate terrain or cleanup.
- `EquipmentProgression` owns new shaving tuning; `ShovelProfile` remains the current authored width/cadence/reach ladder. No scene or save-format changes are needed.
- `GameMenuView.BuildAdmin` already supplies session toggles and tool tuning in the UI Toolkit document. Reuse its button and refresh behavior.

## Implementation

1. Add a shallow circular shave brush to the shared grid mutation path. Orient it to the contacted surface, retain the current footprint scale, and round its edge. Unlike scoops it has no per-stroke random tilt/depth, so held contact produces steady recession.
2. Extend the terrain adapter with `TryShave`; share stale-hit validation, mesh/collider rebuild and notifications with scoop. Refine the mesh contact onto the density isosurface along its normal so sub-cell shaves do not stall on surface-net approximation error.
3. Default player terrain digging to shaving. Scale interval and accepted-cut energy together; carry the small timer remainder across held frames and bound work to one cut per frame, without stored idle/pause catch-up.
4. Keep aim and post-cut pickup, reach, blockers, battery refusal, menus, focus loss, rescue and holding/throwing rules intact. Collection never delays the normal terrain cadence. No charge for an invalid cut.
5. Add `Shaving motion: ON/OFF` near the top of admin. Switching clears dig timing/pulse and suppresses held input; closing the menu requires a fresh dig press. Normal rules and session restore return to shaving. The override never enters a save or release gameplay controls.
6. Update affected concept/baseline/developer guidance, and archive this spec only after checks and a fresh Windows development build succeed.

## Edge cases

- Sub-cell progress, oblique surfaces and chunk seams; rapidly changing aim must not bridge through intervening blockers.
- Permanent boundaries, air, stale hits and insufficient battery must not spend charge or mutate terrain.
- Detached soil and thin remnants clear in the same accepted cut; useful ledges remain governed by existing rules.
- Both modes use the same owned tier, reach, inventory, save data and world. Compare on fresh nearby soil or admin Reset ground.
- High frame times cannot accumulate an unbounded burst; pause/focus loss and comparison changes cannot restart a latched dig.

## Acceptance criteria

- Default hold/toggle digging visibly recedes in shallow repeated cuts; the starting tier can keep cutting at a fixed aim and higher tiers clear faster.
- Admin can switch both ways repeatedly, with an accurate label and no reset of the hole or owned upgrades. Restore normal rules selects shaving.
- Shaving and scoops both produce matching render/collision surfaces and invoke existing cleanup, find and presentation notifications.
- Relevant core grid, player, terrain, pickup and save tests pass; compilation is warning-free.
- Inspect a live MainGame shaving run and the admin control, record useful timing evidence under ignored Logs, then build `builds/windows/SomethingDownThere.exe` through the normal build pipeline.

## Validation

- The selected EditMode and PlayMode checks pass, including sub-cell cuts at multiple surface orientations, current-format restore, cadence across frame rates, collision, discoveries, held/remapped-toggle collection, fuel reserve, rescue, menus and station trades. Fuel assertions use the active cut cost; scene fixtures establish focus after activation. Targeted reruns resolved native-focus timing and one monitoring-request timeout without changing gameplay barriers.
- A disposable MainGame sweep and rendered admin review confirm surface removal and clickable comparison in both directions without changing the hole. The short Editor sweep measured mean 6.80 ms, p95 7.51 ms and maximum 14.60 ms per accepted cut; long-session performance remains the later performance task.
- Fresh-ground checks confirm every current shovel tier increases shaving output per powered second. Captures and measurement details stay in ignored `unity/Logs/task087-*` files.
- The Windows development build succeeds with no compiler errors or warnings. The Pipeline package reports its existing missing runtime configuration and remains disabled in the player; Editor automation is unaffected.
- Player preference on shaving versus scoops remains for the user's comparison playtest; both are available in development admin. No rig, material resistance, audio, save compatibility or external assets were added.

## Playtest iteration — controlled shaving and uninterrupted collection

- The user found shaving much too fast and rejected any pause during object collection. Reduce the shaved layer depth in `EquipmentProgression`, retaining the smooth contact frequency, tool width/reach and current energy rate.
- Remove the pickup recovery timer and the nearby-pickup early return. Held aimed collection rechecks the ray and uses a due cut immediately; collection during an existing cooldown preserves its deadline. A revealing cut still runs only once, and pickup adds no fuel cost.
- Retain the admin comparison so both actions can be checked. Core regressions cover aimed/nearby pickups sharing a ready cut and pickups preserving an in-progress cut deadline in both modes.
- Validation passes for shallow-cut progress, collision/restore, cadence, both pickup paths, held/remapped-toggle input, exposure, full bags and blocked collection. MainGame review confirms slower cutting with increasing output across tool tiers; the fresh Windows development build succeeds without compiler warnings or errors, retaining only the existing Pipeline runtime-configuration warning.

## Full-bag digging playtest iteration
- A full bag keeps common finds in the world while the cutting ray reaches ordinary ground behind them. Preserve tool reach, cadence, fuel and permanent obstructions; capacity becoming available restores immediate collection. Bounded nonallocating ray queries affect digging only, leaving item physics and identity intact.
- Verification: both cutting modes and held/remapped-toggle input continue through full-bag commons, consume fuel per cut, respect cooldown and walls, and collect once space returns. Terrain/shaving checks and the fresh Windows build pass.
