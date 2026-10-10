# 117 — Menu Redesign: Title, Pause, Fullscreen Settings, HUD, Shop

**Status:** on trial: the game's own slate, bone and orange palette. The title shows the crane over the dig site
(blurred cliffs) with a large stacked logo and the menu centred under it; pause is only its menu, with New Game;
settings is a full-screen slate sheet with tabs, in-place ◀ value ▶ selectors and a description panel; dialogs
and the shop are slate cards with orange actions. The HUD shows only money, bag, lamps and an upright battery bar
coloured by the return estimate. Pause has Main Menu. No icons until the user picks an icon pack. Lilita One and Barlow Condensed (OFL) set the
type. Awaiting the user's playtest.

## Objective

The user (2026-10-10), with three reference screenshots from *A Game About Chopping Trees*: "redesign the home
menu so it looks closer to the screenshots, also experimentally redesign the settings also, they are fullscreen."

References, as read from the screenshots:
- **Title:** full-bleed 3D scene, logo top-left in a chunky yellow display face with a dark outline, slightly
  tilted; menu entries below it as large cream capitals with no buttons or panel; version small at bottom centre.
- **Settings:** an opaque warm beige page. A darker tan bar of pill tabs on top (selected tab lighter); a left
  column with spaced section headings and rounded cream rows (label left, ◀ value ▶ right, a segment bar under
  the value, a dark outline on the focused row, greyed rows when unavailable); a right column describing the
  focused setting with its choices listed and the current one marked; pill buttons bottom right.

## Concept Reference

- `09` §8 UI art: readable, high contrast, never competing with the world. Its former "industrial-worksite,
  stenciled labels (final treatment later)" placeholder is replaced by this direction, marked on trial.
- `08` §3 pause: ESC/B always closes the current menu and never traps input; settings persist immediately.
- `08` §8 settings: every option persists immediately; display changes keep timed Keep/Revert.

## Decisions

- **Fonts:** the user chose Lilita One (display) + Barlow Condensed Medium/SemiBold (text) over a stencil face or
  Windows' Bahnschrift. Imported as TTF and referenced with `-unity-font: url(...)` (dynamic SDF at runtime, so
  outlines and shadows work); no FontAsset files, so no atlas churn in git. Card: `art/ui-fonts/README.md`.
- **Scope of the new look:** title, pause, settings and dialogs only (`Screens.uss`, every rule scoped by the
  screen class on `#menuRoot`). HUD, station, inventory and admin keep the grayscale card; Station.uss is untouched.
- **Title:** the camera still shows the spawn view behind it (the first-launch graphics test measures that view).
  A left-to-right dark wash (runtime 128×1 texture) keeps text readable over bright sky. Entries turn yellow and
  nudge right on hover or keyboard focus. Version label from `Application.version`.
- **Pause:** same language as the title: "PAUSED" in the logo style and the entries (Resume, Settings, New Game,
  Save and quit), nothing else. Focus loss still hides everything over the paused world.
- **New Game from pause:** the title's "Start a new game?" dialog; Cancel or Esc returns to pause. Confirming stops
  checkpoints of the running world, lets a write in flight finish, releases the profile and reloads the scene; its
  title then starts the new game at once (static flag, as the Ground Lab restart), archiving the old world as the
  title's New Game does. Rebuilding the world in place was rejected: every system (terrain, finds, crane, lamps,
  progression, player) would need its own reset, where the reload reuses the one tested startup path.
- **Settings:** no Apply / Cancel changes. Options apply and persist immediately (concept `08` §8), so the
  bottom-right buttons are Reset category and Back. Each category opens with a spaced heading (DISPLAY, GRAPHICS
  QUALITY, AUDIO, MOUSE / MOVEMENT / ACTIONS, COMFORT).
