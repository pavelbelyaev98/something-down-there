# Roadmap & Tasks

## Status

- **Active Task:** `025` — rescue keeps finds and charges a fee or debt.
- **Build:** `builds/windows/SomethingDownThere.exe` (MainGame; current-format saves only).
- **Direction:** Change the dig that already exists before any new machine model. `001` waits. Core functionality first; audio, ambience and feel polish are parked in their own late phases until the core loop works.
- **Completed history:** `docs/tasks/completed/` — one spec per finished task; each header carries the final-state summary.

---

## Priority Queue

> **Workflow:** Starting a task: create a thorough spec at `docs/tasks/<ID>-<slug>.md` (Objective, live code analysis, architecture changes, edge cases, Acceptance Criteria). Completing a task: move the spec to `docs/tasks/completed/`, rewrite its header to a 1–2 sentence final-state summary, delete the queue entry here (collapsing a finished phase to one line), and update `docs/baseline.md` if baseline systems changed. A pending entry keeps its title, concept refs, and goal with constraints. Do not shorten it to a bare title. A spec header must not state a count, price, or layout the game no longer uses.
> **Note:** Tasks labelled _Upgrade/Extend/Refactor_ modify existing working systems — inspect `docs/baseline.md` and `docs/architecture.md` first, never rebuild from scratch.

### Phase 0: Immediate Feel Pass

Complete (`048`–`073`) — specs in `docs/tasks/completed/`.

### Next: current dig, no new models

- [ ] **`025` — Rescue Keeps Loot + Debt Recovery** (`04`, `06`): `RescueController` currently removes carried finds. Recovery keeps them, charges a depth-scaled fee, and applies interest-free debt when broke; ordinary falls stay harmless. Gate the debt's baseline-fuel rationale on the §13 free-versus-paid recharge decision.
- [ ] **`007` — Material IDs & Tool Auto-Adaptation** (`03`, `04`): assign material IDs on the voxels that already exist. Soil, clay, and rock differ in bite speed, resistance, and sound *hooks* (actual audio arrives with `028`). No mode switch, no new meshes; materials must read by shape, not color alone. Gravel and concrete follow in `074`.
- [ ] **`015` — Tool-Mounted Silent Visual Detector** (`05`): directional reticle cue for distance and broad direction to the nearest uncollected signaling find; target locking and release priority. Ships with one flagged placeholder find as its test target (`012` proves the real objects later); no value, no identity, no tool model. Oversized set pieces and ending parts also signal once they exist.
- [ ] **`024` — Depth-Aware Return Warning** (`02`, `06`): the warning currently uses charge fractions; estimate return energy from depth and ascent route (safe/risky/critical). The estimator consumes the current jetpack tier, not hardcoded costs; keep HUD additions minimal — `032` consolidates the HUD later.
- [ ] **`012` — Five Slice Finds** (`05`, `14`): exposure already exists. Prove one placeholder shape through reveal and the detector before building the washing machine, drill, gearbox, mammoth bone, and gramophone. Pin each object's tier in the spec (leaning: gramophone as a display unique, the rest sellable distinctives) so `011` and detector eligibility agree.
- [ ] **`001` — Evolving Motorized Tool Rig & 4-Tier Progression** (`04`, `06`): after the systems above. Visible motor-assisted machine with bolt-on attachments, and `ShovelState`/`EquipmentProgression` from 6 levels to 4 tiers. The spec carries the remap of `007`/`024` balance to the new ladder and the Detector-track decision (implement alongside `011` content, or cut the track from concept 06).

### Phase 1: Ground, Materials & Zones

- [ ] **`074` — Gravel & Diggable Concrete Materials** (`03`, `04`): the two response groups `007` deferred; unblocks `006` zone ground, `008` shader work, `010`'s concrete plug and `028` audio. Same voxel path, no new meshes.
- [ ] **`008` — Multi-Material Chunk Meshing & Triplanar Shader** (`03`, `09`): pass material weights into vertex data/shaders for distinct triplanar textures per material. After `074` so all five response groups land once.
- [ ] **`027` — World Marking & Placeable Work Lamps** (`03`, `04`): free tool markings (arrow, home, return-here); reusable, repositionable lamps. Moved before `006`: zones 3–4 are true darkness and `010`'s pockets must be legible before commitment. No map, ever.
- [ ] **`006` — 4 Geological Depth Zones** (`03`): partition depth proportionally: recent fill, old sediment, deep clay/stone, ancient constructed. After `074`+`008` (zone palettes need rendering) and `027` (deep digging needs light). Exact metre splits stay in concept.
- [ ] **`009` — Seam Cleaving Signature Action** (`03`, `14`): broad cuts along density/material seams trigger crack → slab shift → fracture. Mechanically testable against the existing dense rock layer; the several-interesting-finds payoff is accepted only after `013` density exists.
- [ ] **`010` — Hard Pockets & Obstructions** (`03`): start with one concrete plug (needs `074`) the current tool can beat; later 5–8 authored obstacles with multiple solutions (upgrades, C4, routing). Do not wait for C4.

### Phase 2: Discoveries, Clusters & Salvage

