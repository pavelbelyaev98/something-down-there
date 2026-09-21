# 052 — Fresh Find Density

**Status:** complete. Concentrated full-size rocks and coal through the first few metres with stratified depth targets, measured as fresh encounters at the dig face — fallen rocks from earlier layers never count.

## Objective
Keep the accepted turf layer and make fresh finds on deeper excavation faces nearly as
plentiful through the first few metres, with coal entering beneath the rocks.
Fallen objects from earlier digging must never count as evidence of new buried encounters.

## Concept Reference
`05_DISCOVERIES.md`: steady encounters, gradual mineral introductions, deliberate reveal.

## Live Analysis
- Previous checks counted whole depth bands; they did not measure fresh objects crossing
  an excavation face. Physics also carried shallow finds into the review pit.
- Best-candidate placement changes depth on every attempt, biasing early mineral allocations
  away from the shallow carpet. Authored minimum depth does not guarantee early encounters.
- Concentrating the increase in the first few metres keeps the existing runtime population
  model practical; generation/startup/dig costs must be checked with the increased count.
- Existing snapshots already contain complete identity, pose and collection state.

## Changes
- Preserve shallow placement; stratify target depths before ranking nearby lateral candidates and
  tune the existing source catalogs for dense, overlapping progression bands.
- Keep the original turf clearance; pack buried envelopes with a smaller positive soil gap.
- Concentrate extra full-size rocks and coal in the early progression bands; preserve the
  existing lower-reservoir allocation and normal exposure, pickup and falling behavior.
- Raise the shared bounded save population limit with round-trip validation.
- Replace misleading density assertions with original-pose hull intersections and local patch
  checks at successive dig faces. Verify live excavation after removing earlier loose finds.

## Edge Cases
- No reroll, repopulation or save overwrite for existing players; retired identities still resolve.
- Full terrain restore activates exposed saved finds; moved/released bodies restore physically.
- Shallow rocks, authored object size, pickup thresholds and recent lighting stay intact.

## Acceptance Criteria
- Fresh deeper faces approach the accepted shallow encounter density across independent seeds
  and representative patches; coal appears naturally at the initial transition.
- Captures exclude fallen shallow objects; collection and physics still work on newly revealed finds.
- Placement, startup, digging and checkpoint sizes remain bounded; the whole population survives reload.
- Relevant tests pass, code compiles cleanly, and a fresh Windows build is produced.

## Verification
- EditMode: 228 checks verified, including the final focused rerun of patch-density assertions.
  Placement/clearance passed across seeds; full generation measured 352 ms.
- PlayMode: discovery 23/23 and mineral collection/sale/full-population checkpoint reload 1/1.
- Disposable game captures show comparable fresh floor exposure after earlier and fully passed
  objects are removed, with coal increasingly visible in the early descent. User saves were untouched.
- Eight representative cuts averaged 3.47 ms total and 2.35 ms for find refresh in the Editor.
- Windows player rebuilt successfully with no compiler warnings/errors; the existing optional
  Pipeline runtime-config warning remains.
