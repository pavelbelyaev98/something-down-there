# Roadmap & Tasks

## Status

- **Active Task:** None — `064` rebuilt the surroundings as a mountain-ringed valley; next pending is `001`.
- **Build:** `builds/windows/SomethingDownThere.exe` (MainGame; simple site, working menus and gameplay)
- **Direction:** Implementing the vertical slice per `docs/concept/`. Starting with the transformative machine, the janky Sell-All machine & salvage winch, the visual workbench, and 100m+ reservoir depth.

---

## Priority Queue

> **Workflow:** When starting a task, create a thorough spec at `docs/tasks/<ID>-<slug>.md` (Objective, live code analysis, architecture changes, edge cases, Acceptance Criteria). On completion: move spec to `docs/tasks/completed/`, update `docs/baseline.md` if baseline systems changed, and mark `[x]` here.
> **Note:** Tasks labelled _Upgrade/Extend/Refactor_ modify existing working systems — inspect `docs/baseline.md` and `docs/architecture.md` first, never rebuild from scratch.

### Phase 0: Immediate Feel Pass

- [x] **`064` — Demo-Matched Valley & Natural Boundary:** reshaped the surroundings into a radial alpine bowl ringed by two rings of vendor peaks, with demo grass/flower detail, a slope-driven cliff pass, stream, lake and notched waterfall; the invisible perimeter is gone and containment is terrain steepness. (`docs/tasks/completed/064-demo-matched-valley-and-natural-boundary.md`)
- [x] **`063` — Drained Reservoir Environment:** authored the reservoir surroundings from the approved pack — sediment dig ground, grassy camp terrace, rocky forested banks, basin and distant peaks; arena panels hidden, boundaries unchanged. (`docs/tasks/completed/063-drained-reservoir-environment.md`)
- [x] **`062` — Restore Simple Site:** restored the pre-environment gameplay scene and removed generated/copied surroundings; the user will design the environment manually. (`docs/tasks/completed/062-restore-simple-site.md`)
- [x] **`061` — Guided Reservoir Assembly:** rejected; removed in task 062 with the other environment experiments. (`docs/tasks/completed/061-guided-reservoir-assembly.md`)
- [x] **`060` — Restore Game Entry:** restored the normal playable build and removed preview replacement. (`docs/tasks/completed/060-restore-game-entry.md`)
- [x] **`059` — Direct Demo Lake Preview:** rejected; preview and tools removed in task 062. (`docs/tasks/completed/059-demo-lake-preview.md`)
- [x] **`058` — Reservoir Ground Base:** rejected; generated terrain and tools removed in task 062. (`docs/tasks/completed/058-reservoir-ground-base.md`)
- [x] **`057` — Drained Reservoir Surroundings:** rejected; scenery and overrides removed in task 062.
- [x] **`056` — Pure Nature Integration:** configured the purchased pack, replaced prototype grass and documented asset/Git workflow. (`docs/tasks/completed/056-pure-nature-grass.md`)
- [x] **`055` — Graphics Settings:** expose persistent rendering and shadow performance controls in the existing menu. (`docs/tasks/completed/055-graphics-settings.md`)
- [x] **`054` — Render Resolution Defaults:** native rendering by default and supported high-resolution display choices. (`docs/tasks/completed/054-render-resolution-defaults.md`)
- [x] **`053` — Dense World Performance:** suppress hidden find rendering, stop polling anchored physics and cap startup frames. (`docs/tasks/completed/053-dense-world-performance.md`)
- [x] **`052` — Fresh Find Density:** dense fresh encounters through the first few metres below the accepted turf layer, measured independently of fallen rocks. (`docs/tasks/completed/052-fresh-find-density.md`)
- [x] **`051` — Deeper Finds and Noon Light:** preserve the accepted shallow layer, enrich deeper excavation and brighten the surface with vibrant midday presentation. (`docs/tasks/completed/051-deeper-finds-and-noon-light.md`)
- [x] **`050` — Earlier Finds and Gentler Daylight:** denser finds within the first scrapes and a slower fade into tunnel darkness. (`docs/tasks/completed/050-shallow-finds-and-gentler-daylight.md`)
- [x] **`049` — Dark Tunnels:** removed the permanent underground brightness floor and attenuated sunlight through the excavation daylight field so deep/lateral passages become near-black. (`docs/tasks/completed/049-dark-tunnels.md`)
- [x] **`048` — Buried Find Depth Density** (`docs/concept/05_DISCOVERIES.md`): Raise the new-game population from 1,024 to 2,042 finds so no 1 m layer below 2 m falls under ~0.05 finds/m³, keep the 312-find entry burst, and move placement onto a spatial grid so generation stays instant. (`docs/tasks/completed/048-buried-find-depth-density.md`)

