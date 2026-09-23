# 086 — Buried Computer Unique

> The Reservoir Computer uses the existing free computer 7 as a unique authored deeper underground for recovery testing, separate from the trading terminal. One unsellable discovery record survives rope recovery, pad storage and deliberate exhibit placement with its original discovery depth and story.

## Objective and delivery boundary

Make a recognizable computer emerge from intact soil, remain present while the player digs around it, and become the first permanent exhibit. Implement the content/ownership foundation first, then [002](002-rope-extraction.md); deliver and accept both together so this task never ships with an unreachable unique or temporary instant-pickup behavior.

Material work `007` remains independent: this slice uses the existing density/cut pipeline.

## Concept reference

- `05` §§1, 3, 4, 8–9: a unique exists once per save, is unsellable, consumes no bag capacity, survives digging and cleanup, and rewards recognizable partial exposure. Recovery requires deliberate interaction; neither walking nor held digging collects it. No complete-cleaning or identification minigame.
- `03` §§1, 6–8: start with intact ground, preserve the player's excavation and permanent boundaries, and use actual daylight attenuation. No chamber, pre-dug route, personal light or hidden-object marker.
- `07` §5: safe ownership, an individually placed exhibit, name and discovery depth, and a rereadable deadpan sentence. No inventory screen or mechanical reward.
- `08` §§2, 4, 7: rebound interaction input and whole-world persistence; a full ordinary bag cannot prevent unique recovery.
- `14` §§2–5: prove one complete special-find chain before producing the remaining slice roster. Validate recognition and recovery in play, not only through data checks.
- User direction supersedes the old gramophone choice and the requirement to return to a surface button before hauling. `002` specifies the new marking and rope behavior.

## Live codebase findings

| Existing owner | Relevant finding and required extension |
|---|---|
| `DiscoveryCatalog`, `DiscoveryField` | Already own finite population, stable prefab resolution, deterministic placement and restore. Reuse them; do not add a separate unique spawner. Placement currently rejects larger envelopes and its neighbour scan assumes small finds. |
| `DiscoveryContentSetup` | Rebuilds the merged catalog from `PhotoRockSetup` and `MineralSetup`. Source entries contain tier/slot fields, but prefab setup currently makes every entry minor and detector-silent. A manual catalog addition would disappear on the next sync. |
| `BuriedFind` | Owns real surface exposure, renderer visibility, world identity and collection. Assumes a root mesh/renderer/collider and ordinary bag collection. Its collected boolean cannot describe a recovering, stored or displayed unique. |
| `FindPhysics`, `FindHandling`, `FindProximityCollection`, `FpsPlayer` | Loose release, RMB handling, aimed pickup and nearby pickup all assume ordinary finds. Explicit policy gates are needed at the shared collection/handling entry points, including collection immediately after a cut. |
| `SessionInventory`, `InventoryItem`, `StationTrade` | Capacity is an item count and all bag entries are sellable. Keep the bag for ordinary goods; unique ownership belongs to the existing discovery record. Do not represent unsellability merely with a sale value. |
| `WorldSnapshot`, `WorldSaveCodec`, `WorldSaveController` | Current format stores world finds plus bag entries, but no extraction/display lifecycle or exhibit depth. Dirty observation includes find motion, but not special ownership changes. |
| `SurfaceStationSetup` | Uses `Cosmic_Retro_Computer_3.prefab` for the trading station. Its interaction and setup must remain separate from the buried prop. |

Read-only live Editor inspection confirmed MainGame is saved and not playing, and the imported pack contains computer models 3, 7, 14 and 15. Each prefab uses a single mesh and the pack's URP Lit material, with no authored collider. No project assets or scene contents were changed by inspection.

## Selected content and authoring

