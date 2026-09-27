# Roadmap & Tasks

Next: **099 — Disturbed Ground, Odd Spots & Detector-Off Test**. Queue order is priority; completed decisions live in [completed specs](tasks/completed/). Follow [AGENTS.md](../AGENTS.md) for task specs and completion. Each entry states the goal, the intended feel and the constraints; the reasoning lives in the linked concept sections, and the full spec is written when the task starts.

Standing decisions: the HUD detector is **frozen** (kept as built, no new features, nothing new may depend on it) until `099` decides its fate. Seam cleaving (`009`) and the bladder release (`076`) were dropped, and hard pockets (`010`) became ground places inside `006`; reasons are in [Closed ideas](concept/13_OPEN_QUESTIONS.md#closed-ideas). Research evidence behind the rules: [`.research/`](../.research/README.md).

## Phase 1 — Ground that matters

Materials only differed in dig speed and the ground repeated the same thin stack every 8 m. This phase makes each zone its own place and turns dig-speed changes into the clue system ([concept 03 §3–5](concept/03_WORLD_AND_SITE.md#3-the-four-zones)).

- [ ] **`099` — Disturbed Ground, Odd Spots & Detector-Off Test** (`03` §4, `05` §2): add a loose, mixed backfill ground (digs fast, reads as a chunky messy patch across the natural banding) in pits and columns above selected finds; every pit holds something. Place each unique in an odd spot, ground unlike its zone, shaped around its reserved envelope. Then playtest a fresh save with the detector disabled. Pass: testers find every unique without help and follow at least one tell unprompted, so remove the detector (HUD panel, runtime code, tests and concept references) and update `032`, `041` and `080`. Fail: keep it frozen and record why in the spec. Feel: "why did it just get easy? Someone dug here before me."

## Phase 2 — Tool and power

- [ ] **`001` — Evolving Motorized Tool Rig** (`04`, `06`): visible ordinary shovel evolving into a drill and then a machine with readable bolt-on attachments following the progression settled in `087`. Animation and silhouette communicate the automatic material response (which head is biting) and stronger upgrades; a purchase changes the model instantly, never with a blocking animation (Meltopia notes). Reuse the existing tool/input pipeline and keep digging, pickup and aiming unobstructed. No detector attachment while it is frozen.
- [ ] **`022` — Jetpack Hover Hold & Speed Progression** (`04` §5, `06`): upgrade-driven ascent speed, fuel efficiency and hover-hold assist in narrow shafts, using `087` progression. At 150 m the trip home is the risk (Keep Digging 2.0's 5,000 m slog): at the jetpack level a player typically owns, returning from their working depth stays short and yard plus return time stays within the return-friction target. Establish real ascent costs before calibrating `024`.
- [ ] **`024` — Depth-Aware Return Warning** (`02`, `06`): replace charge fractions with a safe/risky/critical estimate from depth, usable ascent route and the live jetpack tier after `022`. Validate against actual return fuel; keep additions small for later HUD consolidation in `032`.
- [ ] **`026` — Sticky C4 Charges & Material Reactions** (`04` §8): valid/invalid preview, sticky landing, remote detonation and a predictable blast volume using the existing dig/cleanup pipeline. Material reactions: a big blast in every ground (never weak in clay or anywhere common), breaking along cracks from `096`, breaking the slab between cracks in concrete, triggering the gravel pour from `097`. Finds, uniques and lamps survive; invalid placement consumes nothing; charges cost money, not battery. Research: Keep Digging's dynamite was "beyond useless", Meltopia's dirt nerf made its shovel hated, and A Game About Digging a Hole's dynamite bounced and clipped. Price against `087`, repeat slice-object recognition tests and measure time saved in `088` and `077`.
- [ ] **`023` — Crouch Footprint Narrowing** (`04` §6): narrower bite footprint while crouched for delicate carving around silhouettes, including half-sunk finds in sealed-room silt.

## Phase 3 — Slice content

- [ ] **`012` — Four Remaining Slice Finds** (`05`, `14`): add the washing machine, hand drill, gearbox and mammoth bone to the existing discovery foundation, with explicit category and recovery policy, placed in their host ground (the mammoth bone in clay, the gearbox and hand drill in or near concrete structures, the washing machine in recent fill). Distinctives sell; the accepted computer unique stays unsellable. Keep the detector-eligibility field as data only; no new detector work. Validate silhouettes with current wide cuts, then repeat blast recognition in `026`.
- [ ] **`013` — Buried Connections ("Follow the Thing") & Clusters** (`03`, `05`): first author one coherent scene around the slice finds, with physical cables, chains, pipes or tiles leading between them, set in matching ground or a ground place. Small finds sit along each trail so following it pays along the way, and each zone gets its own trail type (chains and cables, pipes and machine parts, cracks and veins, ancient grooves). Prove the follow-the-thing payoff before expanding templates.
- [ ] **`075` — Oversized Salvage Set Pieces & Cash-in** (`05`, `06`, `07`): first prove the accepted winch on one larger load through bends and overhangs, with once-only surface-computer payout and salvage records. Validate clearance and repeated recoveries, then decide in this task whether any heavy find needs a special local release (the former `076` bladder idea), only if the winch cannot deliver it. `011` reuses this extraction/payout path. Preserve the computer unique's unsellable destination; includes former `017` cash-in scope.
- [ ] **`014` — Crackable Buried Containers** (`05` §3): suitcases and toolboxes cracked open in the world with the machine, revealing nested discoveries; the contents are a second reveal and a small piece of buried history. No lockpicking or keys; contents belong to the finite population and never reroll.
- [ ] **`088` — Playable-Slice Acceptance & Performance Gate** (`02`, `03`, `06`, `14`): play a fresh slice through recognition, a connected scene, ground tells, a sealed room, upgrades, return and larger-object recovery, once with the detector on and once off (feeding `099`). Measure early find/purchase cadence, dry spells, zone-arrival speed (no restart), return time at depth and C4 value; profile first dig, sustained digging, pours, extraction, lamps and checkpoint frame impact at the intended defaults. Decide price-on-hover (`016`) from this playtest. Fix blockers before bulk content; full-run pacing and long-session gates remain `077`/`082`.

## Phase 4 — Content and economy

- [ ] **`078` — String-Table Text Foundation** (`01`): Localization string tables for every current and upcoming gameplay/UI string, so `016`, `018`, `035` and `044` never hardcode text.
- [ ] **`011` — Discovery Catalog & Quotas, Five Categories** (`05`): commons, distinctives, uniques, oversized salvage and ending parts across the zones — the full concept 05 §1 table — with host-ground weights and new silhouettes to the bottom of the 150 m site so extra depth is never empty ground. Detector eligibility is authored only if `099` keeps the detector. The five slice objects are `012`'s, not this roster.
- [ ] **`004` — Free Automatic Recharge at Camp** (`06` §5, `07` §1): replace the paid `SurfaceRecharge` service with a free, automatic refill while the player is back at camp, shown by a visible charging point (cable and charge light); no button, no price, no computer row. Bigger tanks fill fully. The recovery fee remains the only battery-related cost. Research: Keep Digging's recharge cost grew with battery size and felt like punishment; Meltopia's free refuelling drew no complaints.
- [ ] **`025` — Recovery Keeps Loot, Debt & Winch Self-Rescue Test** (`04` §9, `06` §5): recovery at zero battery (or "I'm stuck" from pause) keeps all finds, charges a depth-scaled fee and applies interest-free debt when broke; ordinary falls stay harmless. Prototype the winch self-rescue: the rim winch lowers its rope down the player's own hole, hooks them and hauls them up the dug route (reusing the rope route planner, with the player carried rather than simulated), fee on arrival. Compare it with the current automatic recovery and keep the one that feels better. Never ask the player to dig their own way out.
- [ ] **`077` — Generation & Economy Pacing Validation** (`02`, `03`, `06`): reject candidate layouts that violate the pacing rules — noteworthy find within the first ten minutes, bounded dry spells over the full 150 m, one major per zone, majors never clumped, novelty to the bottom — and the ground rules: every unique in an odd spot, a soft path through every rock body, sealed rooms closed, each zone's first tell near the shaft. Also validate purchase cadence, zone-arrival speed against the tool ladder, the late economy (final meaningful purchase around 75%, money never dies halfway) and C4 charge value. Acceptance gate for the whole phase.
- [ ] **`018` — Trophy Displays, Lore, Salvage Records & Optional Cleaning** (`07` §5, `05` §8): extend the working unique placement and rereadable name/depth/lore record into compatible physical stands and salvage miniatures/photos of the actual find and yard. Uniques arrive caked in mud; holding dig on one at the yard sprays the mud off in big chunks within a few seconds, revealing its real colours. Cleaning is optional, has no meter and never gates placement; the story line appears when the unique is cleaned or placed, whichever comes first. Includes the remaining `019` inspection scope; keep individual manual socketing and no undiscovered silhouettes.

## Phase 5 — Saves, interface and controls

- [ ] **`037` — Multi-Slot Profiles & Independent Rolling Backups** (`08`): extend whole-world saves to explicit profile slots and independent rolling recovery checkpoints beyond the existing previous-file fallback. Preserve voxels/materials, finds, equipment, display and player state. No excavation-history journaling is needed: the ending uses before-and-after. Implement current-format data only.
- [ ] **`079` — Title & Save-Slot Flow** (`08`): extend the existing Continue/New Game/Settings/Quit title and overwrite confirmation with slot selection and clear ownership/overwrite feedback. Coordinate with `037`; provide a reliable destination for `033` Exit to Title without rebuilding current startup flow.
- [ ] **`033` — Pause Save/Load Navigation & Exit to Title** (`08`): extend existing pause/settings/resume behavior with save/load navigation and Exit to Title using `079`/`037`; ESC/B closes reliably and checkpoint work cannot trap input or discard progress silently. Hosts the "I'm stuck" recovery entry from `025`.
- [ ] **`032` — Minimal Diegetic HUD** (`08`): refactor `GameHudView` to depth meter, bag gauge, battery bar, return warning and a clean reticle, consolidating what `015`/`024` bolted on. The detector panel stays or goes according to `099`.
- [ ] **`034` — Gamepad Binding Support** (`08`, `10`): controller binding in `InputPreferences`, UI Toolkit navigation, automatic glyph swapping.
- [ ] **`080` — Accessibility Baseline** (`10`): FOV default 90° (60–110° range), comfort preset, colorblind palettes for materials, tells and UI, shape+label redundancy checks (tells readable by line and grain, not colour alone), contrast/brightness options, and the optional first-launch comfort preview with its dark-areas note. Detector audio-ping and high-contrast options only if `099` keeps the detector.

## Phase 6 — Audio and feel

- [ ] **`028` — Material-Specific Digging Audio Loops** (`09` §4–6): gravel rattle, clay thump, soil hiss, rock crack, concrete screech, a soft hollow give for backfill, crack runs, the gravel pour's rushing slide and sealed-room break-throughs; spec includes the audio foundation (mixer, settings, feedback channels). No music.
- [ ] **`029` — Motor Whine & Strain Audio** (`09`): engine pitch by tool tier; strain in dense material; puff release on cut completion.
- [ ] **`030` — Cavern Reverb & Depth Low-Pass** (`03`, `09`): depth-based low-pass filtering and reverb deepening with descent, including the enclosed sound of sealed rooms.
- [ ] **`081` — Zone Ambience Layers** (`09`): wind/distant water → drips/settling rock → near-silent ancient hum, crossfading with depth and following the new zone borders.
- [ ] **`031` — Material Debris Particles & Cut Release Juice** (`09`): directional crumbs and dust puffs on stroke completion, pour debris, break-through dust drifting into a sealed room, yard cleaning spray; subtle settling feedback.
- [ ] **`035` — Directional Sound Captions** (`10`): captions for hearing accessibility; rides the audio phase's content.

## Phase 7 — Mystery and ending

Decide the mystery payoff ([Open Questions](concept/13_OPEN_QUESTIONS.md#content-and-systems)) before starting this phase.

- [ ] **`038` — Anachronistic Mystery Trail Oddities** (`01`, `11`): subtle oddities with visible impossibility establishing curiosity, placed in their zones' host ground.
- [ ] **`039` — Zone 4 Ancient Constructed Structure & Material** (`03`, `11`): the zone-4 main ground (its own material, clearly unlike bedrock), anomalous architecture at the bottom of the dig and the ancient sealed chamber; replaces the `006` placeholder.
- [ ] **`040` — Ancient Material Contact Signature** (`09`, `11`): clean surgical cuts, glass-like resonance, too-neat dust; no threat cues and never a direction, proximity or value cue.
- [ ] **`041` — Finale Components & Assembly Sockets** (`11`): 3–4 components with recoverable physical leads (cables, pipes) and ground tells; owned parts auto-insert into sockets. Nothing required may depend on the detector.
- [ ] **`042` — Final Object Presentation Cutscene** (`01`, `11`): reveal the final object chosen with the mystery payoff; normal tools, no genre switch.
- [ ] **`043` — Ending Before-and-After** (`11` §5): show the site as it was before the first dig (rebuilt from the save's seed) next to the player's hole, then the display wall; pausable and skippable. No recording during play.
- [ ] **`044` — Continue Playing & Media Wall** (`07` §6, `11` §6): after the ending the same save continues with the whole site still diggable; newspaper clippings and quiet TV/radio props referencing this save's finds and yard.

## Phase 8 — Yard extras

- [ ] **`020` — Authored Yard Props** (`07`): pickup truck, utility trailer, generator, charging cable, floodlights within 10 s of the shaft.
- [ ] **`021` — Yard Cosmetic Milestones & Sinks** (`07`, `06`): optional worksite evolutions (workbench shelter, display tarp, tool skins, extra lamps) as late money sinks; purely cosmetic.
- [ ] **`036` — Photo Mode** (`08`): pause-only — hide HUD, FOV, filters, watermark. No free camera (concept 08 §6): the player composes from their own view.

## Phase 9 — Release

- [ ] **`082` — Performance & Long-Session Validation** (`14`): stable frame pacing while digging, no cold-start hitch on first dig, no per-launch shader compilation, no progressive decay across a long session, and save-write latency inside the frame-impact budget on a heavily dug 150 m site. Performance failure is the dominant technical complaint across the reference corpora.
- [ ] **`046` — Native IL2CPP Windows Pipeline**: IL2CPP with MSVC; `link.xml` against code-stripping of UI Toolkit, save and Steamworks types.
- [ ] **`083` — Clean-Save Verification & Assist Runs** (`12`, `14`): on the shipping build — clean-save 100% with every required find spawn-obtainable and findable in each seed (never-spawning collectibles locked 100% in a reference game), muted + toggle-dig + controller-only full run, interrupted-session save integrity, and an economy exploit audit (duplicated value, repeated credit, purchase bypass).
- [ ] **`084` — Steam Cloud Saves** (`08`): cloud-sync the existing save format without format changes; verify slot/profile behavior. Requested across every reference corpus.
- [ ] **`085` — Store Page & Content Disclosure** (`01`): honest copy — digging, dark areas, no horror, one difficulty, length, price — plus tags and media. The reference corpora's harshest backlashes came from undisclosed content; the honesty promise is a release requirement.
- [ ] **`045` — Steam Achievements** (`12`): wire fair achievements and re-verify after `046` stripping; optional `047` must also preserve them if adopted. No all-achievements meta-achievement; offline earning queues locally and syncs.
- [ ] **`047` — Release Obfuscation — Optional**: assess concrete value and maintenance cost after `046`; symbol policy and any renaming/string/metadata obfuscation must preserve serialization, reflection and Steam behavior. Not a release blocker and no paid utility without product-specific approval.

## Parked

- [ ] **`016` — Hover Price Tag on Exposed Finds** (`05`, `08`, `13`): subtle fixed sale price when the reticle hovers a collectible sellable find. Decided from the `088` playtest.
