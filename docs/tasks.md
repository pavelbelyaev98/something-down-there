# Roadmap & Tasks

## Status

- **Next Task:** `007` — Material IDs & Tool Auto-Adaptation.
- **Build:** `builds/windows/SomethingDownThere.exe` (MainGame; current-format saves only).
- **Direction:** Materials next; `001` waits. Core functionality precedes audio/feel polish; each mechanic ships with testable content.
- **Completed history:** `docs/tasks/completed/` — one spec per finished task; each header carries the final-state summary.

---

## Priority Queue

> **Workflow:** Starting a task: create a thorough spec at `docs/tasks/<ID>-<slug>.md` (Objective, live code analysis, architecture changes, edge cases, Acceptance Criteria). Completing a task: move the spec to `docs/tasks/completed/`, rewrite its header to a 1–2 sentence final-state summary, delete the queue entry here (collapsing a finished phase to one line), and update `docs/baseline.md` if baseline systems changed. A pending entry keeps its title, concept refs, and goal with constraints. Do not shorten it to a bare title. A spec header must not state a count, price, or layout the game no longer uses.
> **Note:** Tasks labelled _Upgrade/Extend/Refactor_ modify existing working systems — inspect `docs/baseline.md` and `docs/architecture.md` first, never rebuild from scratch.

### Phase 0: Immediate Feel Pass

Complete (`048`–`073`) — specs in `docs/tasks/completed/`.

### Next: current dig + first testable content

- [ ] **`007` — Material IDs & Tool Auto-Adaptation** (`03`, `04`): assign material IDs on the voxels that already exist. Soil, clay, and rock differ in shaving speed, resistance, and sound *hooks* (actual audio arrives with `028`). No mode switch, no new meshes; materials must read by shape, not color alone. Gravel and concrete follow in `074`.
- [ ] **`027` — World Marking & Placeable Work Lamps** (`03`, `04`): placeable, repositionable, reusable lamps that survive digging — the only underground light; every later deep-zone task depends on them. Free tool markings (arrow, home, return-here). No map, ever.
- [ ] **`026` — Sticky C4 Charges: Tech** (`04`): valid/invalid placement preview, sticky landing, remote detonation, predictable blast volume and matching cleanup on the dig pipeline from `007`. Buried finds, the unique and lamps all survive a blast. Pricing lands with `001`; a charge's time-saved value is validated in `077`.
- [ ] **`015` — Tool-Mounted Silent Visual Detector** (`05`): directional reticle cue for distance and broad direction to the nearest uncollected signaling find; target locking and release priority. Test target is `086`'s unique against the always-silent commons; `012` adds the rest. No value, no identity, no tool model. Oversized set pieces and ending parts also signal once they exist.
- [ ] **`024` — Depth-Aware Return Warning** (`02`, `06`): the warning currently uses charge fractions; estimate return energy from depth and ascent route (safe/risky/critical). The estimator consumes the current jetpack tier, not hardcoded costs; keep HUD additions minimal — `032` consolidates the HUD later.
- [ ] **`012` — Four Remaining Slice Finds** (`05`, `14`): the washing machine, hand drill, gearbox, and mammoth bone extend `086`'s discovery foundation, each with a pinned tier and recovery policy (distinctives sell; the computer's unique role is settled) so `011` and detector eligibility agree. Wide-cut and C4 recognition tests run here against live content.
- [ ] **`001` — Evolving Motorized Tool Rig & 4-Tier Progression** (`04`, `06`): after the systems above. Visible motor-assisted machine with bolt-on attachments, and `ShovelState`/`EquipmentProgression` from 6 levels to 4 tiers. The spec carries the remap of `007`/`024` balance to the new ladder and the Detector-track decision (implement alongside `011` content, or cut the track from concept 06). No tier invalidates earlier investment or digs slower in ground the player already digs (tool-tier regression) — the forced-second-tool reset is the reference corpus's worst progression failure.

### Phase 1: Ground, Materials & Zones

- [ ] **`074` — Gravel & Diggable Concrete Materials** (`03`, `04`): the two response groups `007` deferred; unblocks `006` zone ground, `008` shader work, `010`'s concrete plug and `028` audio. Same voxel path, no new meshes.
- [ ] **`008` — Multi-Material Chunk Meshing & Triplanar Shader** (`03`, `09`): pass material weights into vertex data/shaders for distinct triplanar textures per material. After `074` so all five response groups land once.
- [ ] **`006` — 4 Geological Depth Zones** (`03`): partition depth proportionally: recent fill, old sediment, deep clay/stone, ancient constructed. After `074`+`008` (zone palettes need rendering); requires `027` lamps. Exact metre splits stay in concept.
- [ ] **`009` — Seam Cleaving Signature Action** (`03`, `14`): broad cuts along density/material seams trigger crack → slab shift → fracture. Mechanically testable against the existing dense rock layer; the several-interesting-finds payoff is accepted only after `013` density exists.
- [ ] **`010` — Hard Pockets & Obstructions** (`03`): start with one concrete plug (needs `074`) the current tool can beat; later 5–8 authored obstacles, each solvable by upgrades, C4 or routing — verify all three solutions.

