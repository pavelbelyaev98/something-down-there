# 099 — Disturbed Ground, Odd Spots & Detector-Off Test

**Status:** build part complete; the detector-off playtest is the user's.

## Objective

Add loose, mixed backfill ground (digs fast, reads as a chunky messy patch across the natural
banding) in pits and columns above selected finds, every pit holding something; place each unique in
an odd spot (ground unlike its zone, shaped around its reserved envelope). Then playtest a fresh
save with the detector disabled: pass (every unique found without help, at least one tell followed
unprompted) removes the detector; fail keeps it frozen and records why. This session builds
everything the playtest needs; the playtest and its outcome belong to the user.

## Concept reference

- `03` §4 disturbed ground (backfill): when something was buried, a hole was dug and filled again;
  above and around selected buried things the generator leaves a pit or column of loose, mixed
  backfill that cuts across the natural layers; it reads in the wall as a messy, chunky patch
  breaking the clean banding and the tool suddenly sinks in faster; follow it down or sideways and
  something waits at the bottom; only some finds get one; every pit holds something (even only
  rubbish), never an empty decoy. Feel: "why did it just get easy? Someone dug here before me."
- `03` §4 table: backfill digs loose and mixed, fast; it *is* the tell; holds whatever someone
  buried. Tells: world data from the seed, presence never value, felt as well as seen (not colour
  alone: lines, chunks and grain differ), sideways as often as down.
- `03` §4 odd spots: each unique sits in ground that does not match its zone (a gravel pocket in the
  rock, a small concrete room in the clay, a clay lens in the rock); the odd one out is the clue,
  seen with the player's own eyes; the generator shapes it around the unique's reserved space before
  ordinary finds. `03` §8: validation checks every unique sits in an odd spot.
- `05` §2: the detector stays frozen until the detector-off playtest decides (every unique found
  without help and a tell followed unprompted → remove; otherwise keep frozen and record why).
- `09` §1/§5: backfill has its own grain (mixed crumbs and chunks), readable without colour.

## Live codebase analysis

- Ground: `TerrainGround` (zones, lenses, veins, cracks, channels, places, rooms); IDs 0–8; mesh UV3
  `w` = 1 − backfill is reserved. Seats (`TerrainGround.Seats` → `DiscoveryCatalog`) already put a
  find exactly where the ground wants one (sealed rooms).
- Uniques: three authored computers (`art/retro-computer/catalog.json`) at 8, 13.5 and 21 m, all in
  zone 1 (soil); `DiscoveryCatalog` reserves each envelope before ordinary finds. The terrain grid
  is generated in `TerrainVolume.Awake`, before the discovery population, so it cannot read the
  catalog at runtime.
- Detector: `FpsPlayer.Detector` (`FindDetector.Tick`) feeds `GameHudView`'s three-bar panel.
  Admin session toggles live in `FpsPlayer` and `GameMenuView.BuildAdmin`.

## Design

- **Backfill (ID 9)**: the softest ground (~1.5× soil): loose chunky cuts (soil footprint with a
  coarse grain). Shader: mixed chunks — dark loose soil with lumps of clay and gravel stones at a
  coarse scale — so the patch breaks the banding by grain as well as colour.
- **Pits (`TerrainGround.Pits`)**: seeded columns of backfill, mostly steep (some leaning
  sideways), 0.7–1.1 m across and 2.5–6 m long, their tops a few metres above a buried find:
  zone 1 has the most (rubbish pits), then zones 2 and 3; never in places or rooms. Each pit's
  bottom is a seat (1–2 per pit), filled like a room seat by the next find whose depth band covers
  it, so every pit holds something and pits never add or move population counts.
- **Odd spots**: the catalog's authored unique positions are copied onto `TerrainVolume` by the
  discovery sync (`oddSpots`, grid-local centre and envelope radius), and generation shapes a
  flattened lens of ground unlike the zone around each: soil zone → clay or pond clay, clay zone →
  gravel, rock zones → clay. Ordinary finds keep out of the lens (reservation grows by its reach).
- **Detector-off test support**: Developer admin gains a session-only "Detector: ON/OFF", and the
  player accepts `-noDetector` on the command line (works in any build) so a fresh save can be
  played with the panel gone from the first minute. No concept change until the playtest decides.

## Edge cases
- Pits and odd spots stay below the first metres of soil, inside the plot, and never touch places,
  rooms or each other.
- Backfill is not gravel: it never pours.
- An odd spot around a unique never hides it: the unique keeps its reserved envelope.

## Acceptance criteria
1. Backfill digs faster than soil at every level; hardness order holds.
2. Every pit has a find at its bottom; pits exist in zones 1–3 on tested seeds, most in zone 1.
3. Every unique sits in odd-spot ground unlike its zone, around its envelope, with no ordinary find
   inside the lens.
4. The detector can be switched off for a session or from launch; the HUD panel never shows.
5. Screenshots: a backfill pit crossing the banding, a unique's odd spot.
6. Tests pass; build refreshed. The playtest itself: user.

## Detector-off playtest (for the user)
Procedure and pass/fail: [playtest note](../playtests/099-disturbed-ground-odd-spots-detector-test.md).
Pass removes the detector (HUD panel, runtime code, tests and concept references) and updates `032`,
`041`, `080`; fail keeps it frozen and records why here.

## Results (build part, 2026-09-27)

- Shipped seed: 13 pits (6 / 4 / 3 in zones 1–3), 29 seats in all (rooms + pits). Two zone-1 pits
  start 1.7 m down near the plot centre, so the first disturbed ground meets the first shaft. All
  three uniques (8 / 13.5 / 21 m, zone 1) sit in pond-clay lenses. Full-site generation ~1.0 s.
- Backfill sustained output 1.47–1.65× soil at every level. A first response (1.15, 1.1, 1.1, .9)
  gave only 1.22× with the drill, because a shave uses soil's round footprint; it now leans on depth
  and cadence (1.1, 1.05, 1.25, .85).
- Visual review (`Logs/099-sheet2.png`): the first chunk pattern was hard 40 cm axis-aligned
  squares; warped cells now give irregular clay lumps among dark loose stones and darker fill. The
  odd spot reads as a grey-blue lens in brown soil.
- Save re-check on the final Phase 1 ground (Save Performance Player, 60 s per scenario, compared
  with the 150 m run before `006`): encoded saves 4.2 / 5.9 / 9.2 MB (fresh / late / deep, up from
  2.2 / 3.9 / 7.2 with the richer ground); frames during a checkpoint peak at 12.9 / 12.9 / 19.2 ms;
  background encode ~1.7 s; deep reload 2.8 s with a 0.78 s longest loading frame (was 2.5 s /
  0.70 s). Late and deep have no frame over 33 ms; fresh had two (max 41 ms) unrelated to cuts or
  captures (edit max 6.4 ms). A first attempt lost window focus and was discarded.
- The detector-off playtest has not run; the switch and `-noDetector` are in place.
