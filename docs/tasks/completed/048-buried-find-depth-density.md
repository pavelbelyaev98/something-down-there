# 048 — Buried Find Depth Density (Immediate Feel Pass)

**Status:** complete. Depth changes the mix, not whether a metre of digging pays. Placement uses a deterministic spatial grid. The body records rejected attempts, including tiny finds and placer saturation. Do not re-run those, and do not treat any count in this file as current. Populations live in the source catalogs. The rule is `docs/concept/05_DISCOVERIES.md` §1.

## Objective

Make digging below 2 m feel as populated as the entry layer, without touching the shallow burst,
the economy or the disabled bottle content.

Today 312 of 1,024 finds sit in the top 0.65–1.1 m (≈1.2 finds/m³) while everything below 2 m
averages ≈0.04 finds/m³; the 2–3 m slice holds only the 24 leftover coal across the whole
24 × 24 m footprint (≈0.01 finds/m³). With the level-1 scoop (0.35 m radius, one stroke per
0.46 s) that reads as one find every 60–80 s of continuous digging below 2 m.

Target: **2,042 finds** (+99% of 1,024; deep region 712 → 1,730, +143%) with no 1 m depth slice
below 2 m under 30 finds and a mean of ≈60/m (≈0.10 finds/m³) — about one find per metre of a
2 × 2 m shaft descent instead of one per ~3 m.

## Concept Reference

- `05_DISCOVERIES.md`: commons are always worth collecting and stay worthwhile at every depth;
  ordinary hauls pay while special finds get the display moment. New density rule recorded there.
- `02_CORE_LOOP.md` §4: dry spells between notable finds are bounded along representative routes.
- `03_WORLD_AND_SITE.md` §8: positions and depths are randomized per save inside authored bands;
  the accepted population is finite and persisted.
- `15_ANTI_PATTERNS.md`: never clump all novelty early or leave the late game empty; never reroll
  an existing save.

## Live Codebase Analysis

- `Runtime/Interaction/DiscoveryCatalog.cs` generates the population: entries carry `Count`,
  `ShallowCount`, `MinDepth`, `MaxDepth`; the first `ShallowCount` placements use the fixed
  0.65–1.1 m entry rule, every other placement uses its authored band. Banded placement
  maximizes spacing in 3D, the shallow batch in XZ only.
- `Runtime/Interaction/DiscoveryField.cs` owns placement. `MaximumPopulation` is 1,024 and the
  current catalog is exactly at the cap, so any addition fails validation. `Generate` scans every
  earlier placement per candidate (`O(n²)`), which is why the population could not simply grow.
- `Editor/MineralSetup.cs` already reads `minimum_depth_m` / `maximum_depth_m` for the eight
  minerals and enforces strictly increasing minimum depth and sale value.
- `Editor/PhotoRockSetup.cs` reads the single `common_rock` entry with three appearances but no
  depth fields; rocks are therefore unbanded and can only start shallow.
- Scene `MainGame` holds `Discoveries.count = 1024`; `StarterFindSetup.ConfigureScene` rewrites it
  from `catalog.TotalCount` during sync. Terrain is 192 × 256 × 192 cells at 0.125 m
  (24 × 24 × 32 m) and is not changing.
- Four PlayMode tests hard-code 1,024 finds (`DiscoveryIntegrationTests`, `StartupMenuTests`,
  `SaveIntegrationTests` twice); EditMode quota tests hard-code the exact per-entry counts.
- `FindSnapshot` stores identity, pose and collected flag only; exposure is recomputed from the
  terrain, so an old save keeps its old 1,024-find population and never rerolls.

## Implementation

### 1. Catalog data (authored source of truth)

`art/minerals/catalog.json`, `art/photo-rock/catalog.json`:

| Entry | Count | Shallow | Band (m) | Was |
|---|---|---|---|---|
| common_rock | 396 | 96 | 1.1–31.2 | 96, all shallow |
| mineral_coal | 596 | 216 | 1.1–31.2 | 240, 0.65–5 |
| mineral_copper | 240 | 0 | 2.0–9 | 160, 3–9 |
| mineral_iron | 190 | 0 | 6–14 | 128, 7–13 |
| mineral_silver | 165 | 0 | 10–18 | 112, 11–17 |
| mineral_gold | 145 | 0 | 14–22 | 96, 15–22 |
| mineral_emerald | 125 | 0 | 19–26 | 80, 20–26 |
| mineral_ruby | 100 | 0 | 23–29 | 64, 24–29 |
| mineral_diamond | 85 | 0 | 27–31.2 | 48, 28–31.2 |

Rock and coal are the all-depth filler; bands start at 1.1 m so nothing gaps directly under the
entry layer. Ladder bands widen so their overlaps smooth the profile while minimum depth and
sale value still increase strictly. Bottles stay `instances: 0`.

`PhotoRockSetup.cs` gains the two depth fields on its source contract and validates them the way
`MineralSetup.cs` does before writing them into the catalog entry.

### 2. Placement scaling (`DiscoveryField.cs`)

- `MaximumPopulation` 1,024 → 8,192: a safety bound for saves, snapshot validation and the
  serialized count range, not a target.
- Replace the `O(n²)` earlier-placement scan with a deterministic spatial grid (flat
  `int[] head` / `int[] next` buckets, cell edge `MinimumSpacing + 0.001` = 1.151 m, which
  covers the largest required clearance of 1.10 m). Clearance is tested against the ±1 cell ring;
  the best-candidate ranking metric is measured over the ±2 cell ring and capped so candidates with
  no close neighbour tie. Candidate sampling order, attempt budgets and the 64-candidate best-of
  window stay unchanged, so layouts stay deterministic per seed and only positions change.
