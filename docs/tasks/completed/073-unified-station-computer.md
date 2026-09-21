# 073 — Unified station computer

**Status:** complete. Merged sale/upgrade ownership into `ComputerStation` using the selected Cosmic retro model; selling flows directly into upgrades, an empty bag opens upgrades directly, and the old station art/code is removed. The prompt reads simply `Use`.

## Objective
Replace the separate sale and upgrade machines with the user's imported retro computer.
With carried finds, interaction opens the existing selling table; selling the last find or
using Sell All immediately opens upgrades. An empty bag opens upgrades directly.
The interaction prompt is simply `Use`, with no key prefix or computer label.

## Concept references
Update concept overview, core loop, economy, hub and interface chapters to describe one
computer. Keep the future salvage winch separate from the retired hopper/workbench design.

## Live code analysis
- `SellStation` and `UpgradeStation` duplicate interaction ownership but share `StationTrade`.
- `FpsPlayer` owns station reach/focus checks, menu revisions, input barriers and autosave events.
- `GameMenuView` chooses sale/upgrade tables by component type and binds revision-safe buttons.
- `SurfaceStationSetup` authors two Blender models and `StationMotion` moving parts.
- MainGame has two surface anchors; the imported Cosmic pack provides four candidate prefabs.

## Architecture changes
- Merge station behavior into `ComputerStation`, with a selling phase derived from refreshed
  sale offers. Retain `StationTrade` as the only transaction authority and current economy.
- Reuse both Toolkit tables; select them by computer phase. Refresh offers after each sale,
  preserving the same station, paused menu and current wallet. No additional click to upgrade.
- Retain individual sales until the bag is empty. Sell All remains the primary whole-bag action.
- Keep upgrade/refill command IDs disjoint from sale IDs so stale direct calls cannot buy.
- Refactor `SurfaceStationSetup` and scene construction for one computer prefab and collider.
  Use computer 3 from the supplied screenshot and the imported URP material; preserve vendor GUIDs.
  Repair the FBX's stale material remaps to the included material; document the import patch locally.
- Delete the two retired station scripts, moving-part script and obsolete station art,
  prefabs, materials, textures, Blender source and pedestal materials after reference checks.
- Update existing station/scene/refill integration checks; add focused phase-transition
  coverage for the money/inventory flow, stale callbacks and empty-bag entry.

## Edge cases
Cancellation never sells; partial individual sales remain on selling. Failed/stale sales
retain inventory and do not advance. Reopening re-evaluates the current bag. Credit-limit
refusal preserves the haul. Repeated sale activation cannot spend proceeds on upgrades.
Focus loss, out-of-range access, disabled targets, pause and held-input barriers remain.
Do not alter the protected recovery scene or add save migrations.

## Acceptance criteria
- MainGame contains exactly one functional computer matching the reference model, on the apron.
- Loaded bag opens selling; Sell All/final single sale opens upgrades with correct proceeds.
- Empty bag opens upgrades, and upgrades/refills still apply once per activation.
- Retired station assets and code are removed; current scene has no missing references.
- Compilation is warning-free, relevant economy/scene/input integration checks pass,
  gameplay presentation is inspected and a fresh Windows player is built.

## Validation
- Warning-free C# compilation; all 279 selected checks pass: 217 EditMode, 26 UI input,
  14 persistence, 8 startup, 8 station interaction and 6 surface/refill checks.
- Play Mode review confirms the selected upright model, interaction prompt, selling table,
  immediate upgrade transition with correct proceeds, one-click purchase and empty-bag revisit.
- MainGame has one computer, no missing scripts and no retired station asset dependencies.
- Fresh Windows development build succeeded with zero errors and passed Odin validation.
  The sole build warning is the existing Pipeline package notice that its optional runtime
  connection is disabled; C# compilation is clean. Evidence is under ignored `Logs/Task073`.
- Recovery scene left untouched; obsolete station art and temporary screenshot imports removed.
