# 14 — Prototype and Validation Plan

Purpose: prove the loop before content exists. Numerical targets are starting points to test and tune.

## 1. The core hypothesis to test

> A player will repeatedly choose "one more thing" over going home, because the detector hint, the
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
| Materials | 3 distinct families with different feel (e.g., soil, clay, rock) |
| Detector | Tool reaction + edge hint, stable target; include one silent optional distinctive |
| Objects | The five first-slice objects: washing machine, hand drill, gearbox, mammoth bone, gramophone; special exhibit/component placeholders for their interactions |
| Clusters | One coherent buried scene, using a bone, vehicle, household or workshop template |
| Hard pockets | One concrete plug diggable by the current tool; upgrades or C4 are much faster |
| Pressure | Shared action-powered battery, return warning, loot-safe recovery fee/debt with a protected next outing |
| Economy | Shared sell/upgrade computer, two tracks (Tool, Battery), 1–2 purchases each, transparent shop |
| Display | Compatible shelf/stand spaces; individual special placement and rereading, no undiscovered silhouettes or inventory screen |
| Surface | Compact yard: shaft, machine, bench, fuel, display |
| Interface | Minimal HUD, world inspection, pause, full rebinding, controller support |
| Saving | Autosave + restore exact hole, position, charge, finds and display |
| Story | One anachronistic junk object for the mystery trail |

Explicitly out of the slice: zones 2–4, the full roster, the ending, achievements, photo mode, late
sinks and finished large-object content. Test local extraction (including bladder-assisted release
and mud unsticking) beneath an overhang separately and early; the whole-object payoff must work
without a transport shaft to the sky or cable clipping.

## 3. Experiments (numbers to discover)

- Voxel size vs. dig satisfaction and recognition readability.
- Seam cleaving: verify that broad cuts along visible seams trigger the crack → shift → break feedback
  cleanly without disrupting neighboring geometry, creating duplicate loot, or auto-collecting finds unseen.
- Starting shovel speed vs. frustration; fewer stronger upgrade steps and output on familiar ground.
- Battery drain per powered action vs. outing length; recovery frequency and empty-wallet restart.
- Bag capacity vs. trip length; where the hard stop actually lands.
- Detector range, frequency, quiet intervals; how often players follow cues.
- Recognition: exposure percentage at which players identify each of the five objects; test late
 wide cuts and C4 too. Full-bag overflow and interesting finds must survive.
- Cluster spacing: how far players search after finding one related object.
- Hard pocket: visible starting-tool progress vs. returning later for a much faster excavation.
- Rare find value: how many expeditions a "big find" should equal.
- Station time: seconds spent in the yard per trip; nonblocking selling/upgrades and individual
 special placement without underground carrying.
- Story delivery: compare before pickup and immediately before placement; display rereading in both.
- Once deeper content exists: test connected major parts and whichever payoff direction is chosen
  (repeated encounters with one huge buried structure are the current experiment), flexible
  discovery order, required-find trails, power growth against tougher ground, and useful play after
  the final major upgrade.
- Tool-tier regression: time-to-clear per material family for each new head vs the previous tier — no new tier may be slower in ground the player already digs.
- C4 value: digging time saved per charge vs its price — a charge must clearly pay for itself.
- Impossibility signature: testers read the clean cut, resonance and dust as wrong material — never
  as a presence, a value cue or something reacting — including with audio muted.

## 4. Validation metrics (playtest gates)

| Metric | Target |
|---|---|
| First noteworthy discovery | within the first 10 minutes, every seed |
| First-session full loop (onboarding) | an unguided tester finds, sells and buys in one session; nobody needs a wiki or video to complete one loop |
| Voluntary lateral digging | the majority of testers dig sideways at least once per session unprompted |
| Recognition quality | ≥ 80% of testers correctly name slice objects from partial exposure |
| Voluntary full uncovering | ≥ 70% choose to keep revealing an interesting object rather than skip it |
| Purchase cadence | prototype-tuned for fewer stronger steps; 30–45 min is a working milestone hypothesis |
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

1. New player, no tutorial, unguided 20–30 minutes in a focused slice (two material types, common/distinctive finds, one connected lateral clue, one oversized salvage set piece, one major tool upgrade, complete loop).
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
2. **Loop prototype:** sell, upgrade, display, detector, first object recognition.
3. **Slice:** all vertical-slice elements above with placeholder art (custom models only).
4. **Pacing pass:** multiple seeds, measure the metrics, tune generation rules.
5. **Content production:** zones 2–4, coherent places, connected major finds, full rosters and ending.
 Validate part relationships and the final object before committing the full content set.
6. **Polish and release prep:** comfort settings, achievements, verification passes.

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