- **Selectors replace dropdowns and toggles** (`SettingSelector`, `INotifyValueChanged<int>`): arrows and
  Left/Right step, Enter or a click on the value advances. Choices wrap as before; on/off rows treat ◀ as Off and
  ▶ as On (as the keys did) and a click on the value flips it. One pip per choice up to eight, else one track with
  a marker (resolution, FPS limit). With no popup list, the dropdown Escape handling (`DismissDropdown`, the
  `menuDropdown` styling) is gone.
- **Description panel** (`ToolkitSettingsHelp`): follows focus and pointer hover; shows the row's name, a short
  description and its choices with the current one marked (a slider shows its range, a binding its key). The old
  graphics help paragraph is split into the rows' descriptions.
- Tabs, footer and title entries are capitalised in code (USS has no text-transform); element names are unchanged.

## Changes

- `ToolkitMenuComponents.cs`: `SettingSelector`, `ToolkitSettingsHelp`.
- `ToolkitSettingsRows.cs`: `Choice`/`Toggle` build selectors; every row takes a description and feeds the panel.
- `ToolkitDeviceSettings`, `ToolkitInputSettings`, `ToolkitCameraSettings`: headings, descriptions, shared panel.
- `GameMenuView.cs`: logo, wash, version, `pause-menu` class, capitalised entries; dropdown code removed.
  `FpsPlayer.BackFromSettings` no longer asks for a dropdown dismissal.
- `GameMenus.uxml`: logo, wash, help panel, version; footer order Reset, Back. `Screens.uss` new; superseded title,
  pause, settings, dialog, dropdown and toggle rules removed from `Theme.uss` / `Controls.uss`.
- Tests: selectors instead of `DropdownField`/`Toggle`; the dropdown-Escape test is replaced by
  `SelectorsStepInPlaceAndDescribeTheFocusedRow`; startup order compares names; tab captions capitalised.

## Iteration 1 (2026-10-10)

The user on the pause menu: "this is awful, dont show controls when i am in here also when paused add option for a
new game". The controls card is gone (layout, styles, labels and their tests); pause gained New Game (above).
Concept `08` §3 updated.

## Iteration 2 (2026-10-10): HUD

The user, with the old HUD (dark panel "BATTERY 44% | SAFE", "FINDS 2 / 10", "$0", then shovel level, cut, reach,
depth and the lamp/mark key line) and two references (a banknote stack beside a bold "291"; a rounded battery bar
with a bolt at the bottom-left): "hide the shovel and reach details. it is important only to show battery, money,
inventory and lamps and i want it in a different way".

- Money top-left: banknote icon and the balance in Lilita One, no "$". Bag (count/capacity, amber when full) and
  lamps (available/owned, ∞ in the Ground Lab) bottom-left above the battery bar, so the "can I keep digging?"
  readouts sit together.
- Battery: a rounded bar with a bolt, no percentage or band text. The return estimate survives as the bar's colour
  (teal, amber, red; blue when unlimited) and the existing centred LOW FUEL / FUEL CRITICAL / FUEL EMPTY banner.
  "FUEL AT COMPUTER" stays as a small line above the counters, only near the computer.
- Removed: the panel, shovel level and cut, reach, depth (and `FpsPlayer.DisplayDepth`, which only it used), the
  lamp and mark key hints. The frozen detector, admin line, prompts, feedback and banners are unchanged.
- Icons: no owned pack has UI icons, so `HudIcons` draws four flat vector glyphs (card `art/hud-icons`); a bought
  icon set can replace them.
- Concept `08` §1 updated. The battery test now checks the bar's classes and fill instead of the label.
- Then, the user: "make the bar vertical". The bar stands upright (filling from the bottom, bolt at its foot) with
  the bag and lamp counters stacked beside it.

## Iteration 3 (2026-10-10): own palette, title backdrop, shop

The user: the bar's fill is "too rounded, like a bar inside the bar"; "don't copy the colours 1 to 1"; remove
"FUEL AT COMPUTER"; redesign the shop; the title wastes space (compare *A Game About Digging A Hole*: big logo,
menu under it) and the reference works because the axe is the subject, "in our case people look at mud ... a
cave or the river?".

