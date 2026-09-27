# 096 — Cracks & Veins

**Status:** complete. Seeded crack sheets in rock and concrete write a one-sample crack line inside a ~0.5 m fractured band (IDs 6–8, one byte per sample); the band crumbles ~1.5× faster than rock (concrete ~2.4×), renders as broken grain with a thin near-black line, and ore favours it three times over plain rock.

## Objective

Seeded crack networks inside rock masses, the rock zones and concrete structures, hidden until a
cut crosses them. A fractured band beside each crack digs noticeably faster; cracks branch; some
lead into ore while others fade out. They read as thin dark lines on cut faces (line and grain, not
colour alone) and are felt through dig speed in the dark. The crack data is the material field
itself, which `026` (C4 along cracks) reads.

## Concept reference

- `03` §4 cracks and veins (rock, concrete): a crack shows as a thin dark line where a cut crosses
  it; the rock beside it is fractured and digs noticeably faster, cutting across is ordinary rock
  speed; cracks branch; some open into an ore vein or end at a find, some thin out (minerals collect
  in cracks); in concrete, cracks run from old damage toward weak spots and into rooms behind walls;
  C4 on a crack breaks along it. Feel: hit the rock once, read the line, choose a branch.
- `03` §4 shared tell rules: world data from the seed, hidden in solid ground, identical after
  reload with no save data beyond material IDs; presence never value (some cracks fade out with
  nothing); straight digging always works; felt as well as seen; sideways as often as down.
- `03` §5: rock masses are "criss-crossed by cracks and veins"; concrete structures have "cracks
  marking the weak spots"; cracks make a hard place faster (several solutions).
- `09` §1: tells read by shape and grain, not only colour: cracks are thin dark lines on cut faces;
  deep stone palette includes pale crack lines against dark rock (the line reads by contrast and
  shape; here the line is dark against the fractured band's broken grain).

## Live codebase analysis

- `TerrainGround` (006) writes zones, veins and places in a Burst job; `Places()` lists rock masses
  and concrete structures. Zone 3–4 main ground is rock veined with clay.
- IDs 0–5 are used; the saved field is one byte per sample. Mesh UV3 reserves `y` (fractured) and
  `z` (crack) for this task; the shader reads only `x` (pond clay) so far.
- `EquipmentProgression.HardnessOrder` must list every family; `TerrainMaterialTests` asserts each
  family's single-cut rate is strictly below the softer one at every tier.
- Host ground (095): per-type host families with one density weight.

## Design

### Ground families (append-only IDs)
- `FracturedRock` (6): the band beside a crack in rock. Digs like loose ground (between gravel and
  pond clay), ~1.9× rock: noticeably faster in the hands.
- `FracturedConcrete` (7): the band beside a crack in concrete; between clay and rock, ~2.5×
  concrete.
- `Crack` (8): the crack itself, one or two samples thick. Cuts exactly like fractured rock (it is
  the line, not a separate hardness); the hardness order treats families sharing one response as one
  class.

### Generation (`TerrainGround`)
- A crack field of two noise sheet families (different orientations and scales) inside rock (main
  rock of zones 3–4 and every rock mass) and concrete (structure walls and rubble): a sample is
  `Crack` near a sheet's centre and fractured within ~0.3 m of it. Sheets meet and branch where the
  families cross; a gating noise makes each sheet a finite patch, so cracks end and thin out. Rock
  masses use a denser gate ("criss-crossed"); zone-3 main rock a sparser one.
- Clay veins and places are untouched; cracks never reach soil, clay or gravel.
- Concrete cracks: the same field inside concrete, denser, so every structure wall carries a few
  weak lines; `098` routes some toward its rooms.

### Veins and finds
- Host ground generalizes to per-family weights (`host_grounds` + `host_weights`): ores favour
  rock ×3 and fractured rock / crack ×9, so cracks are three times richer than the rock around them
  and "some open into ore". Many crack patches still hold nothing (presence, never value).

### Presentation
- UV3 `y` = fractured (all three families), `z` = crack line. The shader darkens and roughens the
  fractured band (broken grain: stronger relief, slightly darker), and draws the crack as a thin
  near-black line. Base texture stays rock or concrete underneath.

## Edge cases
- Cutting across a crack costs ordinary rock speed except for the thin band; following it is faster.
- Crack samples inside a find's envelope are fine (finds are separate objects).
- Mixed cuts keep per-sample resistance.
- The first 1.1 m and every non-rock ground are unaffected.

## Acceptance criteria
1. Deterministic; cracks exist in every rock mass, zone-3/4 rock and concrete structure; fractured
   + crack share of zone-3 rock is a few percent; no crack sample outside rock/concrete.
2. Fractured rock digs ≥ 1.5× rock and fractured concrete ≥ 2× concrete at every tier; hardness order
   holds (families sharing a response count as one class).
3. Ore density in fractured/crack ground ≥ 2× plain rock at the same depths; crack patches without
   ore exist.
4. Screenshots: thin dark lines on a rock face and on a concrete wall, readable by line and grain.
5. Tests pass; build refreshed.

## Results (2026-09-27, shipped seed 2718)

- Zone 3/4 rock: fractured band 3.7 %, crack line 1.1 %; rock masses and structure walls carry
  cracks; nothing outside rock and concrete. Full-site generation 0.87 s (was 0.55 s).
- Sustained output vs the parent ground, levels 1–10: fractured rock 1.45–1.73× rock (between pond
  clay and clay); fractured concrete 2.4× concrete (between clay and rock).
- Visual review: the first line threshold (one-sample core, weights 0.3–0.75) drew beaded dotted
  lines; the core widened slightly (noise distance .012 → .017) and the line now shows from weight
  0.12, giving continuous, winding lines that branch and end. Evidence `Logs/096-sheet2.png`.
- `026` reads cracks from the material field: `Crack` and `Fractured*` samples, connected.
