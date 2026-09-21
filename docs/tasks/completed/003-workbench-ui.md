# 003 — One-Click Workbench & Sell Machine Table

**Status:** complete. Rebuilt both station menus as one fixed-size parts-board table (`Station.uss` + `ToolkitStationRows`): money-only header, category columns, one-click price/payout rows with tooltips. Superseded by 073, which merged both stations into `ComputerStation`.

## Objective

Turn the station menus into one fixed-size parts-board table: a money-only header, categories in
separate columns (UPGRADES wide and dominant, SERVICES narrow beside it), and one row per track,
refill or carried find. The row itself is decoration — only the price button inside it is
interactive, with the hover/pressed/focus states belonging to that button alone. The button shows
the price, the row shows the single number the purchase changes, and one click buys: no selection
step, no confirm control, no icons, no close chip, no state sentence. The panel never changes size
when the player buys something or cannot afford it.

## Concept Reference

- `06_PROGRESSION_AND_ECONOMY.md`: transparent current -> next and cost, sequential purchases,
  immediate effect.
- `07_SURFACE_HUB_AND_DISPLAY.md`: every track visible with its next level, first interaction works
  on the first try.
- `09_FEEL_ART_AND_AUDIO.md` §8: industrial-worksite flavour, high contrast, out of the way.
- `08_INTERFACE_AND_CONTROLS.md`: controller parity, ESC/B always closes.

## Implemented

- `Runtime/UI/Toolkit/ToolkitStationRows.cs` (new): block, label, segment and button builders. Rows
  are plain `VisualElement`s; the only `Button` in a row is its price/payout button, so nothing else
  can be clicked and the rows carry no interaction states.
- `Runtime/UI/Toolkit/GameMenuView.cs`: `BuildUpgrade` renders an `UPGRADES` column (equipment
  tracks, larger rows) beside a narrower `SERVICES` column (refill and future consumables);
  `BuildSale` renders one row per find plus a full-width `Sell all` bar; `BuildStationHead` prints money only;
  `ActivateUpgradeRow` buys on a single activation or pulses the row when the price is out of reach.
  Secondary stats (reach, stroke, fuel rate) live in the row tooltip.
- `Runtime/Player/FpsPlayer.cs`: `GrantAdminMoney()` (+$500) for playtesting, exposed as
  `Add $500` in Developer admin.
- `Runtime/UI/Toolkit/Resources/GameMenus/Station.uss`: single parts-board skin, fixed 820x336
  workshop and 560x300 sell panels, explicit dark row fills (never the theme's grey button face),
  hover-only on `.station-price` / `.station-sell-value` (no pressed state, disabled buttons are dim
  and inert), upgrades larger than services. A row is name over "progress + value" on the left with
  the price button vertically centred on the right — no label under the button.
- Trade logic, prices, offer revisions and save schema untouched.

## Edge Cases

- Cannot buy: the price button is genuinely disabled — dimmed face, no hover, no press — so it can
  never look buyable when the purchase is out of reach, the track is finished or the tank is full.
  Hover is the only reaction an enabled price button has (no pressed state left behind).
- Finished track shows `MAX`; a full tank shows `FULL`; nothing else is printed.
- Sell rows keep one row = one find; `Sell all` stays one activation and scrolls with the list.
- Stale offers still funnel through `ExecuteStationCommand(index, revision)`.
- Longer lists scroll inside the fixed panel instead of resizing it.

## Verification

- EditMode: 214/214 passed.
- PlayMode: 184/190 passed, 2 skipped, 4 failed — all four in dig/collection helpers
  (`DiscoveryIntegrationTests.DigAbove`, `RescueIntegrationTests.CollectFirstFind`), unrelated to the
  station UI, with a different member failing on each run and passing in isolation. Station suite:
  7/7 including one-click buy, pointer purchase, unaffordable refusal, refill and admin money.
- `builds/windows/SomethingDownThere.exe` rebuilt with this layout.
