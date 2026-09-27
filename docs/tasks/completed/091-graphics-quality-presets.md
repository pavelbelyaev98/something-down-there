# 091 — Graphics Quality Presets and Automatic Defaults

**Status:** complete. Graphics offers presets (Low/Medium/High/Ultra, Custom), a one-click PC test, render resolution with FSR, view distance, FXAA/MSAA and ambient occlusion; a player's first launch measures the PC and stores a recommended preset that the Graphics reset returns to. The frame limit follows the monitor, unfocused players redraw at 30 FPS, and every row acts through global pipeline, camera and quality state.

## Objective
Every PC should start with settings that keep play smooth, above 60 FPS and toward the monitor's rate on high-refresh displays, while strong PCs keep today's full look. Add the missing performance settings, quality presets and a measured auto-configure (first launch and on demand). Frame limits follow the monitor. New content must follow these settings without per-asset work.

## Concept reference
- `08` §8 (updated here): graphics settings persist immediately and affect presentation only; they never change finds, physics or the darkness of deep tunnels, and lamp occlusion survives every tier. User direction replaces "rendering stays at 100%, no render-scale setting": render resolution returns as an explicit performance option (default 100%), with FSR upscaling.
- `10` (updated here): the default frame limit follows the display's refresh rate instead of a fixed 144; VSync and other limits stay explicit choices.
- `09`/`03`: High is the accepted look (grass to the full play-area distance, canyon detail, soft nearby shadows). Lower tiers may shorten grass and terrain detail.
- `14`: stable frame pacing; auto-configure is a one-off measurement, never a recurring cost.

## Live code analysis
- `GamePreferences`/`GamePreferenceValues` own device settings (`game-v1.json`). `UnityGameSettingsPlatform` applies them to a runtime URP clone plus `QualitySettings`, restoring system state on dispose. Graphics already has sun shadows (Off–High), MSAA (Off/2/4/8), texture quality and filtering. Display has VSync and a fixed list of FPS limits (default 144).
- Measured costs (090, RX 9060 XT, 1440p): 75% render scale saves ~25%; grass to 60 m saves ~1.0 ms and off ~1.25 ms; terrain basemap 150 → 75 m saves 0.5–0.8 ms; sun shadows High → Off 0.6–0.85 ms; contact shading 0.15–0.3 ms. The flying view is the heaviest (~18% above the spawn view).
- Contact shading is a `ScreenSpaceAmbientOcclusion` feature on the shared renderer asset. Toggling it would mutate a project asset, and the URP renderer list is internal. The supported per-camera route is a second renderer in the asset's list, selected by `UniversalAdditionalCameraData.SetRenderer`.
- Terrain draw settings can be overridden globally through `QualitySettings.terrainQualityOverrides`; these apply to every `Terrain`, including future ones.
- The camera belongs to `FpsPlayer`, which creates the platform. The title screen renders the world behind the startup page, and the excavation terrain is ready before a save loads.
- Frame timing stats are enabled only for validation builds.

## Architecture
- **Values** (`GamePreferenceValues`, current format only): `AntiAliasing` (0 Off, 1 FXAA, 2/3/4 = 2×/4×/8× MSAA) replaces `Msaa`; new `RenderScale` (50–100%), `ViewDistance` (0 Low, 1 Medium, 2 High) and `AmbientOcclusion`. `FrameLimit` 0 means "Display" and is the default. Recommendation fields (`RecommendedPreset`, `RecommendedScale`, `GraphicsTuned`) record the last auto-configure result; the Graphics reset restores that recommendation.
- **Presets** (`GraphicsQuality`): Low / Medium / High / Ultra set shadows, AA, view distance, contact shading, texture quality and filtering. Render resolution is independent of presets. The preset row shows the matching preset, or Custom when any row differs.

  | | Low | Medium | High (= current look) | Ultra |
  |---|---|---|---|---|
  | Sun shadows | Low | Medium | High | High |
  | Anti-aliasing | FXAA | 2× | 2× | 4× |
  | View distance | Low | Medium | High | High |
  | Contact shading | Off | On | On | On |
  | Textures / filtering | Medium / Standard | High / High | High / High | High / High |