### Phase 1: Machine Upgrades & Surface Shop

- [ ] **`001` — Build the Evolving Motorized Tool Rig & 4-Tier Progression** (NEW) (`docs/concept/04_TOOL_AND_MOVEMENT.md`, `06_PROGRESSION_AND_ECONOMY.md`): The normal game currently has no visible tool (only the admin-only experiment). Build the improvised motor-assisted first-person machine with bolt-on attachments, and refactor `ShovelState`/`EquipmentProgression` from 6 levels to 4 transformative tiers (bite volume, speed, cutting power).
- [ ] **`002` — Upgrade the Sell Station into a Janky Sell-All Machine & Add the Salvage Winch** (`docs/concept/06_PROGRESSION_AND_ECONOMY.md`, `07_SURFACE_HUB_AND_DISPLAY.md`): Upgrade the existing `SellStation` (which already supports Sell All) into a physical hopper with a lever, grinding audio, digital readout, and add the new surface winch that hauls unburied oversized set pieces up the shaft (dynamically carving dirt bottlenecks).
- [x] **`003` — One-Click Workbench & Sell Machine Table** (`docs/concept/06_PROGRESSION_AND_ECONOMY.md`, `07_SURFACE_HUB_AND_DISPLAY.md`): fixed-size parts-board table — upgrades and services in separate columns (upgrades wider), rows are decoration and only the price/payout button is clickable and one click buys; no confirm step, no icons, no resizing.
- [ ] **`004` — Upgrade `SurfaceRecharge` into a Dedicated Yard Fuel Dispenser** (`docs/concept/06_PROGRESSION_AND_ECONOMY.md`, `07_SURFACE_HUB_AND_DISPLAY.md`): Refill logic already exists (`UpgradeStation`/`StationTrade`). Build a dedicated surface dispenser with transparent full/partial refill pricing, whole-dollar `$`, and preserving current fuel when tank capacity upgrades.

### Phase 2: Reservoir Depth, Zones & Ground Feel

- [x] **`005` — Extend `TerrainVolume` to 100m+ Depth & Reservoir Boundaries** (`docs/concept/03_WORLD_AND_SITE.md`): Scale the existing 32m volume to 100m+ depth with a contained lateral footprint. (`docs/tasks/completed/005-terrain-depth.md`)
- [ ] **`006` — Partition the Voxel Grid into 4 Geological Depth Zones** (`docs/concept/03_WORLD_AND_SITE.md`): Partition vertical depth proportionally into 4 zones: Zone 1 Recent Fill (0–25m), Zone 2 Old Sediment (25–50m), Zone 3 Deep Clay & Stone (50–75m), Zone 4 Ancient Constructed (>75m).
- [ ] **`007` — Extend `ExcavationGrid` with Material IDs & Tool Auto-Adaptation** (`docs/concept/03_WORLD_AND_SITE.md`, `04_TOOL_AND_MOVEMENT.md`): Assign material IDs to voxels (Soil, Clay, Gravel, Rock, Diggable Concrete). Tool automatically adapts bite speed, sound, and resistance without manual mode switching.
- [ ] **`008` — Update Chunk Meshing & Triplanar Shader for Multi-Material Ground** (`docs/concept/03_WORLD_AND_SITE.md`, `09_FEEL_ART_AND_AUDIO.md`): Chunk meshing passes material weights/indices into vertex data/shaders to render distinct triplanar textures and normal relief for Soil, Clay, Gravel, Rock, and Concrete.
- [ ] **`009` — Implement Seam Cleaving as a Signature Action** (`docs/concept/03_WORLD_AND_SITE.md`, `14_PROTOTYPE_PLAN.md`): Broad cuts along natural density/material seams trigger crack audio → physical slab shift → fracture break to clear bounded sections efficiently, exposing multiple items at once.
- [ ] **`010` — Author Hard Pockets & Obstructions** (`docs/concept/03_WORLD_AND_SITE.md`): Seed 5–8 authored hard obstacles (concrete plugs, boulder clusters) that resist early tools but offer multiple solutions (upgrades, C4, routing).

