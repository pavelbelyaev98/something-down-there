# 100 — Ground Lab, Ground Releases & Tool Retune

**Status:** complete. Development builds open a Ground Lab from the title (soil and backfill bays plus the crane scenes, never saved); every track has twelve levels, the tool ladder runs from the user's level 1 dial to their max dial at level 12 with the drill at 7, hardness is mostly bite size with a little stroke time, and grounds can be dialled live in Developer admin. Its ground releases were removed by 107.

## Objective

Playtest feedback (2026-09-27): the tool was far too strong, every ground felt the same, cracks
"did nothing", top soil felt hardest, and testing the grounds meant hunting for them in the site.
Give the user a place to feel every ground in isolation and mixed, make cracks and loose ground do
something visible, and hand them the dials.

## Concept reference

- `03` §4: each ground differs in how it digs, what it hides and how it points somewhere; tells are
  felt as well as seen. The gravel pour is bounded, removal-only and never buries, traps or closes a
  route (`03` §9, `15`: no hazards, no cave-ins).
- `04` §1/§3: power outpaces resistance; the drill milestone is a clear step; automatic material
  responses stay distinct. `03` zone rules: arriving in a zone at the expected level never feels like
  a restart.
- `09` §4: per-material feel (debris, sound) and tell feedback felt first.

## Decisions (user)

- **Collapse:** loose ground slumps: gravel keeps its pour, undercut backfill slumps (2 m), soil
  slumps only where it is thinner than 1 m above air (roofs, shelves, lips), within 1.5 m. Clay, rock
  and concrete never collapse. Concept 03/15 updated: every release is a few metres of removal only.
- **Cracks:** a cut into the band breaks the connected band loose along the crack (reach 1.5x the
  tool radius, 0.4-1.2 m).
- **Tool ladder:** level 1 = 0.229 m bite, 2.11x stroke time, no reach bonus; level 10 = 0.708 m,
  2.63x, 1.42 m (the user's admin dials).
- **Feel:** hardness changes bite size and a little speed, plus per-ground effects.
- **Tool rig:** a bit more visible (scale 0.30) and a small thrust along the tool instead of a dig.

## Design

- `ExcavationGrid.TryRelease(GroundRelease, cut, reach)` generalises the pour: loose kinds seed on
  undercut samples (solid, air beneath; soil also thin), cracks on band samples the cut exposed; a
  bounded flood fill of the same ground is removed through the normal removal/cleanup path.
  `TerrainVolume.ReleaseGround` runs crack break, pour, backfill slump, soil slump after each tool
  cut and reports `Released(kind, volume, centre)`; debris colours per kind.
- Ladder: shovel radii grow evenly 0.229 → 0.429 m (levels 1-6); the drill (level 7) steps to
  0.545 m so it out-digs the last shovel even on fresh rock, then grows evenly to 0.708 m; stroke
  time and reach grow linearly. The zone rule now reads "two purchases later, at the levels where
  that ground is the working zone" (clay 3-6, rock 6-10).
- Responses regained a small `Interval` (soil 1, gravel 1.03, clay 1.08, pond clay 1.05, rock 1.15,
  concrete 1.25, fractured rock 1.04, fractured concrete 1.1, backfill 0.92) with sizes scaled by the
  cube root so sustained output per ground is unchanged; fuel follows the stroke time.
- `EmitStroke`: every tool stroke throws 2-16 chips plus dust in its ground's colours and sizes.
- Developer ground tuning: `EquipmentProgression.OverrideResponse` (session only), sliders for
  width/length/depth/stroke time per ground, a live table with each ground's effect, print to
  `ground-tuning.txt`.
- Ground Lab: `GroundLab` bays (three rows of six, 3 x 3 m, 12 m deep, rock below) generated into the
  site grid by `ExcavationGrid.UseGroundLab` (cavities carved on reset), `WorldSaveState.Lab`
  (never initialized, no store, quit without saving), title button in development builds, pause
  **Leave Ground Lab** reloads MainGame; the aim prompt names bay and ground (40 m ray).

## Rejected

- Floating 3D bay signs: the built-in font shader ignores depth, so the labels drew over the
  mountains and overlapped; replaced by the aim prompt.
- A separate lab scene: would duplicate MainGame's environment, lighting and UI wiring.
- Soil slump on any undercut soil: every tunnel ceiling and shaft wall would collapse; only thin soil.

## Results

- `GroundReleaseTests` (5): pour, grazing/clay never releases, backfill slump, only thin soil slumps,
  crack break bounded and 4x+ the bite.
- In the lab at level 4: digging down through the thin soil roof slumped 4.6 m³ into the hollow
  (`Logs/lab-roof.png`); cuts into the rock-with-cracks bay broke ~0.23 m³ each (about 10x a rock bite).
- Top soil is not harder by the numbers (level 1: soil 50 L/s, clay 29, rock 21; a first bite on flat
  ground is 80% of a bite in a hole). The likely cause is the permanent bank just outside the plot
  outline (first 1.2 m never removable), right beside the spawn.

## Iteration (2026-09-28)

- The user will redesign the grounds one by one in the Ground Lab: the site now holds one plain
  ground (soil; `SiteLayout.LayeredGround` off, generator kept for the return). The soil slump was
  removed (soil never collapses). Every track has twelve levels (drill stays at 7; the user's max
  dial is level 12; prices and capacities extend at the same rhythm); Ctrl+Shift+0 picks the last
  level. The HUD reads the full depth (150 m) on the site floor. The grounds reference lives in
  `docs/playtests/100-ground-lab.md`.