- **View distance** via terrain quality overrides. High applies none, so the authored terrain covers the whole play area. Medium shortens grass and props to 120 m at 80% density. Low uses 70 m at 60% density, terrain full-detail texturing to 80 m and coarser terrain geometry. Scenery LOD bias stays at 1, so play-area scenery never culls on any tier.
- **Rendering** (`UnityGameSettingsPlatform.ApplyRendering`, shared with the environment benchmark): render scale plus FSR below 100%; MSAA or camera FXAA; renderer 0 (contact shading) or 1 (none); sun shadow tier; view-distance overrides. Every change is restored on dispose.
- **Frame pacing:** "Display" resolves to the window's monitor refresh rate (fallback 144), including the pre-scene startup cap. In players, an unfocused window renders at most 30 FPS. The game already pauses there.
- **Auto-configure** (`GraphicsAutoTuner`, owned by `FpsPlayer`): on a player's first launch at the title (world ready, focused), and from a Graphics-tab button. It previews candidates uncapped and renders a fixed survey view (the benchmark's flight view, the heaviest) through camera-render callbacks, so gameplay never sees the camera move. It samples `FrameTimingManager` (GPU and CPU, with a frame-interval fallback) after a warm-up. Candidates run High → Medium → Low at 100%, then Low at 85/75/67/50%. It chooses the first preset meeting the preferred rate (the monitor's rate clamped to 60–90). Failing that, the highest preset holding 60; failing that, the first resolution step that holds 60. Measurements carry 10% headroom. Focus loss aborts and retries later. The title buttons wait while it runs.
- **Renderer variant:** `GraphicsQualitySetup` keeps `SomethingDownThereUniversalRenderer NoContactShading.asset` a serialized copy of the main renderer without features, registered as renderer 1. It also enables frame timing stats. The ground/nature setups call it after changing renderer settings.
- **UI:** Graphics tab rows, in order: Quality preset, Auto-configure, Render resolution (slider, 5% steps), View distance, Sun shadows, Anti-aliasing, Contact shading, Texture quality, Texture filtering, plus short help. Display tab: FPS limit gains "Display (N)", 75 and 100.
- **Content rule** (documented in `unity/readme.md`): presentation settings act through global mechanisms, so new content inherits them. That means the pipeline and camera for resolution, AA, contact shading and shadows; `QualitySettings` for textures and terrain overrides; the terrain for ground cover. Authors keep mipmaps on, paint ground cover as terrain details, give scenery LOD groups, and never hard-code per-object quality.

## Edge cases
- Stale or unknown JSON uses current defaults. Rows edited during a run are blocked.
- VSync or a driver-forced cap can hide headroom: GPU/CPU timings are used, not the frame interval.
- Machines where GPU timing is unavailable fall back to the frame interval.
- A missing renderer variant or camera leaves contact shading on and AA as MSAA.
- Editor sessions never auto-run; the button works in play mode. Tests use in-memory stores and non-applying platforms.
- Refresh rates unknown or below 30 fall back to 144.

## Acceptance criteria
- A fresh profile auto-configures once, and the result is visible in the Graphics tab. Auto-configure picks High on the reference PC.
- Each preset measurably reduces cost on the native benchmark, and High renders identically to 090.
- Every row applies immediately, persists, resets to the recommendation, and restores system and renderer state on dispose.
- No source asset is mutated at runtime.
- Frame limit follows the monitor by default; unfocused players cap at 30.
- The docs state how new content follows the settings.
- Relevant tests pass; fresh Windows player built.

## Verification
- Native presets, fresh-install defaults, RX 9060 XT at 1440p, GPU ms (dig site / flying / shore / dug pit):
  - Ultra 8.19 / 9.67 / 7.58 / 5.99
  - High 7.09 / 8.29 / 6.71 / 5.26
  - Medium (then 2× MSAA) 6.56 / 7.74 / 6.19 / 5.23
  - Low 4.76 / 5.64 / 4.75 / 4.25
  - Low at 75% 3.66 / 4.23 / 3.65 / 3.67
- Individual rows: view distance Low −1.2 to −1.5 ms, 75% FSR −1.5 to −1.9 ms, FXAA instead of 2× MSAA ~−1.1 ms, ambient occlusion Off −0.15 to −0.2 ms. Medium now uses FXAA, since MSAA alone cost as much as the whole old Medium step.
- Every setting takes effect in a player: view distance drops terrain-detail triangles, AO Off selects the no-feature renderer, and FSR and FXAA engage. Low renders correctly (flight capture reviewed).
- First launch in the real build:
  - The first attempt picked Medium because High was measured during startup shader and terrain-detail streaming. The survey now primes for 90 frames on the first candidate, and the choice uses the median.
  - Log after the fix: "Graphics auto-configure at 100 Hz: High@100% 8.3 ms; chose High@100%". This matches the benchmark, and the recommendation persisted.
- The Editor and WMI reported this desktop at 60 Hz, while the player's window reported 100 Hz. Frame limits use the player's value.
- Tests:
  - EditMode 297/297, including presets, preview isolation, reset-to-recommendation and the auto-configure decision table.
  - PlayMode FpsUiInput 24/24, including runtime rows and restoration, plus every other selected suite.
  - A stale guard asserting no `qualityPreset` row was removed.
- Windows player built with only the existing collision pre-bake and Pipeline advisories.

## Iteration: no blurry distance at High
- Playtest: distant grassy terrain looked blurry and sharpened on approach. The terrain drew from its basemap beyond the authored 150 m. High now overrides the basemap distance to 1000 m, covering the whole canyon; Medium keeps 150 m and Low 80 m (`GraphicsQuality.BasemapDistance`).
- Measured (RX 9060 XT, 1440p, GPU median): dig site +0.54 ms, shore +0.56 ms, flight +1.03 ms. Also measured, not adopted: LOD bias 1.5 or 2 for scenery (+0.2–0.6 ms). It gave only subtle cliff-geometry gains; crossfaded switches at 50/80/120 m stay.