### Phase 3: Discoveries, Clusters & Detection

- [ ] **`011` — Rebalance `DiscoveryCatalog` & Quotas into 3 Tiers** (`docs/concept/05_DISCOVERIES.md`): Extend the current dense rock/mineral population with authored finds across the geological zones: Commons (sellable junk/ore), Distinctives (repeatable high-value), Uniques (unsellable display finds).
- [ ] **`012` — Extend Deliberate Exposure to All Finds & Add 5 Signature Slice Finds** (`docs/concept/05_DISCOVERIES.md`, `14_PROTOTYPE_PLAN.md`): Exposure already exists at 60%. Extend so every find (including rubbish) must be deliberately uncovered before collection — no vacuum auto-collect. Add the 5 signature slice finds (_washing machine, drill, gearbox, mammoth bone, gramophone_).
- [ ] **`013` — Implement Buried Connections ("Follow the Thing") & Clusters** (`docs/concept/03_WORLD_AND_SITE.md`, `05_DISCOVERIES.md`): Seed physical connectors (heavy cables, rusted chains, matching floor tiles, pipes) leading through the soil to coherent buried scenes, giving lateral exploration an immediate visual reason.
- [ ] **`014` — Implement Crackable Buried Containers** (`docs/concept/05_DISCOVERIES.md`): Add buried containers (suitcases, toolboxes) cracked open with the machine in the world to reveal nested discoveries.
- [ ] **`015` — Implement Tool-Mounted Silent Visual Detector** (`docs/concept/05_DISCOVERIES.md`): Directional visual pulse cue on the machine indicating distance and broad direction to nearest uncollected distinctive/unique. Target locking prevents flickering; fully exposed items yield priority; multi-sensory accessibility audio toggle available.
- [ ] **`016` — Add Subtle Hover Price Tag to Exposed Finds** (`docs/concept/05_DISCOVERIES.md`, `13_OPEN_QUESTIONS.md`): Display subtle fixed sale price tag when reticle hovers over an exposed, collectible sellable find.
- [ ] **`017` — Whole-Object Salvage Surface Winch Extraction & Handling** (`docs/concept/05_DISCOVERIES.md`): Uncovering an oversized object flags it for salvage. At the surface yard, operating the rim winch deploys a heavy cable down the shaft that locks onto the object and hauls it up while carving away dirt bottlenecks, landing on the salvage pad for payout. Refine physical RMB lift/drop and LMB throw.

### Phase 4: Surface Yard & Trophy Display

