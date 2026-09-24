# 04 — Tool and Movement

## 1. One machine

There is exactly one excavation tool. It starts as a visibly improvised, motor-assisted shovel and ends as a garage-built
absurdity. The player never switches tools; upgrades bolt onto the same object.

- **Improvised baseline:** The starting tool is visibly motorized (small lawnmower engine/battery bolted to a spade handle). This immediately establishes the machine fantasy and logically explains why digging consumes battery from the very first stroke.
- **Visible body:** The tool only — no hands visible. The player watches the machine evolve for the whole game.
- **Visual escalation:** Motors, battery packs, wider heads, pipes, reinforcement, a late nozzle,
 welded plates and cables. The silhouette grows ridiculous while staying recognizably the same
 machine.
- **Power escalation:** The machine grows faster than the ground gets tougher. Small early
 digs become large, fast late excavation. Revisit familiar ground and feel the difference; the
 payoff is what the machine can do, not just how it looks.
- **Late-game excavation vs silhouette reveal:** Powerful late-game cutters clear large volumes quickly. For small common items (bottles, ore), this is a benefit that skips tedious cleaning. For massive machinery or buried structures, even a huge cutting head exposes only a fraction, preserving the silhouette discovery loop. Precision crouch allows narrowing the cutting footprint when delicate carving is desired.

## 2. Digging input

- **Hold-to-dig is the default.** Continuous digging from the very first shovel; no click-per-bite.
- **Toggle mode** available; press once to start, once to stop.
- **Additional auto-dig assist** stays for implementation if it offers something beyond hold/toggle.
- **Full rebinding** for every action on keyboard, mouse and controller; left-handed preset;
 sensitivity options; optional gyro.
- No mashing, no QTEs, no rhythm inputs, anywhere in the game.

The current playtest uses a drill-like shaving cut: holding steadily removes shallow layers as
the aim moves across the ground. There is no visible tool rig yet. Developer admin provides a
session-only shaving ON/OFF comparison with scoop digging while this feel is evaluated; ordinary
play never requires switching cutting modes.

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
- **Seam digging** uses these same automatic responses: aim along a visible seam for a larger,
 more efficient local cut ([World and Site](03_WORLD_AND_SITE.md)). No extra mode or required technique.

## 4. Upgrade tracks and the tool

The tool's own track (Tool) controls power, bite size and adaptation quality; see
[Progression and Economy](06_PROGRESSION_AND_ECONOMY.md) for all six tracks. Fewer, stronger steps replace tiny increments;
each purchase noticeably improves the next outing and applies without a blocking animation.

## 5. Jetpack

- Starts simple and **stable**; never deliberately hard to control.
- Each upgrade improves speed, fuel efficiency and assists (hover hold, softer landings).
 Control quality never degrades; useful ascent is never gated by an upgrade-locked altitude ceiling.
- Simple input (Space; controller equivalent); full rebinding still applies.
- Works in narrow player-made shafts without wall bumps dealing damage or knocking the player around.
- With upgrades, returning from old shallow digs becomes trivial — a designed power fantasy.

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

## 8. C4

C4 arrives late in the progression as an optional excavation accelerator.

- **Thrown or placed, then remotely detonated.** Multiple charges can be active at once.
- Charges **stick where they land** — no bouncing or clipping through targets (the reviewed
 anti-pattern).
- Placement is forgiving: a clear valid/invalid preview; no pixel-perfect hotspots; invalid attempts
 do not consume a charge.
- The blast is **properly powerful**: a large, predictable volume of ground disappears with matching
 cleanup. Saving for charges must feel worth it.
- Charges cost money; C4 is an optional accelerator, never the only way past anything.
- Its own small upgrade track: blast size, pack size, efficiency.
- **Finds survive:** distinctives and uniques stay visible for deliberate collection after a blast.
 Commons keep their normal pickup behavior; full-bag overflow waits in the world. Lamps survive too.

## 8. Falling and failure (movement side)

- **No health bar.** The battery is the only resource.
- Ordinary falls give harmless landing feedback only: no battery loss, forced input lock or surface
 recovery. Experimenting in your own hole should not cost progress.
- At 0 battery underground: recovery with all finds kept, a depth-scaled fee and interest-free debt
 that preserves access to a basic refill. Geometry faults are implementation repairs.
- Falls never delete items, never kill, never roll back progress.

## 9. What the tool is not

- Not a weapon; there is no combat.
- Not a light source.
- Never disabled, removed or invalidated by the story.
- Not joined by a separate gun tool. The late nozzle is an attachment on the same machine; the final
 visual is a shovel that has clearly become a cannon.
