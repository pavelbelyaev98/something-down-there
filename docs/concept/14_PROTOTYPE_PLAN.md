# 14 — Prototype and Validation Plan

Purpose: prove the loop before content exists. Numerical targets are starting points to test and tune.

## 1. The core hypothesis to test

> A player will repeatedly choose "one more thing" over going home, because a ground hint, the
> partial silhouette, and the next affordable upgrade all pull harder than the battery warning.

A calm, voluntary return followed by eagerness to dig again is equally valid. Also test whether
related finds suggest a buried place, upgrades transform the scale of excavation, and major parts
build curiosity about what they add up to.

## 2. Vertical slice scope (first playable)

| Element | Slice version |
|---|---|
| Site | One diggable area, full voxel, boundaries visible (concrete + bedrock) |
| Tool | One machine, 2–3 meaningful upgrade levels with visible bolt-on changes, hold/toggle digging, automatic material adaptation stub |
| Jetpack | Stable and usable from the start; ordinary falls are harmless |
| Materials | Soil with backfill pits, then the zone-2 main ground and geodes, each from owned pack textures with its tell and host finds |
| Detector | Frozen as built. The slice is also played with the detector off to test whether ground tells replace it |
| Objects | The five first-slice objects: washing machine, hand drill, gearbox, mammoth bone, buried retro computer; the computer proves unique ownership and crane recovery |
| Crane recovery | Hold Interact on the exposed computer; the crane parks over the hole, its rope descends, attaches and hauls it through a bent player-dug route, clearing load bottlenecks, and the crane sets it down at camp, preserving in-flight saves |
| Clusters | One coherent buried scene, using a bone, vehicle, household or workshop template |
| Geodes | A few hard shells with crystal-lined hollows, diggable by the current tool in seconds; upgrades are much faster |
| Pressure | Shared action-powered battery, return warning, loot-safe recovery fee/debt with a protected next outing |
| Economy | Shared sell/upgrade computer, two tracks (Tool, Battery), 1–2 purchases each, transparent shop |
| Display | Recovered uniques stand at camp where the crane set them, rereadable; no inventory screen |
| Surface | Compact yard: shaft, computer, salvage crane, free charging point, recovered uniques with optional cleaning |
| Interface | Minimal HUD, world inspection, pause, full rebinding, controller support |
| Saving | Autosave + restore exact hole, position, charge, finds and display |
| Story | One anachronistic junk object for the mystery trail |

Mechanics can be tested first with independent unique computer identities sharing the approved model. The detector stays HUD-only and frozen; no physical attachment is built unless it survives the detector-off test. New slice objects and the connected scene remain required before the playable-slice acceptance gate.

Explicitly out of the slice: zones 2–4, the full roster, the ending, achievements, photo mode, late
sinks and finished large-object content. First test the crane's rope on a larger load through
bends and beneath an overhang. Use that evidence to decide whether bladder-assisted release and
mud unsticking need a separate mechanic; the whole-object payoff must work without a transport
shaft to the sky or cable clipping. Gate bulk content on a fresh slice playthrough, early economy
pacing and frame pacing during digging, hauling, lamps and checkpoint writes; retain full-run
and long-session validation before release.

## 3. Experiments (numbers to discover)

- Voxel size vs. dig satisfaction and recognition readability.
- Ground tells: do players notice and follow backfill pits, geodes and lenses unprompted? Is the
  dig-speed change felt in the dark?