- [ ] **`018` — Build Surface Trophy Display Stands & Salvage Records** (`docs/concept/07_SURFACE_HUB_AND_DISPLAY.md`): Build physical display stands where players manually socket unique oddities and display miniatures/photos of whole-salvage set pieces.
- [ ] **`019` — Add Unique Find Lore Cards & Yard Inspection** (`docs/concept/07_SURFACE_HUB_AND_DISPLAY.md`, `05_DISCOVERIES.md`): Interacting with placed trophies displays name, depth found, and 1-sentence deadpan lore card with support for rereading anytime.
- [ ] **`020` — Extend the Existing Surface Yard with Authored Props** (`docs/concept/07_SURFACE_HUB_AND_DISPLAY.md`): The yard exists with Sell/Upgrade/Fuel stations. Add authored props: rusty pickup truck, utility trailer, generator, fuel hose, and floodlights within 10s of the shaft.
- [ ] **`021` — Add Surface Yard Cosmetic Milestones & Sinks** (`docs/concept/07_SURFACE_HUB_AND_DISPLAY.md`, `06_PROGRESSION_AND_ECONOMY.md`): Optional visual worksite evolutions (shelter over workbench, weather tarp over display wall, tool skins, decorative lamps).

### Phase 5: Movement, Battery & Explosives

- [ ] **`022` — Extend the Existing Jetpack with Hover Hold & Speed Progression** (`docs/concept/04_TOOL_AND_MOVEMENT.md`, `06_PROGRESSION_AND_ECONOMY.md`): Jetpack thrust/hover already exists. Extend with upgrade-driven ascent speed, fuel efficiency, and hover-hold assist in narrow shafts without wall-bump collision damage.
- [ ] **`023` — Add Footprint Narrowing to the Existing Precision Crouch** (`docs/concept/04_TOOL_AND_MOVEMENT.md`): Crouch at 35% speed already works (`PlayerCrouch`). Add bite-footprint narrowing while crouched for delicate carving around silhouettes.
- [ ] **`024` — Upgrade `ReturnWarning` to a Depth-Aware Estimate** (`docs/concept/02_CORE_LOOP.md`, `06_PROGRESSION_AND_ECONOMY.md`): The warning currently uses charge fractions. Upgrade it to estimate return energy from depth and ascent route, while preserving full player upgrade freedom. Rebalance dig vs. flight battery drain.
- [ ] **`025` — Refactor `RescueController` to Keep Loot & Add Debt Recovery** (`docs/concept/04_TOOL_AND_MOVEMENT.md`, `06_PROGRESSION_AND_ECONOMY.md`): The current controller deletes carried items on rescue, which contradicts the concept. Refactor so recovery keeps all finds, charges a depth-scaled fee, and applies interest-free debt when broke. Ordinary falls stay harmless.
- [ ] **`026` — Implement Consumable Sticky C4 Charges** (`docs/concept/04_TOOL_AND_MOVEMENT.md`): Placed sticky C4 charges with remote detonation. Removes a large predictable voxel volume while ensuring buried finds survive intact.
- [ ] **`027` — Implement World Marking & Placeable Work Lamps** (`docs/concept/03_WORLD_AND_SITE.md`, `04_TOOL_AND_MOVEMENT.md`): Add free world-space markings via the tool (arrow, home, return-here) for lateral branches. Placeable lamps illuminate deep shafts; the vertical daylight shaft remains the natural landmark (no map, ever).

### Phase 6: Audio, Sensory Feel & Feedback

- [ ] **`028` — Add Material-Specific Digging Audio Loops** (`docs/concept/09_FEEL_ART_AND_AUDIO.md`): Material cutting sound loops: sand hiss, clay thump, gravel rattle, rock sharp crack, concrete grinding screech. No music.
- [ ] **`029` — Add Machine Motor Whine & Strain Audio Feedback** (`docs/concept/09_FEEL_ART_AND_AUDIO.md`): Engine pitch responds to tool upgrade level; motor audibly strains when biting dense rock or concrete; puff release sound on cut completion.
- [ ] **`030` — Configure Cavern Acoustic Reverb & Low-Pass Filtering** (`docs/concept/03_WORLD_AND_SITE.md`, `09_FEEL_ART_AND_AUDIO.md`): Depth-based low-pass audio filtering and cavernous acoustic reverb that deepens as the player descends into deep shafts.
- [ ] **`031` — Add Material Debris Particles & Cut Release Juice** (`docs/concept/09_FEEL_ART_AND_AUDIO.md`): Directional soil crumbs and dust puff particle bursts on stroke completion; subtle visual settling feedback on cuts.

