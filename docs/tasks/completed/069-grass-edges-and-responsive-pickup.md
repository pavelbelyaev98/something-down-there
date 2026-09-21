# 069 — Grass edges and responsive pickup

## Objective
Keep meadow plants beside excavation, remove only foliage actually over the opening, keep turf continuous to the rim, and make aimed collection of loose/falling finds longer-reaching and immediate while digging is held.

## Concept references
`05_DISCOVERIES.md` (exposure and collection), `09_FEEL_ART_AND_AUDIO.md` (meadow and ground), `08_INTERFACE_AND_CONTROLS.md` (held primary action).

## Current implementation
- `SurfaceGrassRenderer` rejects whole clumps when a circle containing their mesh bounds, wind padding and an extra cell halo touches modified surface samples. This discards plants that do not visually touch the opening.
- `GroundTriplanar.shader` blends turf using slope-dependent projections and deliberately darkens its roots. The resulting polygonal band is visible around shallow cuts.
- `FpsPlayer.TryPrimaryAction` allows eligible pickup between shovel strokes, but pickup recovery blocks all primary actions; finds share short station interaction reach. `BuriedFind.TryCollect` repeats that short ray check.
- Finds must remain exposed, directly aimed, unobstructed, capacity-checked and collected exactly once. Handling and station interactions have separate purposes.

## Architecture changes
- Keep grass placement and support in `SurfaceGrassRenderer`. Check the root against actual support, and derive a small surface-density texture after terrain changes. Project-owned grass shader adaptation clips displaced foliage fragments above openings, avoiding padded whole-canopy deletion. The same clip applies to visible, depth and shadow passes; vendor source stays intact.
- Bind the clip shader through `SurfaceGrassSetup` to all project meadow materials. Keep the mask transient, rebuilt on reset/restore/re-enable, with spatial updates rather than per-frame full terrain scans.
- Remove artificial root darkening, narrow the turf fringe to the surface and use continuous top projection/lighting at the meadow cap, retaining soil comparison, dry shading and underground darkness. Keep `GroundTextureSetup` and both ground materials consistent.
- Give released finds a separate longer pickup reach, used consistently for the prompt and collection validation. Keep station and physical lift reach unchanged.
- Allow aimed eligible pickup during excavation/pickup recovery; retain the cooldown for subsequent soil strokes and the explicit drop/throw input barrier. Recheck the aim immediately after a successful cut so a newly exposed target does not wait for another stroke.

## Edge cases
- Adjacent intact grass, thin holes, leaves blown across a hole, round perimeter, deep tunnels beneath intact turf, checkpoint restore and multiple rendering cameras.
- Full bag, buried slivers, intervening soil/props, stationary/falling released finds, distant anchored finds, repeated collection calls, menu/focus/persistence barriers and held/toggle input.
- Odin checks asset configuration and shader references. Behavior and rendered appearance need targeted gameplay regressions and visual inspection.

## Acceptance criteria
- Grass next to a hole remains; foliage rendered over the opening is clipped without a broad bald margin.
- The grass cap has no authored dark outline or abrupt projection band; soil remains matte and deep tunnels remain dark.
- Loose finds can be collected from farther away with the same prompt and actual reach; blocked, buried and off-aim finds remain uncollectible.
- Held/toggled digging collects an eligible aimed falling find between strokes and during another pickup's recovery without extra fuel or digging.
- Relevant terrain/discovery/grass regressions pass, asset validation is clean, gameplay is visually checked and a fresh Windows player is built and smoke-tested.

## Verification
- 295 relevant tests verified: grass 3, discovery 28, physical handling 28, terrain 21 and EditMode 215. One editor test was rerun successfully after a concurrent CLI inspection timed out and injected an unrelated log error; no gameplay assertion failed in that final run.
- MainGame visual review: adjoining cuts retain neighbouring plants, clip foliage over the opening and keep continuous turf without the former dark band. Soil comparison and natural wall shading remain intact.
- Odin profile: 14,490 valid results, no errors or warnings. Temporarily assigning the original vendor grass shader to one flower material correctly produced the new clipping-configuration error; restoring it cleared the scan.
- Windows build succeeded with no compiler or shader warnings; its sole warning is the intentional absence of a runtime Pipeline bridge. The fresh executable launched and closed normally with no startup errors. Reports and captures are under ignored `Logs/Task069/`.
