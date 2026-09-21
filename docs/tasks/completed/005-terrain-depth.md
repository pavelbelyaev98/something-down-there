# 005 — Extend `TerrainVolume` to 100 m Depth & Contained Reservoir Boundaries

**Status:** complete. Scaled the site to 24 × 100 × 24 m (0.125 m voxels) with on-demand chunk materialization, so startup and load cost no longer scale with depth. The save-migration path described below was later retired; only current-format saves load.

## Objective

Grow the shipped site from 24 × 32 × 24 m to **24 × 100 × 24 m at the same 0.125 m voxels**,
keeping the surface plane at `y = 0` and the 24 × 24 m opening exactly where it is today. All new
ground is appended below, so the yard, rims, perimeter, stations, grass and daylight shaft do not
move. One soil material, no zones, no catalog or material work — those are 006–011.

Depth must stay one authored number: raising it to 200 m later is a site constant, a scene re-run and
one documented cap constant, and the deepening path added here keeps existing saves valid
(deepening appends soil; shrinking would destroy dug holes, which is why this task starts at 100 m
and grows on playtest evidence).

Concept: `docs/concept/03_WORLD_AND_SITE.md` §1 (dimensions, contained footprint), §2 (permanent
boundaries), §6 (full voxel, exact progress persistence), `docs/concept/13_OPEN_QUESTIONS.md`
(site dimensions).

## Live code analysis

- `MainGame.unity` authors `TerrainVolume` at `dimensions (192, 256, 192)`, `cellSize 0.125`,
  `chunkSize 16` = 24 × 32 × 24 m, on `Excavation` at `(-12, -32, -12)`, with the flat ground plane
  at `y = 0`. `Bedrock/Floor` tops out at `y = -32`; the four `Bedrock` walls run `y ∈ [-32, -1]` and
  the surface rims meet them at `y = -1`. The untouched preview block is 24 × 32 × 24.
- `TerrainVolume.InitializeSession` eagerly creates one `MeshFilter`/`MeshRenderer`/`MeshCollider`
  GameObject per chunk: 2,304 today, 7,200 at 100 m. Only the top layer owns geometry (the ground
  plane); every interior chunk meshes empty. `Restore` rebuilds all of them.
- `ExcavationGrid` stores one float per sample (`PagedDensity`, 16 KB copy-on-write pages) and rejects
  grids above 256 cells/axis; `GridSnapshot.Validate` and `WorldSaveCodec` repeat that bound. The 100 m
  site needs 800 cells on Y and 29,836,449 samples (119.3 MB).
- `TerrainVolume.DigRadius` rejects `radius < cellSize`; the starter bite is 0.230 m, so the 0.125 m
  voxel size is fixed and depth must come from more cells.
- Saves are version 6, gzip-Fastest, guarded by one `MaximumBytes = 72 MB` (packed and unpacked).
  `WorldSaveCodec.Read` decompresses into a second full-size `MemoryStream`. `WorldSaveStore.Commit`
  and `Load` run inside `Task.Run`; `TerrainVolume.Restore` yields every 8 ms.
- `WorldSnapshot.PrepareForTerrain` migrates the shipped 12 m layout into the 32 m site by copying old
  samples above new solid soil and shifting `LowestCarvedY`, keeping finds/inventory untouched.
- Measured 32 m baselines (`builds/validation/saving/Evidence`): fresh save 1.47 MB / 88 ms encode,
  heavily-carved 4.38 MB / 183 ms encode, `capture` 0.01 ms, frame p99 30 ms during autosave, per-cut
  edit mean 26 ms / max 65 ms, peak private memory 1.26 GB.

## Architecture changes

### Site

- New `Runtime/Terrain/SiteLayout.cs`: the authored site numbers in one place — `Size (192, 800, 192)`,
  `CellSize 0.125`, `ChunkSize 16`, `Origin (-12, -100, -12)`, extent 24 × 100 × 24 m.
- `MainGameSceneBuilder` authors that layout directly (no create-at-12-m step) and
  `ConfigureExcavationDepth` becomes idempotent, opens `MainGame.unity` when needed, rescales the
  bedrock shell (`Floor` top face `y = -100`, walls `y ∈ [-100, -1]` with the same corner overlap),
  resizes the preview block, marks the scene dirty and saves it. Runnable from the menu and headless
  through `-executeMethod`.

### Terrain

- `ExcavationGrid.MaximumCellsPerAxis = 2048`, shared by the grid, `GridSnapshot.Validate` and the
  codec's sample bound (covers the planned 200 m / 1600-cell site).
- `ExcavationGrid.AnyModified(start, size)`: allocation-free test of whether a chunk's cell block
  (plus one halo sample) differs from the analytic untouched soil `Min(cellSize * 2, (Size.y - y) * cellSize)`.
- Lazy chunk materialization in `TerrainVolume`: `InitializeSession` builds only the top chunk layer
  (144 objects, the only geometry in untouched ground); `TryDig` materializes chunks inside its existing
  changed-bounds range; `Restore` walks every chunk key inside the existing 8 ms slice and materializes
  only chunks that touch the surface layer or report `AnyModified`; `ResetExcavation` rebuilds
  materialized chunks and releases interior ones that became empty. `ChunkCount` keeps its name and
  now means materialized chunks; `ChunkKeyCount` exposes the 7,200 possible keys.

### Persistence

- `WorldSaveCodec`: `MaximumPackedBytes = 64 MB`, `MaximumUnpackedBytes = 256 MB`, sample bound
  `MaximumUnpackedBytes / sizeof(float)`, and a streaming read that parses the gzip stream directly
  (removes the ~119 MB unpacked buffer) while still hashing and rejecting trailing data.