### Phase 7: UI, Controls & Persistence

- [ ] **`032` — Refactor `GameHudView` into the Minimal Diegetic HUD** (`docs/concept/08_INTERFACE_AND_CONTROLS.md`): The existing HUD has legacy labels. Refactor to the 5 essentials: real-time Depth meter, Bag gauge with full warning color, Battery bar, adaptive Return Warning estimate, and clean reticle.
- [ ] **`033` — Refactor Pause Menu Navigation** (`docs/concept/08_INTERFACE_AND_CONTROLS.md`): Pause/settings menus already exist. Refactor to Resume, Save & Load, Settings, Exit to Title; ESC/B closes reliably without trapping input.
- [ ] **`034` — Add Gamepad Binding Support to `InputPreferences`** (`docs/concept/08_INTERFACE_AND_CONTROLS.md`, `10_ACCESSIBILITY_AND_COMFORT.md`): The binding system currently supports keyboard + mouse only. Add full controller binding, UI Toolkit navigation, and automatic button glyph swapping.
- [ ] **`035` — Add Directional Sound Captions** (`docs/concept/10_ACCESSIBILITY_AND_COMFORT.md`): FOV slider and crosshair toggle already exist. Add directional sound captions for hearing accessibility.
- [ ] **`036` — Implement Pause Free-Look Photo Mode** (`docs/concept/08_INTERFACE_AND_CONTROLS.md`): Free-look camera tool accessible from the pause menu to inspect unburied finds and photograph the carved hole.
- [ ] **`037` — Extend `WorldSaveController` to Multi-Slot Profiles** (`docs/concept/08_INTERFACE_AND_CONTROLS.md`): Versioned atomic saves already exist. Extend them to support multiple profile slots while persisting exact voxel deformations, placed trophies, inventory, and player state.

### Phase 8: Mystery Climax, Ending & Sandbox

- [ ] **`038` — Seed Anachronistic Mystery Trail Oddities with Visible Impossibilities** (`docs/concept/01_FANTASY_AND_TONE.md`, `11_ENDING_AND_MYSTERY.md`): Seed subtle anachronistic oddities with visible impossibility (modern objects fused into manufactured ancient stone, matching unusual modular connectors) establishing curiosity.
- [ ] **`039` — Embed Zone 4 Ancient Constructed Structure** (`docs/concept/03_WORLD_AND_SITE.md`, `11_ENDING_AND_MYSTERY.md`): Embed anomalous constructed architecture at the reservoir floor (>75m) with smooth unnatural materials that differ clearly from bedrock walls.
- [ ] **`040` — Implement Ancient Material Impossibility Contact Signature** (`docs/concept/09_FEEL_ART_AND_AUDIO.md`, `11_ENDING_AND_MYSTERY.md`): Implement ancient material contact signature: clean surgical cuts, glass-like resonance, and too-neat dust settlement without threat cues.
- [ ] **`041` — Implement Finale Components & Assembly Sockets** (`docs/concept/11_ENDING_AND_MYSTERY.md`): 3–4 required components in constrained regions with recoverable physical leads (cables/pipes); owned parts automatically insert into structure sockets without inventory management.
- [ ] **`042` — Stage Final Object Presentation Cutscene** (`docs/concept/01_FANTASY_AND_TONE.md`, `11_ENDING_AND_MYSTERY.md`): Reveal the ancient junk fabrication machine. Presentation sequence using normal excavation tools without genre switch or loss of tools.
- [ ] **`043` — Implement Ending Excavation History Timelapse** (`docs/concept/11_ENDING_AND_MYSTERY.md`, `13_OPEN_QUESTIONS.md`): Retrospective visual replay at the finale showing the progressive evolution of the player's carved hole from untouched start to bottom.
- [ ] **`044` — Implement Post-Ending Sandbox State & Media Wall** (`docs/concept/07_SURFACE_HUB_AND_DISPLAY.md`, `11_ENDING_AND_MYSTERY.md`): Seamless transition back to the surface hub with unlocked sandbox excavation. Media wall appears with regional newspaper clippings and quiet TV/radio props.
- [ ] **`045` — Wire Steam Achievements Manager to Milestones** (`docs/concept/12_ACHIEVEMENTS_AND_COMPLETION.md`): Wire 5–10 fair achievements for reaching each zone, maxing the machine, completing trophy stands, and revealing the mystery.

