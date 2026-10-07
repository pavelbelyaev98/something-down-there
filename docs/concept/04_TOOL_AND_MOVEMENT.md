# 04 — Tool and Movement

## 1. One machine

There is exactly one excavation tool. It starts as an ordinary shovel and ends as a garage-built
absurdity. The player never switches tools; upgrades bolt onto the same object.

- **Ordinary baseline:** Begin with recognizable shovel scoops. Improvements strengthen the same shovel through level six; level seven adds continuous drill-like cutting, retained through level twelve. This is automatic progression, not a tool swap or a mode selection. Digging retains the shared battery budget throughout the progression.
- **Visible body:** The tool only — no hands visible.
- **Visual escalation (on hold):** For now the tool looks the same at every level of a stage: one shovel for
 levels one to six and one drill for seven to twelve, nothing bolted on per level. The shovel is a stylized
 western round-point shovel (strapped, riveted socket and a T-handle) at three quarters of its first size on
 screen; each stroke pries the soil loose (push in, lever the handle down) and then scoops it, the blade
 lifting clear, and the dirt disappears at the scoop. The drill is a hand mining drill (a jackhammer body with a
 spinning screw head sitting right on it, no shaft between). Per-level visual upgrades wait for the user, and are not added in the meantime.
- **Power escalation:** The machine grows faster than the ground gets tougher. Small early
 digs become large, fast late excavation. Revisit familiar ground and feel the difference; the
 payoff is what the machine can do, not just how it looks.
