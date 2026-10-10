# 08 — Interface and Controls

## 1. HUD

Minimal by design. The HUD answers exactly one question: *can I keep digging?*

| Element | Behavior |
|---|---|
| **Battery** | Current charge as a bar; the shared dig + jetpack resource |
| **Return warning** | Adaptive safe / risky / critical estimate based on depth and ascent energy, shown as the battery bar's colour plus a centred low-fuel banner |
| **Bag** | Count / capacity; turns a warning color when full |
| **Money** | Current balance |
| **Lamp kit** | Available reusable lamps |
| **Reticle** | A plain tiny, softened white dot at the centre of the view: no rim, glow or shape; visible without drawing the eye |
| **Pickup note** | "+Coal" above the bag for each find taken, fading after a moment |
| **Uniques** | A trophy with secured / total: the save's uniques taken, on the crane's rope or kept at camp (user, 2026-10-10) |

Only these (and the frozen detector) are shown: icons and numbers, no text readouts. Depth, tool stats
and key hints are not on the HUD (user, 2026-10-10).
| **Detector** (frozen) | Kept as built until the detector-off playtest decides whether ground tells replace it ([Discoveries](05_DISCOVERIES.md#2-the-detector)). Separate compact HUD with three signal bars based on aim alignment: strongest when looking directly toward a nearby buried find. Hidden when looking away, out of range or once any part is uncovered. No direction/height hints; the aiming reticle stays independent. Silent by default; optional accessibility audio remains planned. |

Not on the HUD: minimap, compass, ore counters, objective list, damage numbers, news ticker, or any
permanent tutorial text.

## 2. Object inspection & bag handling

- **No inventory screen**. The bag is abstract; the HUD shows capacity.
- **Full bag behavior:** A full bag simply prevents new pickups. Uncovered items remain safely sitting in the world. Uniques and ending parts consume **zero bag slots**.
- Sell ordinary finds at the surface computer. Special exhibits and keys are unsellable.
- The computer opens selling for a carried haul, switches directly to upgrades after selling
  the last item, and opens upgrades immediately when there is nothing to sell.
- Its interaction prompt is simply **Use**, with no key prefix.
- Inspect objects in the world. Recovered uniques always allow story rereading where they stand.
- No stats, equipping, sorting or discard menu. Looking never drains the battery.
- **Price on hover remains undecided:** a small fixed sale price could appear once a sellable find
 is exposed enough to collect. It must not reveal hidden objects, price unsellable items or delay
 common pickups. Decided from the playable-slice playtest; see [Open Questions](13_OPEN_QUESTIONS.md#interface).

## 3. Pause menu

Resume · Settings · New Game · Main Menu · Save and quit; Save & Load and save status visible come
later. New Game is on the title screen and in the pause menu; replacing an occupied world
needs a clear overwrite warning. The pause menu is only the menu: no controls list over the game
(controls live in Settings).
Correct back behavior is mandatory: ESC/B closes the current menu and never traps input. Settings
persist immediately and across launches.

## 4. Controls

| Input | Default | Notes |
|---|---|---|
| Dig / use tool | Hold left mouse / trigger | Hold-to-dig; toggle mode available |
| Jetpack | Space / A or bumper | Simple input; stable handling; rebindable |
| Crouch (precision) | Ctrl / stick click | Held; no stealth or stamina |
| Interact (machines, placement, extraction marking) | E / face button | Tap for machines/placement; hold on any visible part of an exposure-ready unique to bolt on a lifting eye for the crane |
| Work lamp | L | Preview; primary places, secondary cancels, Interact retrieves |
| World marking | M | Preview arrow/home/return-here; repeat to change symbol, Interact erases |
| Rotate placement | R | Rotate the current lamp or marking preview |
| Photo mode | Rebindable | Pause-only |

Rules:

- **Every action is fully rebindable** on every device.
- **Controller parity is mandatory:** every screen, including the shop,
 works with a controller; glyphs swap automatically.
- **Left-handed preset** mirrors mouse buttons and updates prompts.
- Sensitivity, invert, deadzone and hold/toggle options exist per action and per input device.
- Optional gyro for fine control.
- No action in the game requires rapid repeated input, simultaneous multi-button holds, or mashing.

## 5. Feedback rules

- Every pickup has visible, audible feedback; the player never wonders whether something was
 collected (the "apparently I collected it but didn't see it" failure is banned).
- A find the dig takes shows no words, buried or free: no exposure percentage, "uncover more" or
 "hold to collect" (user, 2026-10-10); the player digs until it comes free and its pickup note
 appears. Only finds taken another way prompt: "E to take" in a chest, "hold to mark" on a unique.
- Detector feedback has a visual channel; the game is fully playable muted.
- Readability is never color-only: shapes, icons and labels back up every color cue.
- Recovery marking previews directly on the eligible aimed surface, shows hold progress, and becomes a fixed checked mark when confirmed. It follows the moving find and obeys world occlusion; menus and equipment placement suppress the hover preview.

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
 Saving a large voxel hole must preserve responsive input and smooth digging, including a heavily
 dug 150 m site. Total background save duration is a separate measure from a visible frame hitch.
 Meltopia's multi-second save freezes read as crashes and cost it player trust.
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

Rendering defaults to 100% of the selected output resolution. Render resolution (50–100%, FSR
upscaling) is the main performance lever for slower graphics cards and never the default on a PC that
keeps play smooth without it.
Start in borderless mode at the current monitor's desktop resolution, filling its aspect ratio
without cinematic bars. Borderless always follows the monitor; manual output-resolution choices
belong to fullscreen and windowed modes. Their display list includes reported 4K and higher modes,
even if the desktop is currently set lower. Display changes retain timed Keep/Revert confirmation.

Graphics exposes a quality preset (Low/Medium/High/Ultra; Custom after any individual change), a
one-click test of this PC, render resolution, view distance (how far grass, ground and object detail reach),
sun-shadow quality (including Off), anti-aliasing (FXAA or MSAA), ambient occlusion, texture quality
and texture filtering. On first launch the game measures the PC and picks the best preset that stays
above 60 FPS, reaching toward the monitor's rate (up to 90) where a lower tier allows it; resolution
drops only when even Low misses 60. High is the accepted look and strong PCs keep it. The category
reset returns to this recommendation. Lower graphics settings affect presentation only;
they never reduce finds, physics accuracy, or the darkness of deep tunnels. Lamp occlusion remains
active even with sun shadows disabled so light cannot pass through sealed ground. Texture options apply
to replacement mipmapped assets through the renderer. Grass-specific controls follow the chosen
grass implementation.

## 9. Accessibility pointer

The full suite is specified in [Accessibility and Comfort](10_ACCESSIBILITY_AND_COMFORT.md): motion comfort defaults, motor
assists, colorblind palettes, sound subtitles, zero-pressure design and pause-anywhere behavior.