- Use `Assets/Cosmic_Retro_Computer_1_FREE/Prefabs/Cosmic_Retro_Computer_7.prefab` as the visual source. It is a different model from the trading terminal, already imported under the license recorded in `art/retro-computer/README.md`; no download or purchase is required.
- Preserve vendor files/GUIDs. Create a centered project-owned discovery prefab and derived mesh/collision/material assets under `Assets/Content/Discoveries/RetroComputer`. Retain the existing root-mesh contract rather than refactoring every ordinary find to support child renderers for this single-mesh asset.
- Adapt the material through the existing excavation-lighting path. Check UVs, material references, silhouette and darkness in the actual scene; the computer must not glow through burial or function as an underground lamp.
- Add `art/retro-computer/catalog.json` as the source for this discovery and extend the existing local asset card during implementation. The JSON owns stable content ID, display text/lore, unique category, rope recovery policy, detector eligibility, authored placement/orientation, exposure requirement and collision/scale authoring. Numeric item properties and placement values stay out of Markdown.
- Add `RetroComputerSetup.AppendToCatalog` to `DiscoveryContentSetup.Sync`, using the existing mesh centering, surface sampling and prefab authoring helpers. Refactor reusable helpers out of the import-copy assumptions where necessary; do not copy vendor FBX/textures into an invented external-import workflow.
- Create a simple convex hull around the computer's solid silhouette as a project-owned collision mesh. Exposure samples come from exterior visual surface area, not a bounding box or enclosed internal faces. The selected scale must read as a computer and give the rope a visible load; do not shrink it just to satisfy the old small-find placement limit.
- Author the initial location laterally near the early excavation, within usable daylight and starter-tool reach over an ordinary outing. Fully bury it with real cover and without overlaps; no detector/lamp dependency or permanent guidance marker. Exact placement lives in the source catalog. New-game content is deliberately reproducible for this proof; wider seed-randomized unique placement belongs to `011`/`077`.

## Architecture and class changes

### Catalog and generation

1. Introduce explicit discovery category and recovery policy on the authored entry/prefab. Implement only current common/unique and bag/rope behaviors; do not build the full future roster or speculative category systems.
2. Extend `DiscoveryCatalog.Entry` and source validation with optional authored placement. Validate unique multiplicity, unsellability, no bag allocation, nonempty lore, detector eligibility and a valid source prefab. No old-format aliases or migration readers.
3. Generate authored entries through `DiscoveryField` with the same stable identities and saved population as other finds. Reserve their full geometry envelopes before placing ordinary finds. Refactor the existing placement grid to accept those reservations and radius-aware boundary margins; preserve shallow/common band selection and population accounting.
4. Correct the neighbourhood clearance scan for variable radii: scan every cell within candidate radius plus the largest reserved radius and soil margin before applying the spread-ranking early exit. Changing only the maximum-radius constant is insufficient. Validate the computer against the circular opening/apron context as well as the rectangular subsurface bounds.
5. A load restores its saved instance/pose and never generates another computer or replays authored placement over an existing population. Invalid new-game placement fails explicitly instead of silently omitting the unique.

### One discovery record throughout recovery and display

- Add a `FindState` with `World`, `Extracting`, `Collected`, `Stored`, `Displayed` and replace the persisted collected boolean with it. `Collected` remains the ordinary removed-from-world state, whether carried or sold; `Stored` and `Displayed` are unique-only. Existing call sites may read a derived `Collected` property, never maintain a second mutable lifecycle flag.
- `BuriedFind` retains instance identity, content identity, original discovery depth, current pose and state. `DiscoveryField` owns validated transitions and a revision for state changes, in addition to motion. `002` owns the in-progress haul job, referenced to this same instance.
- Separate `ExposureReady` from `CanCollectIntoBag` and `CanMarkForExtraction`. Update `TryCollect`, nearby pickup, post-cut pickup and `CanLift` so rope finds cannot enter ordinary collection/throwing. Keep digging around a ready unique possible: `TryGetCoveringSoil` must not stop helping simply because its exposure threshold has been reached.
- Carry explicit sellability/category through `InventoryItem` and its snapshot validation; `SessionInventory.TryAdd` rejects unique items and `StationTrade` cannot quote or sell them. Ordinary inventory counts and cadence remain intact.
- Stamp discovery depth on the first qualifying visible encounter (exposed geometry and unobstructed player targeting), before hauling changes position. Do not reveal name/lore through intact soil. Once recorded, moving, storing or displaying the object cannot rewrite that depth.
- `FindPhysics` keeps the unique anchored until extraction takes ownership. No accidental gravity release or player lift can drop it into another shaft before marking; ordinary rocks retain their existing physical behavior.

### Yard exhibit