- **No zone resets your speed:** the tool ladder is tuned against the zones' main grounds
 ([zone rules](03_WORLD_AND_SITE.md#zone-rules)). Arriving in a new zone at the expected level never
 feels slower than the zone before; the drill (level seven) arrives around the third zone. Meltopia's
 second tool and its "blue snow" did the opposite and became the game's most common reason for a
 negative review: the player felt their whole investment had been wasted.
- **Late-game excavation vs silhouette reveal:** Powerful late-game cutters clear large volumes quickly. For small common items (bottles, ore), this is a benefit that skips tedious cleaning. For massive machinery or buried structures, even a huge cutting head exposes only a fraction, preserving the silhouette discovery loop. Precision crouch allows narrowing the cutting footprint when delicate carving is desired.

## 2. Digging input

- **Hold-to-dig is the default.** Continuous digging from the very first shovel; no click-per-bite.
- **Toggle mode** available; press once to start, once to stop.
- **Additional auto-dig assist** stays for implementation if it offers something beyond hold/toggle.
- **Full rebinding** for every action on keyboard, mouse and controller; left-handed preset;
 sensitivity options; optional gyro.
- No mashing, no QTEs, no rhythm inputs, anywhere in the game.

Holding repeats ordinary shovel scoops at the early levels. From tool level seven onward it bores
like a real bit (user, 2026-10-05): tip first, the way it is pointed (a slanted look makes a slanted
hole). Held on a spot, its tip opens the middle and its cone widens the hole around it into a clean
cone within about a second, then it takes a layer a cut; the shovel's scoops leave a flat floor.
Sweeping fresh ground digs slowly, since only the tip meets new ground. The visible rig communicates
this shovel-to-drill transition: scoops per stroke, then a spinning drill head; the motion always follows
the tool level (user, 2026-10-05: no admin override).

## 3. Automatic material adaptation (the mode model)

There is no mode button and no required switching. The machine reads the ground and changes
behavior automatically:

- **Through the final tier:** distinct behaviors cycle by material — a fast precise bite (Shave-like), a wide
 cheap scoop (Scoop-like), and eventually a blast head (Nozzle-like). The player sees and hears which
 head is active.
- **Soft preference, never a lock:** each material family clearly rewards one behavior (sand rewards
 the fast bite; hard rock rewards the blast head; loose fill rewards the wide scoop), but every
 behavior can dig everything. Visual/audio feedback shows the response the machine
 selected; it never asks the player to switch modes.
- **No late convergence:** the final head keeps distinct automatic material responses; it does not
 turn every material into the same vacuum action.
- **Upgrades improve all behaviors at once** — one shared tool upgrade level. No separate
 upgrade economy for a second tool.
- **Following a tell** uses these same automatic responses: digging into backfill or a geode's shell
 is just digging where the ground is different ([ground tells](03_WORLD_AND_SITE.md#4-grounds-and-their-tells)).
 No extra mode, input or required technique.

## 4. Upgrade tracks and the tool

The tool's own track (Tool) controls power, bite size and adaptation quality; see
[Progression and Economy](06_PROGRESSION_AND_ECONOMY.md) for all tracks. Each track has twelve levels;
each purchase noticeably improves the next outing and applies without a blocking animation.

## 5. Jetpack

- Starts simple and **stable**; never deliberately hard to control.
- Each upgrade improves speed, fuel efficiency and assists (hover hold, softer landings).
 Control quality never degrades; useful ascent is never gated by an upgrade-locked altitude ceiling.
- Simple input (Space; controller equivalent); full rebinding still applies.
- Works in narrow player-made shafts without wall bumps dealing damage or knocking the player around.
- With upgrades, returning from old shallow digs becomes trivial — a designed power fantasy.
- **The trip home stays short at 150 m.** At the jetpack level a player typically owns, flying home
 from their current working depth never becomes a chore; yard plus return time stays within the
 return-friction target ([Prototype Plan](14_PROTOTYPE_PLAN.md#4-validation-metrics-playtest-gates)).
 Keep Digging 2.0's 5,000 m map showed what happens otherwise: multi-minute backtracks that
 reviewers called a slog.

## 6. Precision crouch

- Held crouch lowers the viewpoint and slows horizontal movement to 35%, allowing low crawlways.
- **Footprint narrowing:** Crouching also narrows the machine's active digging bite, enabling delicate carving around silhouettes without over-cutting adjacent ground.
- No stealth, no stamina, no automatic cliff protection. Crouch is for precision and shaping; it never gates progress.

## 7. Auxiliary tool capabilities (Marking & Salvage)

The machine performs two clean auxiliary interactions without switching tools:
- **World-space marking:** Applies simple, reusable chalk/spray symbols to walls (arrow, home, return-here) to aid navigation.
- **Worksite placement:** Lamp and marking actions open a preview; primary confirms,
  secondary cancels, and rotate changes orientation. Interact retrieves a lamp or erases an aimed
  marking. Placement consumes the input without cutting behind the preview; ordinary digging
  resumes after a fresh press. Menus, focus loss, recovery and loading cancel placement.
- **Salvage tagging:** Attaches a recovery clamp/tag to exposed oversized set pieces, claiming them for surface salvage transfer.
- **Cleaning at the yard (optional):** holding dig on a muddy unique at the yard sprays its mud off in
 big chunks ([recovered uniques](07_SURFACE_HUB_AND_DISPLAY.md#5-recovered-uniques-at-camp)). Same input, no mode.

## 8. Falling and failure (movement side)

- **No health bar.** The battery is the only resource.
- Ordinary falls give harmless landing feedback only: no battery loss, forced input lock or surface
 recovery. Experimenting in your own hole should not cost progress.
- At 0 battery underground: recovery with all finds kept, a depth-scaled fee and interest-free debt
 that preserves the next outing. Being tested: the salvage crane lowers its rope, hooks the player and
 hauls them up their own route ([Progression and Economy](06_PROGRESSION_AND_ECONOMY.md#5-fuel-shared-battery-and-recovery)). Geometry faults are implementation repairs.
- Falls never delete items, never kill, never roll back progress.

## 9. What the tool is not

- Not a weapon; there is no combat.
- Not a light source.
- Never disabled, removed or invalidated by the story.
- Not joined by a separate gun tool. The late nozzle is an attachment on the same machine; the final
 visual is a shovel that has clearly become a cannon.