### Phase 9: Release Pipeline & Build Protection (Before Public Release)

- [ ] **`046` — Migrate Standalone Windows Build Pipeline to Native IL2CPP**: Switch the Windows standalone build to native IL2CPP with the MSVC compiler. Configure `link.xml` to prevent code-stripping on UI Toolkit and save types.
- [ ] **`047` — Integrate Release Code Obfuscation & Binary Hardening**: Integrate symbol stripping, class/method renaming, string encryption, and metadata obfuscation against reverse-engineering tools.

---

## Completed

- **[063] Drained Reservoir Environment:** authored four terrain tiles (inner edges at ±16 m) with vendor mud/gravel/grass layers, a sediment dig material, a grassy south camp terrace, rocky forested banks and distant peaks; hidden arena panels with unchanged boundary colliders, raised outdoor ambient for rock relief and camera far clip 600 m. EditMode 231/231 and PlayMode 191/191 passed (three timing flakes passed on isolated rerun); Windows player rebuilt. No dam/tent/crate/stump assets exist, so those stay out by user decision.

- **[062] Restore Simple Site:** restored MainGame and its presentation to the pre-environment revision, removed reservoir/preview assets and tools, and retained the purchased pack for manual authoring. All 40 relevant scene/gameplay checks passed, the simple site was visually verified and the Windows player rebuilt; saves and recovery content were preserved.

- **[061] Guided Reservoir Assembly (rejected):** copied selected demo lake/shore and river-bank sections into MainGame; gameplay checks passed but the composition was rejected. Removed with the other environment experiments in task 062.

- **[060] Restore Game Entry:** removed the preview override and restored MainGame's menu, walking, excavation and saves; 49 checks passed. Preview assets were subsequently removed in task 062.

- **[059] Direct Demo Lake Preview (rejected):** copied too much of the demo and replaced gameplay with a preview. The game entry was restored in task 060; preview assets and tools were removed in task 062.

- **[058] Reservoir Ground Base (rejected):** generated terrain, painted surface cap and grass variants did not meet the requested art direction. Removed in task 062.

- **[057] Drained Reservoir Surroundings (rejected):** attempted cliff, forest and mountain composition. Scenery, presentation overrides and the environment benchmark were removed in task 062.

> Format: `- [ID] Title: 1-2 sentences on what was implemented and how.`

- **[064] Demo-Matched Valley & Natural Boundary:** `ReservoirEnvironmentSetup` now authors a radial bowl (meadow -> forested slope -> a two-step over-steep bank -> broken ridge) instead of a box canyon, clothes every steep face by scanning the height field rather than by hand-placed cliff rings, and rings the valley with two ranges of vendor peaks sized in metres against the rim (470-1000 m tall, 0.7-1.7 km out) — the previous pass scaled them ~10x too small, so they read as pebbles. `MainGameRoot/Perimeter` is deleted; a 360-bearing sweep in `MainGameSceneTests` asserts every way out of the valley is steeper than the 45 deg slope limit (weakest 1.31).
  - _Iteration (playtest):_ the drained reservoir bed is now tree-free: `SpawnTree` declines any candidate inside `SlopeEdge * LobeAt` (still drawing its randomness so the seed's other scatter keeps its approved layout), removing 740 of 2,287 conifers from the floor and inner slope while the bank/ridge forest is untouched. `MainGameSceneTests` asserts no tree stands inside the bed, and now activates the scene under test before reading `RenderSettings` (the suite had been failing its fog check in the runner's empty scene). EditMode 232/232; Windows player rebuilt.