- Palette: worksite slate, bone and hi-vis orange throughout (logo, menus, settings, dialogs, shop, HUD), in place
  of the reference's beige, yellow and teal. Battery fill is flat (the rounded case clips it): bone, then orange,
  then red; the money icon is sage bills with an orange band.
- "FUEL AT COMPUTER" and `SurfaceRecharge.IsNearby`, which only it used, are gone.
- Title: logo stacked SOMETHING / DOWN / THERE (bone over large orange), menu centred under it at the bottom of
  the left column, as the Digging-a-Hole title fills its side.
- Backdrop: the salvage crane over the fenced dig site, jib across the sky, cliffs behind, framed in the right two
  thirds (`TitleView`: pose swapped onto the view camera only while it renders, field of view 50, Gaussian depth
  of field from 60 m). Rejected: a cave (none exists before a game is made; the title shows a fresh site), the
  waterfalls (rock walls block every low view of them), the lake (pretty but no subject).
- Shop: slate card with an orange top edge, header with the banknote icon and balance, rows as tiles with orange
  progress pips and orange price buttons; sell rows as tiles, Sell all orange. Structure, names and behaviour
  unchanged.
- The description panel shows the hovered row, falling back to the focused row when the pointer leaves.

## Iteration 4 (2026-10-10): consistency, shop list, no homemade icons

The user: the logo's mixed faces and colours "feel off"; the dialog's orange top edge is awkward (full border or
none); title centred but pause left-aligned; the shop has too much text and cramped padding (names pressed to
the tile edge, "Tool 1/12"); the money icon is ugly and "never create your own icons", recommend icon packs
first; the battery outline is too strong; hover must not move words; add a button to the main menu. Reference:
the *Chopping Trees* shop (icon tiles, name, bar, one stat change, big price, no level shown).

- Logo: SOMETHING / DOWN / THERE all orange Lilita with the slate outline; only the first line is smaller.
- Borders: the orange top edge is gone from dialogs and the shop; the help panel's current choice is a plain
  tile (no left stripe).
- Pause uses the title's column: PAUSED over the centred entries. Hover only recolours (no scale or slide).
- Shop: one list (tracks, then services) of roomy tiles: NAME, a progress bar (owned/levels, in place of the
  level counter and pips), one short stat ("Cut 0.46 m → 0.51 m", "Holds 10 → 15", "Fuel 100 → 150",
  "Lift 8 m/s → 9 m/s", "Fuel 55 → 100", "Lamps 4 → 5"; "Shovel → Drill" at the milestone), large orange price.
  Header: BALANCE $n. Column headings removed.
- `HudIcons` and its card are deleted. The HUD shows "$n", BAG and LAMPS captions with numbers and a FUEL
  caption under the bar, which lost its outline.
- Pause Main Menu: `WorldSaveController.RequestMainMenu` saves as quitting does (the same unsaved-exit prompt
  after a write failure), then reloads the scene to the title; in the Ground Lab it leaves the lab.

## Acceptance Criteria

- Title: logo top-left, entries as plain capitals, Continue greyed without a save, Ground Lab only in development
  builds, focus and Back-from-settings land as before.
- Settings fill the screen; every category shows rows with working selectors, sliders and bindings; Left/Right,
  arrows, Enter and clicks change values; the panel follows focus; Escape leaves in one press; display changes
  still raise Keep/Revert.
- Pause shows only its heading and entries; New Game asks first, Cancel/Esc returns to pause, confirming starts a
  fresh world with the old one archived.
- Pause and dialogs readable over bright and dark ground; focus loss shows the bare world.
- HUD: only money, bag, lamps and the battery bar; the bar turns amber and red with the return estimate.
- Station, inventory and admin unchanged.
