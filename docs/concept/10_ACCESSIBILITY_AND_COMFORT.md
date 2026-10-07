# 10 — Accessibility and Comfort

Accessibility here is core design, not a patch: this is a game about a body digging a hole, and the
body must be respected. Every reviewed digging game in the reference set drew
avoidable complaints about hands, eyes, motion or pressure. This one does not.

## 1. Motion comfort (the genre's biggest issue)

| Setting | Default | Options |
|---|---|---|
| Field of view | 90° | 60–110°, unit toggle |
| Camera shake | Off | No digging shake |
| Head bob | Off | Off / subtle / on |
| Comfort preset | Available at first launch | One click applies FOV, no bob, no shake, soft transitions |
| Forced roll / camera lean | Never | Not a feature |
| Jetpack camera effects | None by default | Each effect has a toggle where it exists |

Design rules:

- No screen shake from digging.
- No cinematic camera takeovers, no rotating elevator shots on the critical path.
- Camera motion is smooth at any frame rate.
- Start limited to the monitor's refresh rate, including loading. Settings may select VSync or another
  limit; unlimited rendering is an explicit choice. An unfocused (paused) window redraws at most 30 FPS.
- A player who selects motion comfort in the optional first-launch preview gets the comfort preset
  applied automatically.
- An optional first-launch camera preview ("look around — does this feel right?") lets the player set
  FOV and comfort settings before the first dig.

## 2. Motor accessibility

- **Hold-to-dig** runs while held; **toggle** runs until toggled off. Any additional auto-dig assist
 needs a distinct purpose beyond these two inputs.
- Every hold action has a toggle equivalent.
- **One-hand presets** for left-only and right-only play, on keyboard/mouse and controller.
- **Full rebinding** of everything, including menus.
- No QTE, no mashing, no rapid double-inputs, no precise timing anywhere in the critical path.
- Generous input buffering: inputs during animations are never dropped.
- Aim/placement assistance: snap-to-valid-surface placement for lamps and markings, generous interaction
 radii, no pixel-perfect hotspots.
- No health management or fall death: ordinary falls give harmless landing feedback, without battery
 loss, input lock or surface recovery. Learning movement does not cost progress.

## 3. Vision

- **Colorblind palettes** for materials, tells, ore, UI and (while it exists) detector cues, plus a custom palette option.
- **Tells never rely on colour alone:** backfill, geode shells and lenses differ by stones, grain and
  chunk shape, and the sudden change in dig speed is felt, so they work for colour-blind players and
  in the dark.
- **Shape + label redundancy** everywhere: no information is color-only.
- Contrast and brightness options plus an optional ambient-light floor; the game stays readable at both ends of the display range without removing the need for lamps.
- **Subtitles for all significant sounds**, including ambient tells, with size/background
 options chosen in the text pass; the captions themselves are required.
- UI scale/font scale and screen-reader support remain optional later work.
- Detector feedback is visual by design; the game is fully playable muted. An optional audio ping
  and high-contrast cues are added only if the detector survives its playtest (it is frozen until then).

## 4. Hearing

- All dialogue-equivalent text is on screen.
- All informational sounds (station feedback, placement confirm, pickup) have visual
 counterparts.
- Ambience and SFX volume controls; a mono option is a planned addition if the audio pass supports
 it.

## 5. Cognitive and emotional comfort

Zero-pressure design:

- **Pause anywhere**, any time; no online requirement.
- **No timed content** in normal play (no countdowns, no day/night deadlines, no decay).
- **No missables**: story objects remain findable; nothing expires.
- **No FOMO**: no daily rewards, no limited events, no login streaks.
- **No jumpscares, no horror, no combat**. Dark areas exist but are calm, never scary.
- Intensity warnings are unnecessary by content policy, but a brief note about dark areas appears in
 the optional first-launch settings preview.
- The return-power warning is informational, never panicky; recovery always protects the player's
 finds.

## 6. Difficulty

**One single difficulty, tuned fair**:

- The only failure state is running out of battery, and it costs a fee, not loot.
- No scaling enemies, no punishment for exploring, no twitch requirements.
- Available assist options (including toggle dig) do not disable achievements or content.
- The game has no "easy mode" because it has no unfair mode to compensate for.

## 7. Save safety as accessibility

Losing progress is a disability-comfort issue as much as a technical one:

- Autosave continuously; 3 manual slots; loading restores the exact hole.
- No save loss, no terrain reset, no "fresh world with old purchases".
- Cloud saves arrive later without changing the save format.

## 8. Verification

- Motion-sensitive testers review every zone before content lock; their sign-off is a release gate.
- Assist-mode (toggle dig, muted, colorblind palette, any additional supported assists) full run must complete normally.
- All station interactions verified with controller only and with one hand.
- No content audit failure: no horror, no flashing, no forced camera motion on the critical path.
