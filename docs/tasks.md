# Roadmap & Tasks

## Status

- **Active Task:** `001` — evolving motorized tool and progression.
- **Build:** `builds/windows/SomethingDownThere.exe` (MainGame; current-format saves only).
- **Direction:** Vertical slice per `docs/concept/`: the transformative machine, the shared sell/upgrade computer & salvage winch, and 100m+ reservoir depth.
- **Completed history:** `docs/tasks/completed/` — one spec per finished task; each header carries the final-state summary.

---

## Priority Queue

> **Workflow:** Starting a task: create a thorough spec at `docs/tasks/<ID>-<slug>.md` (Objective, live code analysis, architecture changes, edge cases, Acceptance Criteria). Completing a task: move the spec to `docs/tasks/completed/`, rewrite its header to a 1–2 sentence final-state summary, delete the queue entry here (collapsing a finished phase to one line), and update `docs/baseline.md` if baseline systems changed.
> **Note:** Tasks labelled _Upgrade/Extend/Refactor_ modify existing working systems — inspect `docs/baseline.md` and `docs/architecture.md` first, never rebuild from scratch.

### Phase 0: Immediate Feel Pass

Complete (`048`–`073`) — specs in `docs/tasks/completed/`.

### Phase 1: Machine Upgrades & Surface Shop

- [ ] **`001` — Evolving Motorized Tool Rig & 4-Tier Progression** (`04`, `06`): motor-assisted machine with bolt-on attachments; refactor `ShovelState`/`EquipmentProgression` from 6 levels to 4 tiers. No visible tool exists; fresh design.
- [ ] **`002` — Salvage Winch & Pad** (`06`, `07`): haul unburied oversized set pieces up the shaft, carving dirt bottlenecks; payout at the surface computer; no hopper/workbench (superseded by 073).
- [ ] **`004` — Yard Fuel Dispenser** (`06`, `07`): upgrade `SurfaceRecharge` into a dedicated dispenser; transparent full/partial pricing; preserve fuel on tank-capacity upgrade.

### Phase 2: Reservoir Depth, Zones & Ground Feel

- [ ] **`006` — 4 Geological Depth Zones** (`03`): partition depth proportionally: recent fill 0–25m, old sediment 25–50m, deep clay/stone 50–75m, ancient constructed >75m.
- [ ] **`007` — Material IDs & Tool Auto-Adaptation** (`03`, `04`): assign material IDs to voxels (soil, clay, gravel, rock, diggable concrete); tool adapts bite speed, sound, resistance automatically.
- [ ] **`008` — Multi-Material Chunk Meshing & Triplanar Shader** (`03`, `09`): pass material weights into vertex data/shaders for distinct triplanar textures per material.
- [ ] **`009` — Seam Cleaving Signature Action** (`03`, `14`): broad cuts along density/material seams trigger crack → slab shift → fracture, exposing multiple finds at once.
- [ ] **`010` — Hard Pockets & Obstructions** (`03`): 5–8 authored obstacles (concrete plugs, boulder clusters) with multiple solutions (upgrades, C4, routing).

### Phase 3: Discoveries, Clusters & Detection

- [ ] **`011` — Discovery Catalog & Quotas in 3 Tiers** (`05`): commons (sellable junk/ore), distinctives (repeatable high-value), uniques (unsellable display) across the zones.
- [ ] **`012` — Deliberate Exposure for All Finds + 5 Slice Finds** (`05`, `14`): every find deliberately uncovered before collection; washing machine, drill, gearbox, mammoth bone, gramophone.
- [ ] **`013` — Buried Connections ("Follow the Thing") & Clusters** (`03`, `05`): physical connectors (cables, chains, pipes, tiles) leading to coherent buried scenes.
- [ ] **`014` — Crackable Buried Containers** (`05`): suitcases/toolboxes cracked open in the world with the machine, revealing nested discoveries.
- [ ] **`015` — Tool-Mounted Silent Visual Detector** (`05`): directional visual pulse with target locking; exposed items yield; accessibility audio toggle.
- [ ] **`016` — Hover Price Tag on Exposed Finds** (`05`, `13`): subtle fixed sale price when the reticle hovers a collectible sellable find.
- [ ] **`017` — Whole-Object Salvage Winch Extraction** (`05`): flag oversized finds; rim winch hauls them up carving bottlenecks; refine RMB lift/drop and LMB throw.

### Phase 4: Surface Yard & Trophy Display