- `WorldSnapshot.PrepareForTerrain` generalizes the 12 m → 32 m copy into a deepening chain: any
  shallower checkpoint with the same x/z cells, cell size and rotation migrates to the live site by
  prepending solid soil below and shifting `LowestCarvedY`. Finds, inventory, wallet, battery, crouch,
  dig mode and seeds are carried over unchanged. Save version stays 6; no wire fields change.

## Edge cases

- A save dug to the old 32 m floor must keep every sample at its world position after deepening and
  gain untouched soil below; the lower 68 m must read as solid and diggable.
- A 12 m checkpoint must still migrate (now straight to 100 m) without rerolling finds.
- Digging exactly on a chunk seam, and digging at 90+ m depth, must materialize every chunk whose
  surface changed — including neighbors whose samples only changed inside the topology halo.
- The ground plane must stay raycastable after load: the top layer is always materialized, even though
  its samples equal the analytic base.
- Admin reset must leave no stale colliders or empty interior chunk objects holding buffers.
- `PrepareForTerrain` must keep rejecting different cell sizes, different x/z footprints, a moved
  origin, and same-size layout mismatches.
- The 24 × 24 m footprint, rims, yard anchors, grass, daylight shaft and station positions must be
  byte-identical after the scene re-run.

## Acceptance criteria

1. `MainGame` digs from the surface to a bedrock floor at `y = -100` and stops there; lateral cuts stop
   on concrete/bedrock at `x, z = ±12` at 25 m, 50 m and 75 m depth with no gaps, and a player-sized
   capsule cannot leave the box.
2. `TerrainVolume.Dimensions == SiteLayout.Size`, `CellSize == 0.125`, `SurfaceHeight == 0`, chunk keys
   7,200, materialized chunks after `Awake` ≤ 150 and all in the top layer.
3. A representative cut, surface or deep, rebuilds only the chunks around it (4–12) and per-cut timing
   stays inside the recorded 32 m profile (mean ≈ 26 ms, max ≈ 65 ms on the validation carve path).
4. Saving and loading a 100 m world preserves the exact density, and an autosave of a heavily dug world
   adds no main-thread hitch (`capture` ≤ 1 ms, frame p99 ≈ 30 ms, no new per-frame allocation).
5. Existing 12 m and 32 m saves open into the 100 m site, keep their hole at the same world position,
   keep all finds, and re-save as 100 m.
6. Full EditMode and PlayMode suites pass, and `builds/windows/SomethingDownThere.exe` is rebuilt from
   this change.
7. `docs/baseline.md`, `docs/concept/03_WORLD_AND_SITE.md` and the `13_OPEN_QUESTIONS.md` site row
   record the shipped dimensions; the task is ticked in `docs/tasks.md` and this spec moves to
   `docs/tasks/completed/`.

## Verification plan

- EditMode: deep-grid accept/carve/restore and cap rejection; `SiteLayout` fits the budget caps;
  32 m → 100 m deepening (bit-exact old samples, solid new soil, shifted `LowestCarvedY`, untouched
  finds/inventory, round-trip, misuse rejections); 12 m → 100 m chain; scene dimensions, floor, wall
  spans, wall/rim coincidence and preview bounds.
- PlayMode: materialized chunk counts before/after dig, dig-to-floor at `-100`, wall containment at
  depth, seam and deep-cut fan-out, restore at depth, 32 m checkpoint deepened through normal load.
- Performance: `tools/test-fps.ps1` timings plus the save-performance validation fixture extended to
  carve down to ~60 m, recording encode, capture, copy-on-write and frame distributions.

## Evidence (2026-09-19)

- Scene: `MainGame` now authors 192 x 800 x 192 cells at 0.125 m, origin `(-12, -100, -12)`, bedrock
  floor top face at `y = -100`, walls `y ∈ [-100, -1]`, preview 24 x 100 x 24; rims, perimeter, yard
  anchors, grass and stations unchanged.
- Startup: a fresh site materializes 144 chunks of the 7,200 possible keys (top layer only), so
  `Awake` cost is below the old 2,304-chunk baseline. `Restore` walks the keys inside the existing
  8 ms slice and only rebuilds chunks whose samples differ from the untouched base.
- Dig: the deep bore to the bedrock floor stays inside 1-16 rebuilt chunks per stroke, and
  wall/floor containment passes at 30/60/90 m depth; `MainGameExcavatesThroughFormerFloorAndStopsAtOneHundredMetres`
  ran in 1.7 s.
- Checkpoint: validated + gzip-Fastest encoded on the worker thread; Editor measurements for the
  untouched 100 m density are 25 ms validate and 288 ms gzip (< 1 MB packed), with capture at 0.2 ms.
  The autosave test's end-to-end checkpoint latency is ~1.0 s and its budget is 2 s; frame impact is
  the save-performance fixture's standing gate.
- Suites: EditMode 220/220, PlayMode 187/187; `builds/windows/SomethingDownThere.exe` rebuilt.
- Repairs: three tests left stale by the previous day's price/reach retune (backpack/fuel tier price
  of 10 credited as 6, and the 8 m reach cap asserted as 4) now read the authored constants.

## Out of scope / follow-ups

- 200 m depth: change `SiteLayout` to 1600 cells and re-run the layout method — the caps already cover
  it (`MaximumCellsPerAxis = 2048`, `MaximumUnpackedBytes = 256 MB` against a 238 MB density, so the
  budget test should be re-read). Must land before 006/011 author zone and find bands if the playtest
  says the shaft is too short.
- Industrial boundary art (cracked concrete dam walls, intake tower, natural shelf) — later art task;
  deep ground stays one soil material and the existing bedrock shell.
- Zone materials, seams, hard pockets, lighting mood and true darkness (006–010), find rebalance (011),
  depth-aware return warning and battery rebalance (024).