### Phase 2: Discoveries, Clusters & Salvage

- [ ] **`011` — Discovery Catalog & Quotas, Five Categories** (`05`): commons, distinctives, uniques, oversized salvage and ending parts across the zones — the full concept 05 §1 table, including per-object detector eligibility. The five slice objects are `012`'s, not this roster.
- [ ] **`013` — Buried Connections ("Follow the Thing") & Clusters** (`03`, `05`): physical connectors (cables, chains, pipes, tiles) leading to coherent buried scenes; first proof is one scene. Completes `009`'s multi-find acceptance.
- [ ] **`014` — Crackable Buried Containers** (`05`): suitcases/toolboxes cracked open in the world with the machine, revealing nested discoveries.
- [ ] **`076` — Large-Discovery Local Extraction & Bladder Assist** (`05`, `14`): the 3–5-per-run majors (car, appliance pile, machinery section): local release payoff, bladder unsticking, automatic surface transfer beneath ceilings, permanent-landmark cases. Concept 14 wants this tested early and separately from the winch.
- [ ] **`075` — Oversized Salvage Set Pieces & Cash-in** (`05`, `06`, `07`): author and seed `011`'s signaling oversized targets; reuse `002`'s marking/rope/pad system, add once-only surface-computer payout and records for `018`. Test larger load clearance and repeated recoveries without replacing the computer unique's unsellable destination. Includes the former `017` cash-in scope.
- [ ] **`077` — Generation & Economy Pacing Validation** (`02`, `03`, `06`): reject candidate layouts that violate the pacing rules — noteworthy find within the first ten minutes, bounded dry spells, one major per zone, majors never clumped, novelty to the bottom. Also validate purchase cadence, the late economy: the final meaningful purchase lands near the end of the run, and money never dies halfway through (the maxed-out-halfway, cash-nowhere-to-spend failure recurs across the reference corpora) — and C4 charge value (time saved vs price, invalid placements never consuming a charge). Acceptance gate for the whole phase.

### Phase 3: Surface Yard & Display

- [ ] **`078` — String-Table Text Foundation** (`01`): Localization string tables for every current and upcoming gameplay/UI string, so `016`–`019`, `035` and `044` never hardcode text.
- [ ] **`018` — Trophy Display Stands & Salvage Records** (`07`): physical stands for manually socketed uniques plus salvage miniatures/photos, including capture of the actual find and yard.
- [ ] **`019` — Unique Find Lore Cards & Yard Inspection** (`07`, `05`): name, depth found, 1-sentence deadpan lore card; rereadable anytime.
- [ ] **`020` — Authored Yard Props** (`07`): pickup truck, utility trailer, generator, fuel hose, floodlights within 10s of the shaft.
- [ ] **`021` — Yard Cosmetic Milestones & Sinks** (`07`, `06`): optional worksite evolutions (workbench shelter, display tarp, tool skins, lamps).

### Phase 4: Movement & Battery

- [ ] **`022` — Jetpack Hover Hold & Speed Progression** (`04`, `06`): upgrade-driven ascent speed, fuel efficiency, hover-hold assist in narrow shafts. After `024`, so its drain numbers stop moving.
- [ ] **`023` — Crouch Footprint Narrowing** (`04`): narrower bite footprint while crouched for delicate carving around silhouettes.
- [ ] **`025` — Rescue Keeps Loot + Debt Recovery** (`04`, `06`): recovery keeps all finds, charges a depth-scaled fee, and applies interest-free debt when broke; ordinary falls stay harmless. Detail-tier polish after the core loop — the debt's baseline-fuel rationale is gated on the §13 free-versus-paid recharge decision.

### Phase 5: UI, Controls & Persistence

- [ ] **`079` — Title Screen, New Game & Slot Flow** (`08`): title screen with New Game, continue and slot selection plus a clear overwrite warning — the destination `033`'s Exit-to-Title needs and the surface `037` plugs into.
- [ ] **`032` — Minimal Diegetic HUD** (`08`): refactor `GameHudView` to depth meter, bag gauge, battery bar, return warning, clean reticle — consolidating what `015`/`024` bolted on.
- [ ] **`033` — Pause Menu Navigation** (`08`): Resume, Save & Load, Settings, Exit to Title (needs `079`); ESC/B closes reliably without trapping input.
- [ ] **`034` — Gamepad Binding Support** (`08`, `10`): controller binding in `InputPreferences`, UI Toolkit navigation, automatic glyph swapping.
- [ ] **`035` — Directional Sound Captions** (`10`): captions for hearing accessibility; rides the audio phase's content.
- [ ] **`036` — Photo Mode** (`08`): pause-only — hide HUD, FOV, filters, watermark. No free camera (concept 08 §6): the player composes from their own view.
- [ ] **`037` — Multi-Slot Save Profiles** (`08`): extend `WorldSaveController` to profile slots; persist voxels, trophies, inventory, player state. Decide here whether excavation-history journaling exists for `043`'s retrospective, or commit to the before-and-after fallback.
- [ ] **`080` — Accessibility Baseline** (`10`): FOV default 90° (60–110° range), comfort preset, colorblind palettes for materials and detector cues, detector audio-ping/high-contrast options, shape+label redundancy checks, and the optional first-launch comfort preview with its dark-areas note.

