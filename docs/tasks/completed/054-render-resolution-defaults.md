# 054 — Render Resolution Defaults

**Status:** complete. Rendering is fixed at 100%, and borderless follows the current monitor's desktop size and aspect ratio. Fullscreen/windowed modes retain supported output resolutions with timed Keep/Revert confirmation.

## Objective
Default to native rendering and make resolution choices reflect supported display modes, including 4K where available.

## Concept reference
Concept 08 settings and concept 10 performance/comfort defaults.

## Live code analysis
- `GamePreferenceValues` defaults render scale to 150%, despite the URP asset itself using native resolution.
- `UnityGameSettingsPlatform` already enumerates display modes, but removes modes larger than the current desktop resolution. A supported higher-resolution mode can disappear when the desktop runs lower.
- Display preview already has a Keep/Revert timeout and records the actual applied mode.
- This machine currently reports a maximum output mode of 2560 × 1440. Internal 4K rendering and physical 4K output are separate choices.

## Architecture changes
- Set the render-scale default and Graphics reset to 100%; reuse existing preference/platform ownership.
- `DesktopWindow.ResolutionOptions` preserves reported supported modes independently of the current desktop size. `UnityGameSettingsPlatform` uses this policy while retaining safe fallback/window sizes and deduplicating refresh-rate variants.
- 4K means physical output for other players with compatible displays. Keep this monitor's genuine mode list; do not invent unsupported output modes.

## Edge cases
An empty mode list, a lower desktop resolution on a higher-resolution monitor, duplicate refresh rates, unsupported output modes and existing saved device choices. World saves and recovery scenes are unaffected.

## Acceptance criteria
- Fresh/default graphics use 100% render resolution.
- Supported 4K output is not hidden by a lower current desktop resolution.
- Display preview and rollback still work; existing device choices remain readable.
- Relevant settings checks pass, runtime behaviour is verified, and the Windows player is rebuilt.

## Verification
- Clean C# compilation; display policy checks 11/11 and preference checks 17/17 passed.
- Runtime settings PlayMode check passed, including 100% on the cloned URP renderer and restoration of system settings.
- Simulated reported modes retain 4K, ultrawide and 8K with a lower desktop mode; this machine reports a 1440p maximum, so physical 4K output was not tested here.
- Windows player rebuilt successfully with zero errors. The sole build warning is the existing optional Pipeline runtime configuration notice; MainGame remains clean.

## Full-window startup iteration
- Remove the render-scale preference and control; the runtime URP clone always uses native rendering.
- Resolve borderless startup and previews against the window's monitor, ignoring saved window dimensions. Resolution selection is available in fullscreen/windowed modes; borderless displays Desktop. Keep native-resolution player startup enabled in the scene-authoring utility.
- Verify saved ultrawide dimensions cannot impose a different aspect on borderless, timed rollback still restores a window, and native startup fills the monitor. Graphics defaults and their verification are owned by [055](055-graphics-settings.md).
- Verification: display/preference and live Keep/Revert checks pass; the rebuilt Windows player's client and monitor both measure 2560 × 1440, with a full-screen startup image and no bars. The current Editor Game view is already Free Aspect. The originally reported bars were not reproduced with this device's starting preferences; borderless now rejects the mismatched-size path covered by regression testing.
