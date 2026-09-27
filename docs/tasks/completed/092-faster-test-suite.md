# 092 — Faster Test Suite

**Status:** complete. Find-physics tests restore a compact find population, and Discovery tests the shallow layer plus samples of every type. Trading and worksite tests skip the population, Save tests autosave after 1 s and compare density directly, and slow end-to-end tests are opt-in (`[Explicit]`, run with `-Full`). Low-value UI/dev-tool/duplicate tests and the behaviour-free `FindSize` are gone, and `test-changed.ps1` is incremental: it recompiles external script edits and tests only files changed since the last passing run, selecting suites by first-matching owner rule.

## Objective
Cut test time that slowed development (about 40 min for the full suite; a routine change often selected ~35 min of PlayMode classes). Keep coverage of save integrity, digging/meshing, find exposure/visibility/release, collection, economy and winch recovery.

## Analysis
- Most PlayMode time was setup. Each test loads MainGame additively (~3.5 s), and `DiscoveryField.Start` then instantiates ~12.4k finds (~6 s). FindPhysics setup additionally captured and restored the whole population to retag 3 finds (~16 s per test, ~650 of its 907 s).
- `DroppedRocksSettleAfterRepeatedExtremePitchChanges` settled 11 rocks sequentially (~175 s) and hit its 180 s timeout.
- EditMode `DiscoveryCatalogTests` regenerated the same seeded layouts repeatedly (100 seeds in one test). Its burial sweep also re-read a 12.5k-vertex array for every placement.
- `test-changed.ps1` applied every matching rule. `Content/` selected the four heaviest classes, even for shaders. Partial and helper test files mapped to classes that don't exist, so zero tests ran and still reported a pass. Settings files selected Save, Rescue and Recharge.

## Changes
- `TestInputPreferences.RestoreSmallFindFixture` caches one compact population per run: the three coal finds first, three of each common appearance, and every unique. `ConfigureCompactFinds` defers generation once the cache exists. `ConfigureWithoutFinds` (Worksite) and Station's own hook skip generation entirely.
- The dropped-rock stability test keeps per-rock holds and pitch swings, then settles all rocks together in one drift window.
- `DiscoveryCatalogTests` shares layouts per seed within a run and uses 20 seeds for shallow coverage. The vertex burial sweep runs on one seed, with vertices read once per appearance. Reproducibility still compares two fresh generations.
- `[Explicit]` (run with `-Full`):
  - Discovery: `DefaultShovelRevealsMultipleShallowFinds…`, `DeeperScrapesRevealFreshFinds…`
  - Save: `CrouchOnlyChangesAutosave…`, `EveryDepthMineralCanBeUncovered…`
  - Terrain: `LargeRepeatedCutsExpose…`, `MainGameExcavatesThroughFormerFloor…`
  - FindPhysics: `DroppedRocksSettle…`
- Deleted as cosmetic, developer-tool or duplicated elsewhere:
  - FindCollectionFlow: `ShavingUncoversAndCollectsOneAimedIdentity…`, `PickupVisualPullsQuickly…`
  - Discovery: `AimedUncoveringAssistsBothFindSizes…`, `ToggleOnFullBagCollectsWhenSpace…`
  - Terrain: `RepeatedPrimaryActionsCannotBypassACutCooldown…`, `RepresentativeAcceptedCutsReport…`, `AdminMenuButtonsChooseStrength…`
  - Station: `AdminCanGrantTestMoney…`, `CriticalFractionalFuelRefills…`, `FullBagRowsScrollWithKeyboard…`
  - SurfaceRecharge: `EmptyWalletBagAndFuelStillUse…`, `BottomCenterFuelWarningSurvives…`
  - FpsUiInput: `HudRetainsControlsAndCenteredLayout…`, `SharedControlsKeepReadableStates…`
  - Rescue: `UnlimitedBatteryDoesNotRescue…`