### Phase 6: Audio & Feel Polish (parked until core play works)

- [ ] **`028` — Material-Specific Digging Audio Loops** (`09`): sand hiss, clay thump, gravel rattle, rock crack, concrete screech; spec includes the audio foundation (mixer, settings, feedback channels). No music.
- [ ] **`029` — Motor Whine & Strain Audio** (`09`): engine pitch by tool tier; strain in dense material; puff release on cut completion.
- [ ] **`030` — Cavern Reverb & Depth Low-Pass** (`03`, `09`): depth-based low-pass filtering and reverb deepening with descent.
- [ ] **`081` — Zone Ambience Layers** (`09`): wind/distant water → drips/settling rock → near-silent ancient hum, crossfading with depth.
- [ ] **`031` — Material Debris Particles & Cut Release Juice** (`09`): directional crumbs and dust puffs on stroke completion; subtle settling feedback.

### Phase 7: Mystery Climax, Ending & Sandbox

- [ ] **`038` — Anachronistic Mystery Trail Oddities** (`01`, `11`): subtle oddities with visible impossibility establishing curiosity.
- [ ] **`039` — Zone 4 Ancient Constructed Structure** (`03`, `11`): anomalous architecture at the reservoir floor, clearly unlike bedrock.
- [ ] **`040` — Ancient Material Contact Signature** (`09`, `11`): clean surgical cuts, glass-like resonance, too-neat dust; no threat cues.
- [ ] **`041` — Finale Components & Assembly Sockets** (`11`): 3–4 components with recoverable physical leads; owned parts auto-insert into sockets. Components signal — covered by `015`'s eligibility.
- [ ] **`042` — Final Object Presentation Cutscene** (`01`, `11`): reveal the ancient junk fabrication machine; normal tools, no genre switch.
- [ ] **`043` — Ending Retrospective** (`11`, `13`): presentation still open in §13 (timelapse vs before-and-after); requires the `037` history decision. If timelapse, journaling must run from the first dig.
- [ ] **`044` — Post-Ending Sandbox & Media Wall** (`07`, `11`): unlocked sandbox excavation; newspaper clippings and quiet TV/radio props referencing this save.
- [ ] **`045` — Steam Achievements** (`12`): wire 5–10 fair achievements; re-verify after `046`/`047` hardening. No meta-achievement for completing all achievements (single point of failure); offline earning queues locally and syncs.

### Phase 8: Release Pipeline (before public release)

- [ ] **`082` — Performance & Long-Session Validation** (`14`): stable frame pacing while digging, no cold-start hitch on first dig, no per-launch shader compilation, no progressive decay across a long session, and save-write latency inside the frame-impact budget. Performance failure is the dominant technical complaint across the reference corpora.
- [ ] **`046` — Native IL2CPP Windows Pipeline**: IL2CPP with MSVC; `link.xml` against code-stripping of UI Toolkit, save and Steamworks types.
- [ ] **`047` — Release Obfuscation & Binary Hardening**: symbol stripping, class/method renaming, string encryption, metadata obfuscation.
- [ ] **`083` — Clean-Save Verification & Assist Runs** (`12`, `14`): on the shipping build — clean-save 100% with every required find spawn-obtainable in each seed (never-spawning collectibles locked 100% in a reference game), muted + toggle-dig + controller-only full run, interrupted-session save integrity, and an economy exploit audit (duplicated value, repeated credit, purchase bypass).
- [ ] **`084` — Steam Cloud Saves** (`08`): cloud-sync the existing save format without format changes; verify slot/profile behavior. Requested across every reference corpus.
- [ ] **`085` — Store Page & Content Disclosure** (`01`): honest copy — digging, dark areas, no horror, one difficulty, length, price — plus tags and media. The reference corpora's harshest backlashes came from undisclosed content; the honesty promise is a release requirement.

### Parked (pending concept §13 decisions)

- [ ] **`004` — Yard Fuel Dispenser** (`06`, `07`): upgrade `SurfaceRecharge` into a dedicated dispenser; transparent full/partial pricing; preserve fuel on tank-capacity upgrade. After the free-versus-paid recharge decision — Keep Digging's exponential recharge-cost backlash (upgrading the battery felt like punishment) is the cautionary evidence.
- [ ] **`016` — Hover Price Tag on Exposed Finds** (`05`, `13`): subtle fixed sale price when the reticle hovers a collectible sellable find. After the price-on-hover decision.
