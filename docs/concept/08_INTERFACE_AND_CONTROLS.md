# 08 — Interface and Controls

## 1. HUD

Minimal by design. The HUD answers exactly one question: *can I keep digging?*

| Element | Behavior |
|---|---|
| **Depth** | Current depth below the surface rim |
| **Bag** | Count / capacity; turns a warning color as it fills |
| **Battery** | Current charge; the shared dig + jetpack resource |
| **Return warning** | Adaptive safe / risky / critical estimate based on depth and ascent energy |
| **Detector** | Diegetic tool reaction plus a subtle screen-edge direction hint; silent by default (optional accessibility audio toggle available) |

Not on the HUD: minimap, compass, ore counters, objective list, damage numbers, news ticker, or any
permanent tutorial text.

## 2. Object inspection & bag handling

- **No inventory screen**. The bag is abstract; the HUD shows capacity.
- **Full bag behavior:** A full bag simply prevents new pickups. Uncovered items remain safely sitting in the world. Uniques and ending parts consume **zero bag slots**.
- Sell ordinary finds at the surface computer. Special exhibits and keys are unsellable.
- The computer opens selling for a carried haul, switches directly to upgrades after selling
  the last item, and opens upgrades immediately when there is nothing to sell.
- Its interaction prompt is simply **Use**, with no key prefix.
- Inspect objects in the world and on their displays. Placed uniques always allow story rereading.
- No stats, equipping, sorting or discard menu. Looking never drains the battery.
- **Price on hover remains undecided:** a small fixed sale price could appear once a sellable find
 is exposed enough to collect. It must not reveal hidden objects, price unsellable items or delay
 common pickups. See [Open Questions](13_OPEN_QUESTIONS.md#interface-and-ending).

## 3. Pause menu

Direction (final layout later): Resume · Save & Load · Settings · Exit to Title, with save status
visible. New Game is on the title screen; replacing an occupied world needs a clear overwrite warning.
Correct back behavior is mandatory: ESC/B closes the current menu and never traps input. Settings
persist immediately and across launches.

## 4. Controls

| Input | Default | Notes |
|---|---|---|
| Dig / use tool | Hold left mouse / trigger | Hold-to-dig; toggle mode available |
| Jetpack | Space / A or bumper | Simple input; stable handling; rebindable |
| Crouch (precision) | Ctrl / stick click | Held; no stealth or stamina |
| Interact (machines, placement) | E / face button | Context-obvious prompts |
| C4: throw / detonate | Rebindable pair | Multiple charges; remote detonation |
| Photo mode | Rebindable | Pause-only |

Rules:

- **Every action is fully rebindable** on every device.
- **Controller parity is mandatory:** every screen, including shop and display placement,
 works with a controller; glyphs swap automatically.
- **Left-handed preset** mirrors mouse buttons and updates prompts.
- Sensitivity, invert, deadzone and hold/toggle options exist per action and per input device.
- Optional gyro for fine control.
- No action in the game requires rapid repeated input, simultaneous multi-button holds, or mashing.

## 5. Feedback rules

- Every pickup has visible, audible feedback; the player never wonders whether something was
 collected (the "apparently I collected it but didn't see it" failure is banned).
- Detector feedback has a visual channel; the game is fully playable muted.
- Readability is never color-only: shapes, icons and labels back up every color cue.

## 6. Photo mode

Pause-only and simple: hide HUD, adjust FOV, apply basic filters, toggle a watermark. **No free
camera** (there is no player model to frame). The player composes from their own view — which is the
point: the hole and the find are the subject.

No postcard export or separate sharing system; the existing photo mode stays.

## 7. Save system (player-facing)

- **Autosave** continuously at a measured interval and on events (sales, upgrades, recoveries).
- **Three manual save slots** for different worlds/seeds.
- **Asynchronous, non-blocking save serialization:** save writes run in background threads or
 delta-diff chunks with a prototype frame-impact target of <100 ms and no perceptible hitch.
 Saving a large voxel hole must preserve responsive input and smooth digging. Total background save
 duration is a separate measure from a visible frame hitch.
- **Independent rolling backups:** several backup generations are written at different save events,
 and a corrupted active save can never take the backups down with it. Loading a damaged save falls
 back to the newest valid generation and says so plainly — the world is never silently reset.
- Save status is visible but unobtrusive; no save spam.
- Steam Cloud comes later; the save format is designed so it can be added without changes.
- Loading restores the exact hole, bag, display and progression — never fresh terrain with old
 purchases. Resume at the saved position with the same charge and loot; no reload travel or refill.

## 8. Settings that must exist (summary)

Motion comfort (FOV, bob, comfort preset), controls (rebinding with a one-click reset to defaults, sensitivity, handedness),
audio (ambience/SFX levels, mute), UI (scale where applicable), gameplay toggles (hold/toggle dig; additional assist if useful), and save management. Options persist immediately; every effect that exists has a
corresponding control.

Render resolution defaults to 100% of the selected output resolution. The display list follows
the player's supported monitor modes, including 4K and higher when reported, even if their
desktop is currently set lower. Display changes retain timed Keep/Revert confirmation.

Graphics exposes render resolution, shadow quality (including Off), anti-aliasing, texture quality,
and texture filtering, with a category reset. Lower graphics settings affect presentation only;
they never reduce finds, physics accuracy, or the darkness of deep tunnels. Texture options apply
to replacement mipmapped assets through the renderer. Grass-specific controls follow the chosen
grass implementation.

## 9. Accessibility pointer

The full suite is specified in [Accessibility and Comfort](10_ACCESSIBILITY_AND_COMFORT.md): motion comfort defaults, motor
assists, colorblind palettes, sound subtitles, zero-pressure design and pause-anywhere behavior.
