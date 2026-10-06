# 095 — Host Ground

**Status:** complete. Source catalogs can give each type host grounds with density weights; placement keeps every depth band and picks a find's lateral spot from a host group chosen in proportion to weight × candidates. Since 107 the only hosts are soil and backfill, so the bias waits for the zone grounds.

## Objective

Find placement prefers each type's host ground through per-type data in the source catalogs, so
players learn "that rock mass (or gravel) is worth a look" as knowledge, never as a radar. Soft
bias with scatter; prices never change; each depth band keeps its find rate. Later rosters (`012`,
`011`) declare their host ground the same way.

## Concept reference

- `03` §4 host ground: each ground holds its own kind of find (soil → rubbish, junk, plain rocks;
  gravel → heavy things: coins, tokens, metal, nuggets; clay → bones, wood, leather, fossils,
  organic things; rock → ore; concrete → waterworks and village items, rooms behind it; backfill →
  whatever someone buried). A soft bias with scatter: the odd coin in clay still happens. Prices
  stay fixed per type; host ground changes where things are, never what they are worth. The player
  builds a mental map; that is knowledge, not a value radar.
- `05` §1 where finds sit: heavy coins, tokens and metal in gravel, bones and organic things in clay,
  ore in rock veins, waterworks and village items in and around concrete, rubbish in soil.
- `05` §1 constant rate, rising value: depth changes what you meet, never whether digging pays.
- `03` §4 tells: presence, never value.

## Live codebase analysis

- `DiscoveryCatalog.Generate(extent, seed)` shuffles every non-authored instance, assigns each a
  depth band (core or scatter) and calls `DiscoveryField.Generate`, which picks a stratified target
  depth per band and then keeps the best-spread of up to 64 lateral candidates inside the plot
  footprint (a small ±0.15 m depth window). Placement knows nothing about the ground.
- `DiscoveryField.InitializePopulation` runs after the terrain session exists (`TerrainVolume` owns
  the seeded `ExcavationGrid` and its material field); tests call `catalog.Generate` directly.
- Current roster: plain rocks (soil) and the mineral ladder (ore). Coins, beads, bones and
  waterworks items arrive with `012`/`011`.
- Ground from `006`: soil + gravel lenses (zone 1), clay with pond-clay basins, rock masses and
  concrete structures (zone 2), rock veined with clay plus rock masses and concrete (zones 3–4).

## Design

- **Catalog data:** each source entry may name `host_grounds` (family names) with matching
  `host_weights` (density multipliers ≥ 1; unlisted ground is 1). The family at the find's centre
  decides; concrete also counts right beside its walls (axis probes just past the find's reach), so
  "in and around concrete" works although no find fits inside a thin wall. Types without host data
  are unbiased.
- **Placement:** unchanged depth targets (each band keeps its rate). Each find's lateral candidates
  are grouped by host weight; a group is chosen in proportion to weight × candidates seen, and the
  usual best-spread ranking picks inside it. Host ground therefore carries its weight × the density
  of other ground at the same depth wherever both exist, and plain ground keeps scatter. The draw
  happens only when candidates fall in mixed ground, so all-host layouts (the turf rocks in soil)
  keep their stream.
- **Ground lookup:** `DiscoveryCatalog.Generate(extent, seed, ground)` takes a grid-local material
  sampler; `DiscoveryField` passes the live excavation grid's immutable material field.
- **Roster data now:** plain rocks → soil (weight 3); coal and all ores → rock (weight 3). With
  the current ground that concentrates ore in zone-2 rock masses and in rock rather than clay veins
  deeper, and keeps plain rocks out of gravel lenses. Coins/tokens/beads/marbles → gravel,
  bones/organics → clay and pond clay, waterworks → concrete, rubbish → soil are the rule for
  `012`/`011`, recorded in the importer contract and concept.

## Edge cases

- Where no host ground exists at a find's depth (ore in zone 1), placement is unbiased there.
- A dense host (rock masses) must not fail placement: rejected candidates count as attempts and
  best-spread still applies; clearance rules are unchanged.
- Saved populations keep their positions; host data applies to new games.

## Acceptance criteria

1. Deterministic layouts; exact quotas and depth-band shares unchanged (existing sweeps pass).
2. Over the shipped ground, ore density inside rock (zone-2 rock masses, zone-3 rock) is ≥ 2× its
   density in non-rock ground at the same depths, and plain rocks are denser in soil than in
   gravel lenses; every type still appears outside its host (scatter).
3. Placement stays under 1 s.
4. Screenshot with X-ray shows ore clustering in a rock mass.

## Results (2026-09-27)

Density in host vs other ground at the same depths (shipped ground, discovery seed 12): ore in
zone-2 rock masses ×2.7 of the surrounding clay; deep ore ×3.1 in rock vs clay veins; plain rocks
×1.8 in soil vs gravel lenses (2.5–14 m). Every type still turns up outside its host.

Rejected attempts (kept so they are not re-run):
- **Accept/reject roll before spread ranking:** the best-of-64 spread choice undid the bias for small
  hosts (rock masses are ~1 % of a layer): rock masses held no more ore than clay.
- **"In or beside" for every host:** probing the find's reach counted almost every vein find as rock
  (veins are ~1 m wide), so deep ore showed no preference (×1.0). Only concrete keeps the probe.

The host-weight data model is per family, ready for `096` (ore toward fractured rock and cracks).
