# 094 — Deeper Site (150 m)

**Status:** complete. The site is 150 m deep with a 512 MB unpacked save budget (room for 200 m); one zone-aligned ore ladder spans the full depth at a roughly constant find rate, and saving, loading and the first dig stay invisible on a heavily dug full-depth site, so the dug-ground-only save rewrite is not needed.

## Objective

Deepen the excavation from 100 m to 150 m before zones exist, so zone borders (`006`), find depths
(`095`) and jetpack tuning (`022`) are set once. Raise the save budget that the current grid nearly
fills, re-spread the catalog find-depth bands over the full 150 m at a roughly constant find rate,
and prove on a heavily dug full-depth site that saving stays invisible (memory, autosave frame
impact, load time, first-dig hitch). The "store only dug ground" save rewrite happens only if
these measurements fail.

## Concept reference

- `03` §1 Dimensions: the site is **150 m deep**, giving each of four zones room for its main
  ground to be learned; footprint keeps its width. Two named risks: the trip home (jetpack, `022`)
  and visible save stutter (Meltopia's save freezes read as crashes). Background saving must stay
  invisible at full depth, and the extra depth must be filled with finds, never "big but empty".
- `03` §3 zone table: zone 1 recent fill holds coins, coal, copper and junk; zone 2 old sediment
  holds "better ore"; zone 3 deep stone holds "rare ore"; zone 4 holds impossibilities and final
  parts (content from `039`/`041`). Depth splits are roughly even quarters.
- `05` §1 Where finds sit: constant rate, rising value — a metre of descent meets finds at a
  roughly constant rate to the bottom; each type has a narrow core band plus a thin scatter band
  (junk survives deep, the odd valuable turns up shallow); junk belongs to the recent fill, the mid
  ladder to the sediment, and deep ground is mostly worth carrying home. The first few metres stay
  almost as full as the first scrape (the accepted turf/rock/coal entry layer).
- `02` §4: the 150 m depth keeps a roughly constant find rate to the bottom.
- `03` §6: progress never resets; loading restores exactly the hole.

## Live codebase analysis

- `SiteLayout`: 288 × 800 × 208 cells at 0.125 m (36 × 100 × 26 m), origin `(-18, -100, -13)`.
  Deepening appends soil below; the surface stays at `y = 0`, so the yard, rim, stations,
  terrain hole and daylight shaft do not move.
- Samples: 289 × 801 × 209 = 48.4 M. `WorldSaveCodec.MaximumUnpackedBytes = 256 MB` gives
  `MaximumSamples` = 50.3 M — 96% full. At 1200 cells: 72.5 M samples = 290 MB density + 72.5 MB
  material IDs. `ExcavationGrid.ValidateDimensions` and `TerrainMaterialSnapshot` enforce the bound.
- Memory per sample in play: 4 B density (`PagedDensity`, 16 KB copy-on-write pages), 1 B material
  ID, 1 B support-search state (`ExcavationGrid.supportState`, reserved at load). Captures share
  pages; the worker validates and gzip-Fastest encodes on a background thread.
- `TerrainVolume` materializes chunks on demand: 18 × 50 × 13 = 11,700 keys today, 17,550 at
  150 m; startup builds only the top layer, restore walks keys in 8 ms slices.
- Catalogs (`art/minerals/catalog.json`, `art/photo-rock/catalog.json`): rocks 5,390 (640 turf),
  coal 1,200 with a dense 0–5.5 m entry layer; the ore ladder coal→diamond is compressed into
  1–31 m (~110–130 finds/m), then a separate "deep allocation" of gold/emerald/ruby/diamond fills
  31–99 m at ~44 finds/m with one flat mix. Total 12,374 of `MaximumPopulation` 16,384.
- Uniques (`art/retro-computer/catalog.json`) use grid-local authored positions (y = 92/86.5/79,
  i.e. 8/13.5/21 m deep); deepening moves the origin, so these must shift with it.
- Scene: `MainGameSceneBuilder.ConfigureExcavationSize` applies `SiteLayout` (terrain dims, origin,
  preview, bedrock). `LakebedSiteSetup.Performance` sizes the excavation occlusion view volume from
  `SiteLayout.Extent`; baked occlusion must be rebaked afterwards.
- Old saves fail with "The saved excavation layout does not match this game. Start a new game."
  (`WorldSnapshot.ValidateTerrain`); the wire format itself is unchanged, so the version stays 11.
- `SavePerformanceFixture` measured only shallow scenarios, and its cut ray started inside solid
  soil (back faces are not hit), so its "late" scenario could not cut.

## Changes

### Site and save budget
- `SiteLayout.DepthCells = 1200` (150 m); origin `(-18, -150, -13)`.
- `WorldSaveCodec.MaximumUnpackedBytes = 512 MB` → `MaximumSamples` ≈ 104 M. This also covers the
  documented next step (200 m, 96.7 M samples, 483 MB) without another budget edit.
  `MaximumPackedBytes` stays 64 MB unless the measurements say otherwise.
- Re-run **Configure Excavation Size**, **Refresh Lakebed Performance** (view volume) and rebake
  occlusion; `MainGameSceneTests` already derive their checks from `SiteLayout`.

### Find bands (catalog data; prices unchanged)
- Keep the accepted entry layer exactly: rock and coal counts, cover and bands unchanged.
- Replace the compressed ladder plus flat deep allocation with one zone-aligned ladder over the
  whole depth: contiguous core bands (copper 5–27 m, iron 25–47, silver 45–69, gold 67–93,
  emerald 91–115, ruby 113–133, diamond 131–149.2), each with an 86% core share and a wide scatter
  band reaching into neighbouring zones. Counts give a roughly constant ~60–75 finds/m from 10 m to
  the floor (the rock tail keeps 5–10 m denser), with mean value rising ~5 → ~9 → ~17 → ~35 from
  zone 1 to zone 4. Totals stay inside `MaximumPopulation`.
- Remove the superseded deep allocation (`DeepCount/DeepMinDepth/DeepMaxDepth`, importer fields,
  validation and its test) — no entry uses it any more.
- Uniques keep their depths below the surface: authored grid-local y shifts by +50 m. Odd-spot
  placement is `099`'s.

### Measurement fixture
- `SavePerformanceFixture` gains a full-depth `deep` scenario: a wide shaft to the floor, chamber
  fields at 25/50/75% depth and a working room near the floor on seeded materials; the checkpoint
  is reopened from disk (load time, longest loading frame, peak private bytes) and the first cut
  after loading is timed (first-dig hitch). Each scenario now digs in an open working room.
  `-saveProfileUnfocused` measures without waiting for window focus (the unfocused 30 FPS cap then
  bounds frame times; both depths are measured the same way).

## Edge cases

- A 100 m save must fail with the existing "layout does not match — start a new game" message,
  never crash or partially load.
- Digging to the new floor at `y = -150` stops on bedrock; containment at depth holds.
- The first cut into never-touched ground at 140 m must not hitch (chunk materialization on demand).
- Placement must stay deterministic and inside the plot footprint; every depth metre from 6 m to
  the floor holds finds on every seed; no band exceeds the site (`MineralSetup` bound).
- Catalog core depths must still increase along the value order (importer rule).

## Acceptance criteria

1. `SiteLayout.Extent.y == 150`, the scene's terrain, preview, bedrock floor and occlusion view
   volume follow it, and the ground surface, rim, stations and plot are unchanged.
2. The shipped grid fits `MaximumSamples` and the unpacked budget with room for 200 m.
3. A fresh 20-seed population has finds in every metre from 6 m to 149 m (no dry 5 m window), mean
   value rises from zone 1 to zone 4, and placement stays under 1 s.
4. On a heavily dug full-depth site: no frame over 33 ms; capture frames no worse than ordinary
   frames; encode/checkpoint stay on the worker inside the 10 s autosave interval; load from disk
   finishes in a few seconds behind the loading screen; the first cut after load costs no more
   than an ordinary cut; peak memory is recorded against the 100 m baseline.
5. EditMode/PlayMode tests pass; `builds/windows/SomethingDownThere.exe` rebuilt.
6. `docs/baseline.md` describes the 150 m site.

## Measurements (2026-09-27)

Validation player (`SavePerformance.exe -saveProfileSeconds 60 -saveProfileUnfocused`, 1600 × 900,
Ryzen/RX 9060 XT desktop), identical fixture at both depths. "Deep" = shaft to the floor, chamber
fields at 25/50/75 % depth and a working room 4 m above the floor, reopened from disk.

| Deep scenario | 100 m, 12,374 finds | 150 m, 15,683 finds |
|---|---|---|
| Frames mean / p99 / max | 7.0 / 12.3 / 17.4 ms | 7.1 / 13.5 / 18.3 ms |
| Capture frames p99 / max | 15.6 / 15.6 ms | 12.3 / 12.3 ms |
| Main-thread capture mean / max | 2.5 / 7.0 ms | 3.0 / 9.0 ms |
| Worker encode (validate + gzip) | 1.0 s | 1.4 s |
| Packed checkpoint | 6.2 MB | 7.2 MB |
| Load from disk to ready | 2.1 s | 2.5 s |
| Longest frame while loading | 536 ms | 697 ms |
| First cut after load (grid + mesh) / its frame | 6.0 / — ms | 6.1 / 17.9 ms |
| Ordinary cut mean / max | 5.5 / 9.1 ms | 5.5 / 9.4 ms |
| Peak private bytes (incl. the fixture's own carving grid) | 4.76 GB | 5.19 GB |

- No frame exceeded 33 ms in any scenario at either depth; capture frames stay inside the
  ordinary frame distribution. Saving remains invisible, so no dug-ground-only save format.
- The longest loading frame is the find population restore (it scales with the find count, not
  depth: 536 → 697 ms for +27 % finds). It happens behind the loading screen; slicing it across
  frames is a candidate for `082`.
- The fixture holds its own full carving grid while measuring (~435 MB at 150 m), so peak memory
  overstates the game by that much at both depths.
- Earlier fixture runs cut from inside solid soil (back faces are not hit), so the old shallow
  "late" numbers carried no cut timings; every scenario now digs in an open working room.

## Decisions

- **Constant rate over the full depth:** below the accepted dense entry layer (turf rocks, rocks
  and coal in the first ~5 m, unchanged), the rate is ~55–85 finds per metre down to the floor
  (before: ~110–130/m to 31 m, then ~44/m). Total 15,683 finds, inside `MaximumPopulation`.
- **Ladder follows the zones:** concept 03 §3 and 05 §1 place copper/coal in the recent fill,
  "better ore" in the sediment and "rare ore" in the deep stone, so core bands now tile the depth
  instead of compressing the ladder into 31 m. Mean value per find rises ~6.5 → ~10 → ~16.5 → ~35
  by zone. Prices are unchanged; early money per metre is lower than before, which `077` validates
  against purchase cadence.
- The superseded deep allocation (catalog fields, importer and validation) was removed.
- Uniques keep their depths (8/13.5/21 m); odd-spot placement is `099`'s.
