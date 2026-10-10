# 117 — Menu Redesign: Title, Pause, Fullscreen Settings

**Status:** on trial: the title and pause stand on the world with a yellow sign-painted logo and a plain-word
menu (pause: only the menu, with New Game); settings is a full-screen paper sheet with tabs, in-place ◀ value ▶
selectors and a description panel; dialogs are a paper card. Lilita One and Barlow Condensed (OFL) set the type.
Awaiting the user's playtest.

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

## Not done (offered)

- **Hero shot on the title:** the reference's main image is a prop close-up (axe in stump) over a blurred scene.
  Ours keeps the spawn view, mostly dirt foreground. A title-only shot (e.g. the shovel stuck in a spoil heap by the
  plot, depth-of-field background) would need its own camera pose and must not move the graphics test's view.

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

## Acceptance Criteria

- Title: logo top-left, entries as plain capitals, Continue greyed without a save, Ground Lab only in development
  builds, focus and Back-from-settings land as before.
- Settings fill the screen; every category shows rows with working selectors, sliders and bindings; Left/Right,
  arrows, Enter and clicks change values; the panel follows focus; Escape leaves in one press; display changes
  still raise Keep/Revert.
- Pause shows only its heading and entries; New Game asks first, Cancel/Esc returns to pause, confirming starts a
  fresh world with the old one archived.
- Pause and dialogs readable over bright and dark ground; focus loss shows the bare world.
- Station, inventory and admin unchanged.
