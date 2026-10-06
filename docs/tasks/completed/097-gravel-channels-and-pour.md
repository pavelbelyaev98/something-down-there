# 097 — Gravel Channels & Pour

**Status:** removed by 107 (asset-only grounds, 2026-10-05): gravel channels and the pour are gone. The body is kept as history so it is not re-run.

## Objective

Winding gravel channels (old riverbeds) through the soil and clay zones, running sideways as often
as down and holding heavy finds via host ground. Undercutting a gravel section releases it in one
bounded pour: its loose gravel disappears as debris and its finds drop, through the existing
detached-ground cleanup and find-release paths. A pour never buries the player, closes a route,
leaves floating specks or deletes a find. C4 under gravel triggers it later (`026`).

## Concept reference

- `03` §4 gravel channels and the pour: a channel is an old riverbed, a winding band of gravel
  through clay or soil, sideways as often as down; heavy finds settled in it; it is solid ground,
  not a tunnel, followed or ignored like a cable. Dig underneath a gravel section and it lets go in
  one rush: loose gravel disappears as debris, finds tumble down; the pour stays inside the
  undercut section (a few metres); never buries the player, never closes a route home, never
  leaves floating specks, never deletes a find. Feel: rushing slide and rattle, then quiet and a
  small pile of finds.
- `03` §4 host ground: gravel holds heavy things (coins, tokens, metal, nuggets). `03` §9: the
  pour is a reward, not a hazard. `03` §3: zone 1 has "the first gravel channels", zone 2 "winding
  gravel channels (old riverbeds)". `02` §4: each zone introduces its tell gently, the first
  channel near the main shaft.
- `03` §6: debris is visual only; collision matches the mesh; interesting finds survive cleanup.
- `09` §4: undercutting gravel, a rushing slide and rattle ends in quiet and a small pile of finds
  (audio is `028`, particle polish `031`).

## Live codebase analysis

- `TerrainGround` writes zones, lenses, veins, cracks and places; gravel exists as zone-1 lenses
  and short vein stretches in rock. No channels.
- `ExcavationGrid.CompleteRemoval` runs detached-soil, sliver and remnant cleanup after every
  removal; `TerrainVolume.TryCut` commits tool cuts (`CommitEdit` rebuilds chunks and notifies finds).
- Finds re-check exposure on terrain change; nearly exposed finds with clear interiors turn
  dynamic and fall (`BuriedFind`/`FindPhysics`), which is the find-release path a pour reuses.
- `SalvageWinch.Feedback` shows the pooled crumb/dust particle pattern with project materials
  (`Content/Salvage/SoilCrumbs.mat`, `SoilDust.mat`).

## Design

- **Channels (`TerrainGround.Channels`)**: seeded polylines in zone 1 (3) and zone 2 (3), each
  18–30 m long, heading wandering horizontally and slope within ±25° (sideways more than down),
  kept under the plot. Cross-section is a flattened tube ~1.6–2 m wide and ~1 m tall. Channels
  replace the zone's main ground (never places). Zone 1's first channel starts within a few metres
  of the plot centre at 4–7 m depth, so the first tell meets the first shaft.
- **Host ground:** copper, silver and gold (metal) also favour gravel ×4.
- **The pour (`ExcavationGrid.TryPourGravel`)**: after a tool cut, gravel samples in and just above
  the cut that now have air directly beneath are seeds; at least 12 seeds (a real gravel ceiling,
  ~0.2 m²) start a pour. The connected solid gravel within 3 m of the seeds (at most ~30 m³) is
  removed, then the usual cleanup clears anything left floating. Removal only, so nothing can bury
  the player or close a route. Finds inside become exposed and the existing release drops them.
- **Feedback (`TerrainVolume`)**: a `Poured` event (volume and centre) for audio/C4, and a burst of
  pooled gravel chips and dust falling from the section, reusing the recovery crumb materials.

## Edge cases

- Digging down onto gravel (air above it) never pours; a sideways cut into a channel wall with no
  ceiling does not either; tunnelling into a channel does (its ceiling is undercut).
- A long channel pours in bounded sections, one per undercut.
- Gravel lenses pour the same way (they are loose gravel too).
- A pour across a chunk seam rebuilds every affected chunk; saves store the result like any cut.

## Acceptance criteria

1. Channels exist in zones 1 and 2 for every tested seed, lie under the plot, run more sideways than
   down, and the first zone-1 channel passes within ~6 m of the plot centre at 4–8 m.
2. Grid tests: undercutting a gravel slab pours it within the reach bound, leaves clay intact and no
   floating fragments; cutting onto gravel or a tiny undercut does not pour.
3. PlayMode: a find inside a poured section survives, turns dynamic and falls.
4. Screenshots: a channel on a cut face, and the cavity plus fallen finds after a pour.
5. Tests pass; build refreshed.

## Results (2026-09-27)

- Shipped seed: 3 channels per zone (93 segments); zone 1 gravel 6.2 % (lenses + channels), zone 2
  0.6 %. Generation 0.9 s for the full site.
- Play Mode check (`Logs/097-sheet.png`): one scoop into the first channel's underside poured
  14.1 m³; the cavity opened above the room, a few ores dropped to its floor, and a neighbouring
  gravel body and the surrounding soil stayed. Debris particles are sparse; their polish (and the
  rushing slide/rattle audio) is `031`/`028`.
- The pour fires only from tool cuts (`TryToolCut`); fixtures and the winch never pour. `026`
  calls the same grid method for C4 under gravel.

## Iteration (playtest 2026-09-27)

- The pour became one of several ground releases (a thin-soil slump was tried and removed again):
  undercut backfill slumps too, and crack bands break loose; see [100](100-ground-lab-and-ground-releases.md).
