# 022 — Jetpack Hover Hold & Speed Progression

**Status:** complete. The jetpack is a shop track (`EquipmentProgression.Jetpack`) that climbs faster and cheaper per metre every level; flight is Space alone (hold to climb, let go to fall) and there is no hover.

## Objective

Give the jetpack its own ten-level shop track: each purchase flies faster and costs less battery per
metre climbed, and from the first purchase a hover-hold assist keeps the player in place in a
narrow shaft. Measure the real ascent time and battery cost per level and depth so `024` can
calibrate its warning against numbers instead of charge fractions.

## Concept reference

- `04` §5: the jetpack starts simple and **stable**, never deliberately hard to control. Each
  upgrade improves speed, fuel efficiency and assists (hover hold, softer landings); control
  quality never degrades; no upgrade-locked altitude ceiling. Simple input (Space). Works in narrow
  player-made shafts with no wall-bump damage or knock-back. Returning from old shallow digs becomes
  trivial (power fantasy). The trip home stays short at 150 m at the level a player typically owns.
- `14` §4: return friction: yard + return time ≤ ~15% of session time.
- `06` §2: Jetpack is one of five tracks, ten levels (starter included), nine sequential purchases at
  the shared tier price; every purchase noticeably improves the next outing; purchases apply at once,
  no blocking animation. Tracks enter the shop with their mechanics. §5: one battery powers digging
  and jetpack; drain only during powered actions.
- Research: A Game About Digging a Hole's jetpack is "widely liked"; its and Keep Digging's
  complaints are trekking out every few minutes and multi-minute backtracks; One Man's Trash players
  asked for a jetpack over rope climbing.

## Live codebase analysis

- `FpsPlayer.Move`: Space after a 0.22 s hold (free tap = jump) thrusts at 30 m/s² up to 8 m/s,
  8 energy/s (1 energy/m); release falls under -20 m/s² gravity; a re-press in the same flight
  thrusts at once. Stats sit in the scene-serialized `FpsTuning`; no track, no hover.
- Shop: `EquipmentKind { Shovel, Inventory, Fuel }`, `StationTrade` offers/levels/revisions,
  `ComputerStation` rows (refill and sell commands after the tracks), `GameMenuView` headline and
  tooltip per track. Save: `WorldSnapshot` levels + `WorldSaveCodec` (version 11).
- Admin: session-only switches in `FpsPlayer` + `GameMenuView.BuildAdmin` (Motion comparison).

## Design

- **Track data** (`EquipmentProgression.JetpackProfile`, levels 1–10, nothing in the scene): top
  ascent speed 8 → 17 m/s (+1 per level), thrust 30 → 48 m/s², energy per second 8 → 8.5, so energy
  per metre climbed falls 1.00 → 0.50. Level 1 is exactly today's jetpack. `FpsTuning` keeps only
  the hold delay (input feel); the scene loses its jetpack stat copies.
- **Hover hold (level 2+)**: once powered flight has begun, the jetpack can hold the player's height:
  it brakes vertical motion to zero (45 m/s²) and holds, draining half the thrust rate. It never
  holds within 0.5 m of standing ground, so arriving at the rim or a floor just lands. An empty
  battery drops the player as before.
- **When hover engages (A/B, Developer admin → "Hover: …", session-only)**:
  - **A — while digging (default)**: hover holds while dig is held in the air; let go of dig and
    you fall as today. Space stays the only jetpack input and going down your shaft is unchanged;
    closest to "simple input" and to the narrow-shaft use (digging a side tunnel mid-shaft).
  - **B — on release**: releasing Space hovers; hold Crouch to drop. Holds anywhere, also for
    looking and picking up, but descending needs a second input.
  Remove the losing variant once chosen.
- **Shop row**: "Jetpack n/10", headline top speed `a → b m/s`, tooltip energy per metre and hover.

## Ascent cost (straight climb, from rest)

| Level | Top speed | Energy/m | 37.5 m | 75 m | 150 m |
|---|---|---|---|---|---|
| 1 | 8 m/s | 1.00 | 5.3 s / 41 | 10.0 s / 78 | 19.4 s / 153 |
| 4 | 11 m/s | 0.75 | 4.0 s / 31 | 7.4 s / 59 | 14.2 s / 115 |
| 7 | 14 m/s | 0.60 | 3.2 s / 25 | 5.9 s / 48 | 11.3 s / 93 |
| 10 | 17 m/s | 0.50 | 2.7 s / 21 | 4.9 s / 40 | 9.3 s / 78 |

Time includes the 0.22 s hold delay; thrust fights gravity (-20 m/s²), so the net climb acceleration is
10 → 28 m/s². Energy is battery units (starter tank 100). Straight vertical time is short at every
depth even at level 1, so the track's real payoff is battery share (a level-1 climb from 75 m eats most
of a mid tank) and the hover; lateral walking through a player's own tunnels adds to these numbers.

## Edge cases

- Free tap-jump, the hold delay, last-fuel burn and re-press behaviour stay as they are.
- Hover never starts from a plain fall or jump (only after powered flight in this airborne phase).
- Ceiling contact during hover or climb stops vertical motion with no damage or push.
- Save/restore keeps the owned jetpack level; admin hover choice is never saved.

## Acceptance criteria

1. Jetpack track in the shop at the shared tier prices; buying applies immediately and saves.
2. Every level climbs faster and cheaper per metre than the last; level 1 matches the old jetpack.
3. Hover (level 2+) holds height within a few centimetres and drains battery; level 1 never hovers.
4. Both hover variants selectable in Developer admin; default A.
5. Tests pass; build refreshed.

## Results

- Build 2026-09-27 includes the track. Shop screenshot `Logs/022-shop.png`: "Jetpack n/10" row with top speed `a → b m/s` and the shared
  tier price; the four-track table still fits.
- PlayMode: each level reaches its top speed at its energy rate; level 2 hover holds within 3 cm
  and drains 4.05/s; level 1 never hovers; no hover under half a metre above a floor or on an empty
  battery; "on release" holds until crouch and resets with Restore normal rules.

## Iteration (2026-10-10): hover removed

The user, shown the hover A/B (hold height while digging, or whenever Space is released with crouch to drop):
"neither is needed... I want to control the flight ONLY with space... just hold space to fly". Hover hold,
both triggers, the Developer admin **Hover** switch, the profile's `HoverHold` and the hover constants are
removed; letting go of Space falls whatever else is held (`LettingGoOfSpaceFallsEvenWhileDiggingOrCrouching`).