- Add `UniqueDisplayStand : MonoBehaviour, IInteractionTarget` under `Runtime/Interaction`. It resolves the stored unique through `DiscoveryField`; it has no separate item inventory and cannot instantiate a second ownership record.
- MainGame gets one plain compatible stand and the receiving pad supplied by `002`, using original geometry created through Blender MCP with recipes and `.blend` sources under `art/salvage-winch/`. Keep station ownership under `MainGameRoot/Surface` and preserve the trading computer.
- After arrival the actual computer remains visibly secured on the pad in `Stored` state. At the empty stand, interact to place that same owned object with a short nonblocking snap. No underground carrying, placement menu, purchase or automatic exhibit placement.
- Before first placement, the focused stand can show the recovered object's name, recorded depth and one-sentence story; interacting places it. Interacting with the placed exhibit rereads that card in the existing UI Toolkit presentation. No price, rarity, condition or stat bonus. `018`/`019` later extend this foundation to multiple compatible sockets and richer presentation.
- Empty stands never preview the undiscovered computer or its name. Stored/displayed views do not participate in buried exposure culling or ordinary pickups.

### Persistence and scene tooling

- Extend `FindSnapshot` with lifecycle, discovery-depth validity/value and optional display socket ID. `002` adds extraction job state to the same `WorldSnapshot`.
- Advance the current save version once for the combined delivery; reject previous formats using the existing unsupported-save flow. New Game is the development workflow; no migration support.
- `WorldSaveController` observes discovery-state, extraction and display changes and requests checkpoints at ownership transitions. Capture all of them with terrain/player data in one consistent main-thread snapshot; keep the existing background writer and atomic replacement.
- Restore terrain first, then find records, then extraction/pad/display views. Cross-validate content category, bag exclusion, socket uniqueness and state/job agreement before mutating the scene. Invalid current-format data uses existing corruption recovery, never silent regeneration.
- Use shared idempotent `SalvageWinchSetup` editor wiring for the stand and references; catalog sync must retain the computer on every run. Save intentional edits only to MainGame, never the protected recovery scene.
- Add focused Odin validation for catalog/scene references and category/recovery invariants. Update `baseline.md`, affected ownership in `architecture.md`, the local asset card and tooling guide only once the implementation actually exists.

## Edge cases

- A full bag and zero battery cannot block marking, storage or display; powered player actions retain their existing costs.
- Walking across it, held/toggled digging, a wide cut, cleanup and later C4 must not collect or destroy it. No external forces change its anchored pose before extraction.
- Recognition and marking are distinct from actual ownership completion. Reserved/in-flight items cannot be sold, displayed or claimed again.
- Save/reload at partial reveal, exposure-ready, arrival and display yields exactly the same instance and progress; repeated loads cannot duplicate it.
- Ordinary finds around the computer remain available, with the established full-bag hard stop and dig cadence. Surface station use must still open the appropriate sell/upgrade screen.

## Acceptance criteria

- A fresh MainGame contains the selected computer fully underground, separate from the shop terminal, with no common-find or boundary overlap. Repeated catalog sync retains it and valid new games never omit it.
- A normal early excavation exposes a recognizable fragment and then the whole silhouette. No x-ray cue, bright material or instant disappearance spoils discovery.
- Below the authored exposure requirement, extraction is unavailable. Above it, `002` supplies hold-to-mark on any visible part. The object remains available even when the ordinary bag is full.
- The complete `002` haul ends with the same computer on the receiving pad, then allows deliberate placement on the stand and repeatable lore/depth inspection. Selling ordinary loot never includes it.
- Current-format snapshots preserve every lifecycle state and reject duplicate identities, invalid sockets and illegal bag membership without corrupting the last valid checkpoint.
- Relevant catalog, placement, input/collection, ownership and save tests pass; no cosmetic layout unit tests. Include dense common placement beside the reserved larger object and regression checks for common pickup cadence.
- Warning-free compile, Odin MainGame validation, manual recognition/arrival/display playtest and a fresh `builds/windows/SomethingDownThere.exe` complete the combined delivery. Archive both specs and remove their queue entries only after both are accepted against these criteria.

## Implementation decisions

- Reserve the larger authored envelope before common placement; check rare large reservations separately so the dense common bucket scan stays local.
- Copy vendor geometry into a new readable owned mesh instead of inheriting the imported mesh's runtime readability flag. The owned material disables emission; the underground and exhibit fronts face the player approach. Vendor content stays unchanged.
- Current-format lifecycle validation rejects unique bag membership, duplicate ownership and mismatched extraction progress. Common finds retain their existing saved historical values.

## Verification

The combined test, visual-review and fresh Windows-build evidence is recorded in [002](002-rope-extraction.md#verification).

## Deeper physics test placement

The authored computer is moved deeper in its source catalog for longer approach and haul testing. New worlds use this placement; existing find coordinates are not rewritten. Once fully freed it can fall and settle, while bag/grab exclusions and its unique identity remain unchanged.