- [ ] **`018` — Trophy Display Stands & Salvage Records** (`07`): physical stands for manually socketed uniques and salvage miniatures/photos.
- [ ] **`019` — Unique Find Lore Cards & Yard Inspection** (`07`, `05`): name, depth found, 1-sentence deadpan lore card; rereadable anytime.
- [ ] **`020` — Authored Yard Props** (`07`): pickup truck, utility trailer, generator, fuel hose, floodlights within 10s of the shaft.
- [ ] **`021` — Yard Cosmetic Milestones & Sinks** (`07`, `06`): optional worksite evolutions (workbench shelter, display tarp, tool skins, lamps).

### Phase 5: Movement, Battery & Explosives

- [ ] **`022` — Jetpack Hover Hold & Speed Progression** (`04`, `06`): upgrade-driven ascent speed, fuel efficiency, hover-hold assist in narrow shafts.
- [ ] **`023` — Crouch Footprint Narrowing** (`04`): narrower bite footprint while crouched for delicate carving around silhouettes.
- [ ] **`024` — Depth-Aware Return Warning** (`02`, `06`): estimate return energy from depth and ascent route; rebalance dig vs. flight battery drain.
- [ ] **`025` — Rescue Keeps Loot + Debt Recovery** (`04`, `06`): refactor `RescueController`; keep finds, depth-scaled fee, interest-free debt when broke.
- [ ] **`026` — Consumable Sticky C4 Charges** (`04`): placed charges with remote detonation; predictable volume; buried finds survive intact.
- [ ] **`027` — World Marking & Placeable Work Lamps** (`03`, `04`): free tool markings (arrow, home, return-here); reusable lamps; no map, ever.

### Phase 6: Audio, Sensory Feel & Feedback

- [ ] **`028` — Material-Specific Digging Audio Loops** (`09`): sand hiss, clay thump, gravel rattle, rock crack, concrete screech. No music.
- [ ] **`029` — Motor Whine & Strain Audio** (`09`): engine pitch by tool tier; strain in dense material; puff release on cut completion.
- [ ] **`030` — Cavern Reverb & Depth Low-Pass** (`03`, `09`): depth-based low-pass filtering and reverb deepening with descent.
- [ ] **`031` — Material Debris Particles & Cut Release Juice** (`09`): directional crumbs and dust puffs on stroke completion; subtle settling feedback.

### Phase 7: UI, Controls & Persistence

- [ ] **`032` — Minimal Diegetic HUD** (`08`): refactor `GameHudView` to depth meter, bag gauge, battery bar, return warning, clean reticle.
- [ ] **`033` — Pause Menu Navigation** (`08`): Resume, Save & Load, Settings, Exit to Title; ESC/B closes reliably without trapping input.
- [ ] **`034` — Gamepad Binding Support** (`08`, `10`): controller binding in `InputPreferences`, UI Toolkit navigation, automatic glyph swapping.
- [ ] **`035` — Directional Sound Captions** (`10`): captions for hearing accessibility.
- [ ] **`036` — Pause Free-Look Photo Mode** (`08`): free-look camera from pause to inspect finds and photograph the carved hole.
- [ ] **`037` — Multi-Slot Save Profiles** (`08`): extend `WorldSaveController` to profile slots; persist voxels, trophies, inventory, player state.

### Phase 8: Mystery Climax, Ending & Sandbox

- [ ] **`038` — Anachronistic Mystery Trail Oddities** (`01`, `11`): subtle oddities with visible impossibility establishing curiosity.
- [ ] **`039` — Zone 4 Ancient Constructed Structure** (`03`, `11`): anomalous architecture at the reservoir floor, clearly unlike bedrock.
- [ ] **`040` — Ancient Material Contact Signature** (`09`, `11`): clean surgical cuts, glass-like resonance, too-neat dust; no threat cues.
- [ ] **`041` — Finale Components & Assembly Sockets** (`11`): 3–4 components with recoverable physical leads; owned parts auto-insert into sockets.
- [ ] **`042` — Final Object Presentation Cutscene** (`01`, `11`): reveal the ancient junk fabrication machine; normal tools, no genre switch.
- [ ] **`043` — Ending Excavation Timelapse** (`11`, `13`): retrospective replay of the hole's evolution from untouched start to bottom.
- [ ] **`044` — Post-Ending Sandbox & Media Wall** (`07`, `11`): unlocked sandbox excavation; newspaper clippings and quiet TV/radio props.
- [ ] **`045` — Steam Achievements** (`12`): wire 5–10 fair achievements to zones, machine, trophies, mystery.

### Phase 9: Release Pipeline & Build Protection (Before Public Release)

- [ ] **`046` — Native IL2CPP Windows Pipeline**: IL2CPP with MSVC; `link.xml` against code-stripping of UI Toolkit and save types.
- [ ] **`047` — Release Obfuscation & Binary Hardening**: symbol stripping, class/method renaming, string encryption, metadata obfuscation.