- **[056] Pure Nature Integration:** replaced the prototype grass with instanced BK grass, mesh-sized soil support, vendor wind and updated URP shaders while preserving project lighting. Asset/Git LFS workflow is documented; 304 checks passed, final grass/scene checks and native startup verified, and Windows player rebuilt with clean output-folder handling.

- **[055] Graphics Settings:** replaced the Graphics placeholder with persistent render-resolution, shadow, MSAA, texture-quality and filtering controls plus category reset; shadow tiers modify the runtime URP clone while excavation daylight stays independent. Preference/runtime checks passed, the menu and live shadow pass were verified, and the Windows player was rebuilt.

- **[054] Render Resolution Defaults:** new graphics preferences default to 100%, and supported display modes remain selectable above the current desktop resolution, including 4K and higher. Display/preference checks and runtime renderer verification passed; Windows player rebuilt.

- **[053] Dense World Performance:** conservative soil occlusion suppresses buried mesh submission, and anchored physics wakes only for terrain edits or explicit handling/restore. Native matched-view performance improved roughly fourfold with unchanged density; startup is capped at 144 FPS, gameplay verified and Windows player rebuilt.

- **[052] Fresh Find Density:** concentrated full-size rocks and coal in the first few metres, reduced buried soil gaps and stratified depth targets to prevent placement from emptying the start of each band. Fresh-face/local-patch checks and gameplay with earlier reveals removed now validate encounters; placement, collection and save/load passed, visuals reviewed and Windows player rebuilt.

- **[051] Deeper Finds and Noon Light:** increased ordinary depth-band populations and added a catalog-owned lower-reservoir allocation while preserving the accepted shallow layout exactly. Near-overhead sunlight and a global URP saturation profile brighten the presentation; placement, collection, save/load and lighting checks passed, game captures reviewed and Windows player rebuilt.

- **[050] Earlier Finds and Gentler Daylight:** packed more full-size rocks just beneath the turf using actual mesh envelopes and catalog-authored cover, while extending daylight reach without restoring an ambient floor. Placement/lighting checks and discovery gameplay passed, visuals reviewed and Windows build refreshed; existing saves keep their population.

- **[049] Dark Tunnels:** removed the 45% ambient floor and applied the connected-air daylight field to direct sun as well as sky fill/reflections on soil and finds, preserving local lamps. EditMode 221/221 and daylight PlayMode 2/2 passed; actual game tunnel captures verified and Windows player rebuilt.

- **[005] Reservoir Depth & Boundaries:** scaled the shipped site to 24 x 100 x 24 m (192 x 800 x 192 cells at 0.125 m) by appending solid soil below the untouched surface, made chunk objects materialize on demand so startup and load cost stop scaling with depth (7,200 keys, 144 built on a fresh site), and generalized the checkpoint migration so 12 m and 32 m saves deepen in place with their hole and every find intact. EditMode 220/220, PlayMode 187/187, Windows build rebuilt. Three tests left stale by the previous day's price/reach retune now read the authored constants.

