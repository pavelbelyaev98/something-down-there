# 050 — Earlier Finds and Gentler Daylight

**Status:** complete. Packed more full-size rocks just beneath the turf using actual mesh envelopes and catalog-authored cover, and extended the early daylight reach without restoring an ambient floor.

## Objective and concept

Respond to the latest playtest: first scrapes should reveal more full-size rocks,
and daylight should last longer before shafts and covered branches become dark.
References: concept §05 (early finds), §03.7 and §09 (lighting).

## Live analysis

- Shallow placement hangs off a bounding-box diagonal plus a large soil margin;
  its corners are empty space for the rounded approved rocks. This exaggerates
  both burial depth and separation, limiting early encounters.
- The catalog already owns counts and depth bands, but shallow cover is hardcoded
  in `DiscoveryField`. Saved find positions restore explicitly and must stay intact.
- Task 049 removed artificial ambient fill correctly; the older daylight decay
  curve now reaches darkness too soon because it no longer has that bright floor.

## Changes

- Calculate a rotation-safe enclosing sphere from actual visual/collider vertices
  in `DiscoveryCatalog.Entry`, keeping all approved appearances inside the envelope.
- Add optional shallow soil-cover bounds to the existing source/catalog contract
  and pass them to `DiscoveryField.Generate`; retain its legacy fallback for old
  fixtures/catalogs. Increase the shallow rock allocation in the source catalog.
- Keep rock models, scale, exposure requirements, prices and deeper quotas intact.
- Extend the existing daylight transport budget and stretch its falloff so initial
  excavation remains readable. Keep sealed rooms black and local lamps effective.
- Keep saved populations unchanged; new population settings apply to new games.

## Edge cases and acceptance

- Deterministic generation succeeds across varied seeds, with no overlap or rocks
  poking through untouched turf. A shallow scrape intersects substantially more
  finds without reducing model size or bypassing deliberate exposure.
- Zero-count legacy items continue to resolve; restored positions/items remain intact.
- Light falls smoothly over a longer route; sufficiently deep/long routes still
  become near-black. Surface brightness and solid-wall occlusion stay intact.
- Relevant placement, discovery and daylight tests pass; inspect shallow excavation
  and tunnel captures, then rebuild the Windows player through the open Editor.

## Validation

- EditMode checks validated: 224, including a hundred-seed population sweep and
  shallow-reach cases; the five daylight cases passed after updating the old fade expectation.
- PlayMode: discovery 22/22 and daylight 2/2 passed. Generation remained within its
  time budget; existing saved identities/positions and exposure rules stayed valid.
- Actual scene captures under ignored `Logs/Task050/` show the dense shallow layer,
  readable middle/side passages and black far-depth ground. Review used an additive
  play session without a save profile; model scale and deeper quotas are unchanged.
- Fresh Windows player built successfully. C# compiles cleanly; the build retains
  the existing optional Pipeline runtime-configuration warning.