- Each deleted behaviour keeps a covering test: `FindCollectionFlowTests.AimedHalfCovered…`/`FullBagKeepsCutting…`, `FpsPlayerTests.DigCadence…`, `StationTradeTests.FractionalCriticalFuel…`, and `RescueTransactionTests`/`ReturnWarningTests`.
- `test-changed.ps1`:
  - First matching rule wins, with specific owners first, and helper/partial files map to their real suites.
  - Settings and graphics files → `FpsUiInputTests`. Shaders, materials, editor tooling and project settings → EditMode only. `MainGame.unity` → `StartupMenuTests`.
  - A selected class that runs no tests now fails the run. `-Full` includes `[Explicit]` tests.

## Phase 2
- **`FindSize` removed.** It had no runtime effect; the concept has no size classes. Find prefabs were reserialized. Tests that meant "Small" now select the three coal fixture finds (`TestInputPreferences.IsCoalFixture`), so their semantics are unchanged.
- **Discovery** restores the generated shallow layer, three of every other type and all uniques (`RestoreLayerFixture`). Full-count checks assert against the scene's own recorded generation. The two opt-in deep-layout tests restore the generated population. Redundant input variants were removed: held at level 1 and toggle at level 6 still cover both inputs and both levels, and the toggle wide-scoop duplicate is gone.
- **Save:** `WorldSaveController.AutosaveSeconds` is a per-controller property (default 10 s); Save tests use 1 s. Density checks compare samples directly (`AssertSameDensity`). Wait helpers assert only on timeout, instead of every frame.
- **EditMode catalog tests:**
  - Layouts are cached per seed and keyed by catalog content; a per-fixture cache was reset between tests by the runner.
  - The shallow-coverage sweep asserts once per seed instead of ~2,500 times.
- **`test-changed.ps1`** retries its clean-scene preparation while the Editor finishes leaving the previous Play Mode run.

## Results (class time, this PC)
| Suite | Before | After |
|---|---|---|
| EditMode | 135 s | 35 s |
| FindPhysics | 907 s | 209 s |
| Discovery | 337 s | 86 s |
| Save | 366–420 s | 271 s |
| Station | ~85 s | 23 s |
| Worksite | ~40 s | 16 s |
| Terrain | 119 s | 88 s |

The default full run is roughly 40 → 16 min. A settings or UI change now runs EditMode plus `FpsUiInputTests` (~1 min).

## Deferred (measured estimates from the audit)
- **Merge parameterised variants** into loops (FindPhysics pickup cadence ×6, AimedHalfCovered ×4, walk-over rules; Discovery input variants; Save reload variants). Worth less now that setup is cheap.
- **Shared `[UnityOneTimeSetUp]` scene per class:** now worth ~70 s (Terrain) at the cost of cross-test state leaks. The compact populations captured most of the gain.

## Verification
- Phase 1: EditMode and every selected PlayMode class passed; the one timeout is covered in 091.
- Phase 2: EditMode 297/297, Discovery 18/18, FindPhysics 38/38 and Save 12/12. Startup, Station and Worksite results are in the task hand-off.

## Iteration
- A 074 run tested stale assemblies: the unfocused Editor had not imported externally edited test files, and the run reported failures from the previous code. `test-changed.ps1` now recompiles first when any script or asmdef is newer than `Library/ScriptAssemblies`, and stops on compile errors.
- The default selection was every uncommitted file. Work is rarely committed between tasks, so each run grew toward the full suite (114 dirty paths at the time). The script now records per-path content hashes after passing checks and selects only paths changed since then. Failing paths stay selected. Docs, art cards and tooling select nothing.
- EditMode spent about 11 of its 38 s building 20 seeded layouts, shared by five population sweeps. By default the sweeps now check the three seeds the fixed-seed tests already build. Their 20-seed cases, and the aggregate depth-mix sweep, are `[Explicit]` and run when catalog or placement code changes, or with `-Full`. Default EditMode: 38 → 24 s. Package, asmdef and Unity-version changes widen to the whole default PlayMode assembly. The runner filter matches one name substring, so PlayMode classes still run one at a time.
- Two fixes after the first incremental runs. An EditMode failure used to reselect every PlayMode class; a file whose PlayMode classes passed now records that separately, so a rerun repeats EditMode only. With 2 s polling a transitional runner status read as "finished" and the next class started over a running one; only a completed or error status now ends a wait, and a run that did not complete counts as a failure.