- Detector-off run: on a fresh save with the detector disabled, do testers find every unique without
  help? This decides the detector ([Discoveries §2](05_DISCOVERIES.md#2-the-detector)).
- Zone arrival: at the expected tool level, does any zone's main ground feel like a restart?
- Return at depth: jetpack return time from typical working depths across the 150 m site.
- Crane self-rescue vs automatic recovery at zero battery: which feels fairer and better?
- Optional unique cleaning: do players enjoy it, and does anyone feel forced to do it?
- Geodes: does breaking in land as a moment, and do the crystals read before they are collected?
- Starting shovel speed vs. frustration; ten meaningful upgrade levels, the shovel-to-drill milestone and output on familiar ground.
- Battery drain per powered action vs. outing length; recovery frequency and empty-wallet restart.
- Bag capacity vs. trip length; where the hard stop actually lands.
- Recognition: exposure percentage at which players identify each of the five objects; test late
 wide cuts too. Full-bag overflow and interesting finds must survive.
- Cluster spacing: how far players search after finding one related object.
- Geodes: visible starting-tool progress through the shell vs. returning later for a much faster excavation.
- Rare find value: how many expeditions a "big find" should equal.
- Station time: seconds spent in the yard per trip; nonblocking selling/upgrades and crane
 recovery without underground carrying.
- Story delivery: compare before pickup and on arrival at camp; rereading at camp in both.
- Once deeper content exists: test connected major parts and whichever payoff direction is chosen
  (repeated encounters with one huge buried structure are the current experiment), flexible
  discovery order, required-find trails, power growth against tougher ground, and useful play after
  the final major upgrade.
- Tool-tier regression: time-to-clear per material family for each new head vs the previous tier — no new tier may be slower in ground the player already digs.
- Impossibility signature: testers read the clean cut, resonance and dust as wrong material — never
  as a presence, a value cue or something reacting — including with audio muted.

## 4. Validation metrics (playtest gates)

| Metric | Target |
|---|---|
| First noteworthy discovery | within the first 10 minutes, every seed |
| First-session full loop (onboarding) | an unguided tester finds, sells and buys in one session; nobody needs a wiki or video to complete one loop |
| Voluntary lateral digging | the majority of testers dig sideways at least once per session unprompted |
| Ground tells | the majority of testers follow at least one tell unprompted per session |
| Uniques without the detector | every tester finds every slice unique with the detector off, or the detector stays |
| Zone arrival | no tester describes a new zone as slower than the last or as a restart |
| Recognition quality | ≥ 80% of testers correctly name slice objects from partial exposure |
| Voluntary full uncovering | ≥ 70% choose to keep revealing an interesting object rather than skip it |
| Purchase cadence | per [Progression and Economy](06_PROGRESSION_AND_ECONOMY.md) §9 |
| Trip decision | testers want another outing; pushing for one more find is optional, never a requirement for success |
| Return friction | yard + return time ≤ ~15% of session time |
| Station clarity | no tester asks how to sell and upgrade at the computer after using them once |
| Recovery | fair, loot-safe and financially recoverable; ordinary return remains convenient |
| Save integrity | zero lost holes, inventories or display states across interrupted sessions |
| Feel | no floating snags; no unreachable pickups; no stuck spots |
| Performance feel | no cold-start hitch on the first dig; stable frame pacing while digging; no progressive decay across a long session |
| Save write latency | zero perceptible freeze/hitch; frame-impact budget is tested separately from total background save duration |
| Return navigation | testers find their way back to the surface unaided; none report feeling lost |
| Mystery tone (deep-zone pass) | once those zones exist, testers describe the impossibilities as awe and curiosity, never dread |
| Impossibility signature (deep-zone pass) | the contact signature reads as wrong material, never as a response, direction or value cue |

## 5. The core test script (observe, don't explain)

1. New player, no tutorial, unguided 20–30 minutes in a focused slice (two main grounds with their tells, common/distinctive finds, one connected lateral clue, one geode, one oversized salvage set piece, one major tool upgrade, complete loop). Run it once with the detector on and once with it off.
2. Observe key behavioral questions:
   - *Does digging feel good without an imminent reward?* (Digging must be inherently satisfying even during empty stretches).
   - *Do they voluntarily follow a lateral clue?* (Observe if the exposed cable/chain naturally pulls them sideways without a prompt).
   - *Do objects get noticed without becoming chores?* (Verify silhouette recognition without players feeling stalled by common pickups).
   - *Does the upgrade improve the complete outing?* (Measure digging, collecting, travel, and hub time together—not just cutting speed).
   - *Can players return and resume comfortably?* (Test navigation with simple markers, near-full bag, and saving mid-discovery).
3. Inspect the resulting hole: shape, lateral branching, abandoned pockets.
4. Interview: what they remember finding, what they wanted next, what annoyed them.
5. Compare against the metrics above; adjust content distribution and feedback before adding content.

## 6. Build order

1. **Feel prototype:** dig, materials, cleanup, jetpack, battery, recovery. No economy, no art.
2. **Loop prototype:** sell, upgrade, display, first object recognition (the detector was built here and is now frozen).
3. **Ground that matters:** 150 m site, one main ground per zone from owned assets, host ground, disturbed ground, geodes, lenses around uniques and the detector-off test.
4. **Slice:** all vertical-slice elements above with approved existing assets or original placeholder art.
5. **Pacing pass:** multiple seeds, measure the metrics, tune generation rules.
6. **Content production:** zones 2–4, coherent buried scenes, connected major finds, full rosters and ending.
 Validate part relationships and the final object before committing the full content set.
7. **Polish and release prep:** comfort settings, achievements, verification passes.

## 7. Release gates (design-side)

- Core test moment observed repeatedly in external playtests.
- All validation metrics met or consciously waived by the developer.
- Muted + toggle-dig + controller-only full run completes with no blockers.
- Clean-save 100% completion verified (ending, special exhibits, tracks, zones reached); Steam
 achievements checked separately, with all required finds available in each seed.
- No save-loss, no terrain reset, no stuck states, no unreachable finds.
- First-session comprehension: unguided testers complete one full loop unaided and want a second trip.
- Session-length stress run keeps dig rhythm stable: no shader or streaming hitch on the normal
  digging path.
- Save write latency check: saving an extensively deformed late-game excavation causes zero frame freeze or input hitch.
- Return navigation: testers get back to the surface unaided; no lostness or stuck reports.
- Mystery tone check: testers read the impossibilities as wonder, not threat.