- Seed layouts change (no golden positions are asserted anywhere); every separation, coverage and
  soil-enclosure invariant is preserved.

### 3. Tuning readout

Session-only line in the Developer admin panel (`GameMenuView.BuildAdmin`, existing "Body"
block): exposed / total finds, collected finds, m³ removed and finds per m³, computed on demand
from `BuriedFind.Exposure` and `ExcavatedVolume`. Nothing new is saved.

## Edge Cases

- Existing saves: snapshots keep their own find list (1,024 or fewer); `ValidateRestore` resolves
  the same content ids, the cap only rises, so nothing rerolls or migrates.
- Density must stay achievable inside the soil envelope for every seed; the placement failure path
  must never trigger between 2 m and the floor.
- The shallow batch is untouched: 312 finds, all in 0.65–1.1 m, still covering the whole topsoil.
- Bottles stay at zero count and keep their restorable identity/pose/value path.
- Bag-full behaviour is unchanged: a find stays in the world; denser ground makes it more common.
- X-ray markers now spawn for ~2,000 finds; dev-only, must still toggle without stutter.

## Acceptance Criteria

- Catalog totals: 2,042 finds, 312 shallow, 1,730 banded, per-entry counts exactly as above.
- For seeds 0–99: every 1 m slice between 2 m and 30 m holds ≥30 finds, no slice exceeds 110, and
  the mean is ≥45 (0.10 finds/m³ target with the existing 576 m² footprint).
- Placement invariants hold for every seed: no pair closer than its soil envelope, every rotated
  mesh vertex starts inside soil, determinism (same seed replays exactly).
- Generation of the full population stays under 1 s warm; no new-game hitch beyond that budget.
- Old-save compatibility case passes: a 1,024-find snapshot validates, restores unchanged and is
  never rerolled.
- EditMode and PlayMode suites pass; `builds/windows/SomethingDownThere.exe` is rebuilt and a
  fresh-game playtest shows no barren stretch longer than ~20 s of continuous digging below 2 m.

## Verification

- EditMode: 215/215 passed. PlayMode: 186/186 passed (previous baseline run had four
  grab/dig helper failures).
- Placement: 2,042 finds in **46 ms** warm (guarded by a < 1 s regression test); the 100-seed
  sweep places every find, keeps the soil envelope and holds ≥30 finds in every 1 m slice from
  2 m to 30 m.
- Old saves: a 1,024-find population still resolves every content id, keeps its pose and value
  and is never rerolled (`PreviousThousandFindPopulationStillResolvesWithoutReroll`).
- `builds/windows/SomethingDownThere.exe` rebuilt through
  `Tools/Something Down There/Build Windows Player` on 2026-09-17.

### Test adjustments that came with the denser world

- Grab/dig helpers now take the first *diggable* soil hit: a dense field can put another find, or a
  carved ledge that is already void, in the ray before the ground.
- The 2 x 2 m blind entry patch asserts ≥2 revealed finds (authored density is ~0.54 finds/m², so
  ~2 per 4 m²); the ≥1-collectible and first-encounter bounds are unchanged.
- The dropped-find settle test drops on the flat yard grid its sibling test already validates
  (three metres apart, stable item order) and waits 10 s before measuring drift, because a hard
  drop on voxel ground can keep creeping before PhysX sleeps it.
- Mineral counts include coal: 1,646 mineral + 396 rock finds.

### Handed to playtest

- Dig a fresh shaft to ~12 m and read the Developer admin line: below 2 m it should show roughly
  0.05–0.12 exposed finds per m³ with no stretch longer than ~20 s of continuous digging without a
  find. X-ray now draws ~2,000 markers; verify it still toggles without stutter.

## Iteration — depth decides the mix (same day)

Playtest feedback: filler junk everywhere made depth a colour swap. The catalog now gives every
type a dense **core band** plus a thin **scatter band** (`CoreMinDepth`, `CoreMaxDepth`,
`CoreShare` on the catalog entry, authored as `core_minimum_depth_m` and friends in the source
JSONs). `CoreShare` of a type's banded finds land inside the core, the rest scatter across the
wider band — which is what produces the odd valuable find near the surface and the odd lump of junk
deep.

- **1,996 finds total** (312 entry burst unchanged, 1,684 banded), core rate ~16 finds per metre
  of depth per type, three overlapping cores at every depth, so a metre of descent meets a find at
  roughly the same rate from top to bottom (3 m rolling windows stay within 1.6x).
- Mix by depth: rock+coal are 61% of finds at 2–5 m, 33% at 6–9 m, 5% at 10–13 m and ~0% below
  22 m; gold and above are 5% at 2–8 m, 34% at 14–17 m, 63% at 18–21 m and 94–100% below 22 m.
  Scatter keeps ~8 cheap finds and ~21 valuable finds outside their core bands per seed.
- Price per type is unchanged; the site's total sellable value rises to ≈$22.6k, which task `001`
  and `006` re-price against.

### Iteration verification

- EditMode 216/216 (new `DepthMixSlidesFromJunkToValueAndKeepsScatteredOutliers`, rewritten
  constant-rate window check, core-share check per entry).
- Concept updated in place: `05_DISCOVERIES.md` §1 and §7, `06_PROGRESSION_AND_ECONOMY.md` §3 now
  state the constant-rate / rising-mix rule instead of "commons stay worthwhile at any depth".
- Windows build rebuilt after the iteration.