- [ ] **`011` — Discovery Catalog & Quotas, Five Categories** (`05`): commons, distinctives, uniques, oversized salvage and ending parts across the zones — the full concept 05 §1 table, including per-object detector eligibility. The five slice objects are `012`'s, not this roster.
- [ ] **`013` — Buried Connections ("Follow the Thing") & Clusters** (`03`, `05`): physical connectors (cables, chains, pipes, tiles) leading to coherent buried scenes; first proof is one scene. Completes `009`'s multi-find acceptance.
- [ ] **`014` — Crackable Buried Containers** (`05`): suitcases/toolboxes cracked open in the world with the machine, revealing nested discoveries.
- [ ] **`076` — Large-Discovery Local Extraction & Bladder Assist** (`05`, `14`): the 3–5-per-run majors (car, appliance pile, machinery section): local release payoff, bladder unsticking, automatic surface transfer beneath ceilings, permanent-landmark cases. Concept 14 wants this tested early and separately from the winch.
- [ ] **`075` — Oversized Salvage Set Pieces** (`05`): author and seed the handful of oversized winch targets the catalog defines in `011`, flagged for signaling and winch recovery.
- [ ] **`002` — Salvage Winch & Pad** (`06`, `07`): rim winch hauls flagged set pieces up the shaft, carving dirt bottlenecks; payout at the surface computer; records feed `018`. After `075` — the winch has nothing to haul until then. (Absorbs the removed `017`.)
- [ ] **`077` — Generation Pacing Validation** (`02`, `03`): reject candidate layouts that violate the pacing rules — noteworthy find within the first ten minutes, bounded dry spells, one major per zone, majors never clumped, novelty to the bottom. Acceptance gate for the whole phase.

### Phase 3: Surface Yard & Display

- [ ] **`078` — String-Table Text Foundation** (`01`): Localization string tables for every current and upcoming gameplay/UI string, so `016`–`019`, `035` and `044` never hardcode text.
- [ ] **`018` — Trophy Display Stands & Salvage Records** (`07`): physical stands for manually socketed uniques plus salvage miniatures/photos, including capture of the actual find and yard.
- [ ] **`019` — Unique Find Lore Cards & Yard Inspection** (`07`, `05`): name, depth found, 1-sentence deadpan lore card; rereadable anytime.
- [ ] **`020` — Authored Yard Props** (`07`): pickup truck, utility trailer, generator, fuel hose, floodlights within 10s of the shaft.
- [ ] **`021` — Yard Cosmetic Milestones & Sinks** (`07`, `06`): optional worksite evolutions (workbench shelter, display tarp, tool skins, lamps).

### Phase 4: Movement, Battery & Explosives

- [ ] **`022` — Jetpack Hover Hold & Speed Progression** (`04`, `06`): upgrade-driven ascent speed, fuel efficiency, hover-hold assist in narrow shafts. After `024`, so its drain numbers stop moving.
- [ ] **`023` — Crouch Footprint Narrowing** (`04`): narrower bite footprint while crouched for delicate carving around silhouettes.
- [ ] **`026` — Consumable Sticky C4 Charges** (`04`): placed charges with remote detonation; predictable volume; buried finds survive intact.

### Phase 5: UI, Controls & Persistence

- [ ] **`079` — Title Screen, New Game & Slot Flow** (`08`): title screen with New Game, continue and slot selection plus a clear overwrite warning — the destination `033`'s Exit-to-Title needs and the surface `037` plugs into.
- [ ] **`032` — Minimal Diegetic HUD** (`08`): refactor `GameHudView` to depth meter, bag gauge, battery bar, return warning, clean reticle — consolidating what `015`/`024` bolted on.
- [ ] **`033` — Pause Menu Navigation** (`08`): Resume, Save & Load, Settings, Exit to Title (needs `079`); ESC/B closes reliably without trapping input.
- [ ] **`034` — Gamepad Binding Support** (`08`, `10`): controller binding in `InputPreferences`, UI Toolkit navigation, automatic glyph swapping.
- [ ] **`035` — Directional Sound Captions** (`10`): captions for hearing accessibility; rides the audio phase's content.
- [ ] **`036` — Photo Mode** (`08`): pause-only — hide HUD, FOV, filters, watermark. No free camera (concept 08 §6): the player composes from their own view.
- [ ] **`037` — Multi-Slot Save Profiles** (`08`): extend `WorldSaveController` to profile slots; persist voxels, trophies, inventory, player state. Decide here whether excavation-history journaling exists for `043`'s retrospective, or commit to the before-and-after fallback.
- [ ] **`080` — Accessibility Baseline** (`10`): FOV default 90° (60–110° range), comfort preset, colorblind palettes for materials and detector cues, detector audio-ping/high-contrast options, shape+label redundancy checks.

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
- [ ] **`045` — Steam Achievements** (`12`): wire 5–10 fair achievements; re-verify after `046`/`047` hardening.

### Phase 8: Release Pipeline (before public release)

- [ ] **`046` — Native IL2CPP Windows Pipeline**: IL2CPP with MSVC; `link.xml` against code-stripping of UI Toolkit, save and Steamworks types.
- [ ] **`047` — Release Obfuscation & Binary Hardening**: symbol stripping, class/method renaming, string encryption, metadata obfuscation.

### Parked (pending concept §13 decisions)

- [ ] **`004` — Yard Fuel Dispenser** (`06`, `07`): upgrade `SurfaceRecharge` into a dedicated dispenser; transparent full/partial pricing; preserve fuel on tank-capacity upgrade. After the free-versus-paid recharge decision.
- [ ] **`016` — Hover Price Tag on Exposed Finds** (`05`, `13`): subtle fixed sale price when the reticle hovers a collectible sellable find. After the price-on-hover decision.