- **[048] Buried Find Depth Density:** rebalanced the catalog to 2,042 finds (rock and coal became all-depth filler from 1.1 m down, ladder bands widened and raised ~1.4x) so every 1 m layer below 2 m holds at least 30 finds, kept the 312-find entry burst, and replaced the all-pairs placement scan with a deterministic spatial grid (46 ms for the full population, 8,192 save bound). Added a depth-density invariant across 100 seeds, an old-save compatibility case, and a session-only finds-per-m³ line in Developer admin.
  - _Iteration (same day, playtest):_ the all-depth filler made depth a colour swap. Catalog is now **1,996 finds** where every type has a dense core band plus a thin scatter band (`CoreMinDepth`/`CoreMaxDepth`/`CoreShare`), so the dig rate stays constant (~45–60 per metre of depth) while the mix slides from 61% rock/coal at 2–5 m to 94–100% gold-and-above below 22 m, with a few outliers crossing over both ways. New `DepthMixSlidesFromJunkToValueAndKeepsScatteredOutliers` test and rewritten constant-rate window check; concept §05/§06 updated in place; EditMode 216/216, PlayMode 186/186, Windows build rebuilt.
  - _Iteration (next day, playtest):_ entry layer raised to **640 finds at 0.45–1.05 m** with every find at **0.7 model scale** (`model_scale` in the source catalogs; mass stays authored), catalog at 2,324 finds. 0.4-scale models were tried and rejected: tiny finds are hard to read, and the layer only holds ~700 items at 0.7 scale before placement saturates. Smaller finds exposed a steady millimetre creep on slopes that kept them awake forever; `FindPhysics` now treats that creep as quiet so dropped finds settle, and small finds may resolve inside a single bite, so the exposure tests assert the contract (no collection below threshold) rather than a partial reveal. Headless content sync (`tools/sync-content.ps1`) and `-Filter` on `tools/test-fps.ps1` added for Editor-closed work.
  - _Iteration (second day, playtest):_ the **top metre is now rock-only** — 560 rocks at 0.4–1.0 m with 45% packed into the top 25 cm (an all-rock layer saturates near 590, so 560 is the workable ceiling at 0.7 scale) — and the ore ladder starts at 1 m with **coal at $4 (2× rock), copper $5**. Every upgrade track now shares one tier price ladder (`EquipmentProgression.TierPrices` = 10/25/55/100/180; the shovel runs one tier deeper), and neither the tool ladder nor the prices are serialized into `MainGame` any more. Tool tuning lives in Developer admin as live sliders (bite/speed/reach per shovel, per-level memory, _Print tool tuning_ → `Logs/tuning.txt`), and `tools/test-changed.ps1` runs only the classes a change owns, falling back to batchmode when the Editor is closed.
  - _Iteration (005 playtest):_ the shallow layer was criticised in both directions (one find per scrape too sparse; smaller pieces and added junk both rejected). Final shape: **full-size rocks own the shallow layer** (1,000 rocks, 460 hung just under the turf at 0.47–0.75 m) and the **ore ladder starts beneath them** (coal $4 from 1 m, then copper…diamond). Catalog is 2,578 finds, every type at its authored 0.7 scale; bottles stay retired as zero-count entries so old saves resolve them. Measured ceilings: 520 shallow rocks saturates the placer and 600 fails, so 460 is the packing limit for full-size rocks — a ~1.2 m wide, 0.3 m deep starter scrape turns up **~1 rock** (max 2). More per scrape requires smaller pieces or additional small object types; that trade is recorded in concept §05. The shallow tier packs with best-of-8 candidates while banded types keep their 64-candidate spread, and the sync tool applies the authored `model_scale` on import. EditMode 220/220, PlayMode 187/187, Windows build rebuilt.
- **[003] One-Click Workbench & Sell Machine Table:** rebuilt both station menus as one fixed-size table (`Station.uss` + `ToolkitStationRows`) with a money-only header, category columns, one-click rows carrying price plus the changed value, and tooltips for secondary stats; added `Add $500` to Developer admin and rebuilt the Windows build.
- **[000] Baseline Transition:** Consolidated project documentation, migrated concept chapters into `docs/concept/`, established baseline prototype inventory (`docs/baseline.md`), decentralized minimal asset tracking, and established the roadmap.

### Dropped as Already Implemented (pre-existing prototype)

- **Hold-to-Dig & Toggle:** Fully implemented in `FpsInput.cs`/`InputPreferences.cs` (hold default, toggle persisted).
- **Terrain Crumb Cleanup:** Fully implemented in `ExcavationGrid.cs` (`RemoveDetachedSoil`, `RemoveTinyRemnants`).
- **Subterranean Daylight Falloff:** Implemented in `ExcavationDaylight.cs` (connected-air sunlight and sky fill, gradual early fade, no brightness floor).
