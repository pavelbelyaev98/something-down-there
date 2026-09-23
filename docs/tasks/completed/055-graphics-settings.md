# 055 — Graphics Settings

**Status:** complete. Persistent shadow, MSAA, texture-quality and filtering controls use balanced defaults and category reset, with rendering fixed at 100%. Shadows adjust only the runtime URP clone while excavation daylight stays independent.

## Objective
Expose useful, persistent performance controls in the existing Graphics tab, with clear labels and native rendering by default.

## Concept reference
Concept 08 settings and concept 10 performance/comfort controls. Options affect presentation, never excavation, discovery density or physics.

## Live code analysis
- `ToolkitDeviceSettings.BuildGraphics` is a placeholder; shared rows already support mouse, keyboard/controller navigation and scrolling.
- `GamePreferences` owns device JSON, validation, category reset and persistence boundaries. Render resolution, MSAA, texture mip limits and anisotropic filtering already apply through `UnityGameSettingsPlatform`.
- The platform clones the active URP asset and restores system settings on disposal. Authored shadows currently use a high-resolution atlas and four cascades.
- `ExcavationDaylight` controls underground light independently from shadow maps. Grass uses a separate procedural renderer; asset-specific density controls are outside this pass.

## Architecture changes
- Replace the placeholder with Render resolution (50–150%, default 100%), Shadows (Off/Low/Medium/High), Anti-aliasing (Off/2x/4x/8x), Texture quality (Low/Medium/High), and Texture filtering (Off/Standard/High).
- Show brief descriptions of resolution and performance trade-offs. Enable Graphics reset and retain the existing navigation/persistence lifecycle.
- Add one bounded shadow-quality preference. Missing fields in old device JSON retain the current High appearance; reset affects only graphics.
- Apply shadow tiers using public URP atlas-resolution, cascade-count and distance properties on the runtime clone. Off uses zero shadow distance; other tiers reduce atlas/cascade work without touching underground daylight.
- Keep texture controls on Unity's global mip/filter settings so replacement mipmapped textures inherit the policy. No texture imports or source assets change.

## Edge cases
Legacy/corrupt preferences, unavailable URP, settings toggled repeatedly, resetting graphics without changing display/audio, native-size UI at reduced render resolution, low-resolution menu scrolling, and restoration on leaving Play Mode. Existing world saves/recovery scene remain untouched.

## Acceptance criteria
- All five controls apply immediately, survive normal preference persistence and reset correctly.
- Settings match the existing menu style, support navigation and are readable in a small window.
- Shadow Off genuinely disables shadow-map work while the excavation daylight field remains active; returning to High restores the authored presentation.
- Relevant preference/runtime checks pass; source rendering assets remain unchanged and a fresh Windows player is built.

## Verification
- C# compilation clean; preference checks 17/17 and settings PlayMode checks 8/8 passed, including legacy JSON, save/reload, reset isolation and renderer restoration.
- Actual Graphics controls and keyboard-submit Reset exercised in a disposable additive MainGame with in-memory preferences and no save owner. Menu captures reviewed at 720p with default and reduced rendering settings; UI stays sharp.
- Live shadow-pass inspection confirmed Off takes the empty-shadow path and High restores the authored four-cascade atlas. The excavation daylight revision and sampled underground ambient remained unchanged.
- Windows player rebuilt successfully with zero errors; the sole warning is the existing optional Pipeline runtime-configuration notice. MainGame restored clean; no source rendering assets or player saves changed.

## Balanced defaults iteration
- Replace quality-heavy startup/reset defaults with Medium shadows and 2× MSAA; retain full-resolution textures, High filtering and the existing frame cap. Rendering stays at 100%, with the scale row, help text and persisted field removed.
- Apply the requested graphics reset to this device through `GamePreferences`, retaining controls, audio and world saves. Borderless uses the monitor's desktop size, as specified in [054](054-render-resolution-defaults.md).
- Verify preference validation/reset/persistence, renderer ownership and fixed scale, display Keep/Revert, actual startup framing and the remaining Graphics controls before the Windows rebuild.
- Verification: preference/display tests and focused settings/navigation PlayMode checks pass. Live review confirms the four remaining controls and native render scale, with the requested defaults applied to player and Editor preferences. Windows build succeeds with no script warnings or errors; only the existing optional Pipeline runtime warning remains.
