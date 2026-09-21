# 051 — Deeper Finds and Noon Light

**Status:** complete. Increased ordinary depth-band populations and added a catalog-owned lower-reservoir allocation while preserving the accepted shallow layout; near-overhead sunlight and a global URP saturation profile brighten the surface.

## Objective
Keep the accepted shallow rock layer, make subsequent excavation meet more objects,
and present the surface with vibrant colors and a bright, nearly overhead midday sun.

## Concept Reference
`05_DISCOVERIES.md`: steady encounters with a more valuable mix at depth.
`09_FEEL_ART_AND_AUDIO.md`: readable, colorful daylight with genuine deep darkness.

## Live Code Analysis
- `DiscoveryCatalog` already separates shallow cover from core/scatter depth bands.
  Those bands still end at the previous terrain floor, leaving the extended reservoir empty.
- `DiscoveryField` uses deterministic spatial buckets, bounded searches and fixed save limits.
- `SunPresentationSetup` owns the approved sky, but inherits the older angled scene sun.
  The world camera has no post-processing component; the active URP renderer supports it.
- Saved populations restore their exact records; catalog changes apply to new games.

## Changes
- Increase banded populations in the existing source catalogs; retain shallow allocation,
  cover, model sizes, prices and identities.
- Add an optional deep allocation and band to existing catalog entries/importers; remove it
  from the ordinary core/scatter quota so early progression retains its shape.
- Configure the sun and matching sky disc together in `SunPresentationSetup`.
  Add one reusable global URP color profile and enable it on the world camera.
- Extend placement checks to the current site, verify deep coverage, exact quotas,
  overlap clearance, deterministic generation and invalid allocation rejection.

## Edge Cases
- Existing saves must retain finds, collected state, historical values and positions.
- Absent deep fields preserve the existing catalog behavior; impossible allocations fail validation.
- Shallow rocks remain reachable by the same first scrapes and never protrude through turf.
- Increased population must fit the existing save bound and generation time budget.
- Color grading must preserve black; local lamps and daylight occlusion remain effective.
- Re-running editor setup must reuse the profile/volume rather than duplicating them.

## Acceptance Criteria
- Population tests demonstrate richer ordinary bands and continuous lower-reservoir coverage.
- Existing shallow encounter, save compatibility and discovery gameplay checks pass.
- Noon lighting and richer colors are verified on surface and excavated ground;
  deep tunnel captures remain near-black.
- C# compiles cleanly; fresh Windows player is built and relevant concept/baseline docs updated.

## Verification
- EditMode: 227/227 passed, including exact shallow-layout preservation and lower-reservoir coverage.
  Full population generation measured 145 ms; the density/clearance sweep passed across seeds.
- PlayMode: discovery 22/22, mineral collection/sale/checkpoint reload 1/1, daylight 2/2 passed.
- Disposable game captures verified vivid noon surface light, readable early shafts and black
  far-depth ground. Re-running sun setup retains one volume and profile; user saves were untouched.
- Windows player rebuilt successfully; no compiler errors/warnings. The pre-existing optional
  Pipeline runtime-config warning remains; it does not affect the game.
