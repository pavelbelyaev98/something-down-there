# 102 — Compact Lamps, Place-Anywhere, Bought Lamps & Depth Darkness

A hand-sized work lamp places wherever the player aims (only an empty kit refuses) and lights a wide area with a per-light near-brightness cap; New Game gives a few lamps and the computer sells more one at a time, with no lamp upgrade track and only the nearest eight lamps shining. Underground daylight tallies descent and sideways travel along the dug route, plus sky light scattered down it: a shaft or walked ramp fades naturally from the first metres (dim by about 10 m, lamps from about 12-15 m, dark by about 20 m), a side tunnel darkens from its first metres at any depth, fresh cuts are lit at once, and the surface is untouched.

## Objective
User request (2026-09-29): the placed work lamp is too big, its preview often turns red and refuses
placement, and it lights too small an area; the dig looks black too shallow. Make the lamp a small
light that places wherever the player aims and lights a wider space; start the player with a few
lamps and sell every further lamp at the computer; make the shaft readable to about 20 m and dark
by about 28 m on screen. Only light inside the excavation changes: sun, sky, grade, surface shadows
and camp lighting stay exactly as they are.

## Decisions (user, 2026-09-29)
- **No lamp upgrade track.** Rejected: twelve steps of brightness or count are thin "+5%" steps; a
  count track needs a smaller free kit (manufactured scarcity); the shared tier ladder would charge
  up to $2,300 for one more lamp; more shadowed lamps are capped by the shadow budget anyway.
- **Bought lamps:** New Game gives a few free lamps; every further lamp is bought one at a time at
  the computer for a flat price and is kept for good (reusable, never consumed). No multi-buy.
  The user expects around a hundred placed lamps by the end of a run; a cap bounds saves and cost.
- **Lamp types** (small vs flood lamp) are parked: interesting, but need testing and discussion.
  **Light string** (cable of lights from camp) dropped: the only cheap version (lights following
  your path) removes the choice of where light goes.
- The new model goes straight into the game; the user judges it in the playtest build.
- Far lamps: measure the cost of many lamps; fixing it (lamp light field) is a later task.
- Darkness: readable to ~20 m, black by ~28 m on screen (today it already looks black before 25 m).

## Concept reference
- 03 §7: no personal light; placed lamps are the only light underground, reusable, never expire or
  drain charge, attach to any solid surface, fall and stay recoverable when released; one lamp lights
  a useful stretch with broad, gradual falloff and no hot spot; solid ground blocks it. Daylight
  follows the open route, fades with depth, faster sideways; no ambient floor; sealed rooms dark.
- 09 §2: darkness escalates from shade to true black; early digging gets daylight first.
- 06 §4: reusable lamps bought individually were a late money sink; now they are bought from the
  start. 06 §2 (five tracks) is unchanged.
- 10 §3: snap-to-valid-surface placement assistance. 15: darkness is never a hazard.
- 049 (rejected attempt): never brighten the excavation through a global grade change.

**Concept changes in this task:** 03 §7 and 09 §2 (sky light reaches only the upper part of the
first zone; below it lamps are the only light), 03 §7 and 06 §4 (starter lamps plus bought lamps).

## Live code analysis
- Red preview: `WorksiteTools.UpdatePreview` rejects an aimed lamp when the ~0.3 × 0.5 m box,
  tilted to the noisy dug-surface normal and lifted at most ~0.2 m, overlaps any collider, when it
  comes within ~0.25 m of the player's capsule, or when the aim hits another lamp.
- `PlaceLamp` sets `Anchored` from the aimed point, not the lifted base: a lamp lifted over another
  lamp could anchor in mid-air. Dynamic lamps are not woken after nearby digging.
- The player is on Ignore Raycast; finds already ignore collision with the player (`FindPhysics`).
- Light: `ExcavationLighting.hlsl` bounds near irradiance with one global constant, so near
  brightness scales with output. Only `ExcavationLit` and `GroundTriplanar` include it; MainGame has
  no other point or spot light. Sun shadow distance is derived from `LightCullDistance + LightRange`
  (`UnityGameSettingsPlatform`, `SunPresentationSetup`), because URP drops additional-light shadows
  beyond it; that sum must stay put to keep the surface unchanged.
- Renderer is Forward (8 lights per object); the 4096 shadow atlas at 512 tiles holds ~10 point
  lights. More shadowed lamps shrink every tile and blur/leak.
- Daylight: `ExcavationDaylightGrid` measures the connected-air route (down 1, sideways/up 2 per
  0.5 m) and maps it through `exp(-0.018 p - 0.0012 p²)`. Sloped or stepped player shafts make the
  route much longer than the depth, which is why the screen looks black earlier than the formula
  suggests; `Exp` runs for ~1.16M cells per rebuild.
- Economy/save pattern: `JetpackState`, `StationTrade.RefillOffer`, `ComputerStation` command
  indices, `GameMenuView.BuildUpgradeRow` service rows, `WorldSnapshot`/`WorldSaveCodec` v14.

## Changes
1. **Model:** original Blender puck lamp (`art/work-lamps/create_assets.py`): rubber foot, yellow
   housing, frosted dome, steel guard, spike below the base that reads as jabbed into the ground.
   `WorksiteToolsSetup` builds the prefab with a small collider, light origin in the dome, light mass.
2. **Placement:** `WorksiteTools.SolveLamp` turns the aim into a pose and never fails: upright on
   floor-like surfaces, surface-aligned on walls and ceilings, lifted in small steps, then a box
   sweep back along the aim to the first clear spot; anchored only when the final base touches fixed
   scenery, otherwise a falling lamp. Aiming at a lamp or find is fine. Lamps ignore collision with
   the player. `PlaceLamp(pose)` commits exactly the previewed pose; only an empty kit refuses.
3. **Light:** per-light soft saturation of irradiance (`att / (1 + att·I/N)`, I = light output),
   identical to today at today's output; raise output for a flatter, wider falloff and a modestly
   longer range while keeping range + cull distance (sun shadow distance) unchanged. Only the nearest
   eight lamps shine, fading in and out; the rest keep their glowing dome.
4. **Bought lamps:** `EquipmentProgression` starter count, cap and flat price; `LampKit` owned by
   `FpsPlayer`; `StationTrade.LampOffer`; a "Work lamp" row in the computer's Services column;
   HUD `LAMPS available/owned`; the empty-kit prompt points to the computer.
5. **Save:** owned count in `WorldSnapshot` (validated, placed ≤ owned), codec v15 (New Game).
6. **Darkness:** a steep-shouldered curve from a per-cost lookup table (no per-cell `Exp`),
   tuned on real dug shafts to readable at ~20 m and black by ~28 m; sealed rooms stay dark.

## Edge cases
- Tight crevice, low ceiling, own feet, on top of a lamp or find, open air: a pose always exists;
  never inside ground or behind the aimed wall.
- A lamp that ends up inside ground after a pour/slump returns to the kit; unsupported lamps fall;
  crane contact knocks lamps loose (unchanged).
- Buying: stale offer, unaffordable, at the cap; buying never touches bag or equipment.
- Save: owned outside the range, placed > owned or slots beyond the cap are rejected.
- Many lamps: shadows stay sharp (≤ 8 shadowed lamps); a far lamp's dome still glows.

## Acceptance criteria
- Small lamp; placement works on floors, walls, ceilings, corners, next to finds and lamps and at
  the player's feet; only an empty kit refuses, and its prompt points to the computer.
- One lamp lights a clearly larger area without a hot spot; a wall still blocks it.
- Shaft readable at ~20 m and black by ~28 m on screen; the surface looks identical.
- New Game has 4 lamps; the computer sells one lamp per click at a flat price up to the cap;
  owned count survives save/load.
- Compiles warning-free; relevant tests pass; screenshots of placement, lamp light and shaft depths;
  frame time with many lamps measured; fresh Windows build; playtest note.

## Implementation notes
- **What made the dig dark:** on screen a narrow shaft looked dim even at 3 m with full route
  daylight, because vertical walls only receive the sky's horizon light, and the grade crushes it.
  The route curve alone could not make 20 m readable. Added sky light scattered down the route
  (`ExcavationBounce`, the up-facing sky irradiance times `ExcavationDaylight.Bounce`), zero at the
  surface and faded in over the first metres, multiplied by the route daylight like the rest of the
  ambient. A/B of the surface view with it on and off differs only inside the hole's mouth.
- **Route metric:** the old 6-neighbour route counted a 45° ramp as three times its depth (black at
  ~11 m). Rising/descending diagonals now cost 1.25 cells against 1 straight down and 2 sideways or
  up; costs are in quarter cells, distances are 16-bit, and a per-cost lookup replaces `Exp`.
- **Curve:** `exp(-(route / 24 m)^6)`; screenshots: readable at 20 m, very dim at 25 m, black at
  28 m. Shallow walls end up a little brighter than before; the tuning favoured readability.
- **Lamp light:** per-light saturation `att / (1 + att·I/2)` (identical to the old global bound at
  the old output) lets output buy reach; output 100, range 22, cull 30 (range + cull stays the sun
  shadow distance, so surface shadows are unchanged). A lamp shines from up to 0.3 m out along its
  axis, clamped by a ray so it never sits inside ground: a floor lamp only 7 cm high otherwise left
  the floor black. Only the nearest eight lamps shine, fading over 0.35 s.
- **Performance (Editor, one view of a 12 × 8 m room):** 0 lamps 978 draw calls; 4 lamps 1,481;
  8 lamps 1,988; 60 placed with 8 shining 2,113. Each shining lamp costs ~125 draw calls of shadow
  casting; the budget holds the cost flat beyond eight. GPU time is not available in the Editor.
- **Tried and replaced:** the steeper curve on the old route metric alone hit the shaft numbers,
  but a 45° ramp went black at ~11 m and a 3 m shaft wall still looked dim on screen. The small
  lamp at output 60 shining from its dome left the floor beyond it black and the walls modest.
  Scattered light at twice the final strength made 3 m walls brighter than the sunlit surface.

## Sideways darkness playtest iteration (2026-09-30)
- Feedback: dig 6 m down, then sideways, and the tunnel stays fully lit; digging sideways must get
  darker. Cause: the flat-topped route curve kept full daylight for the first ~15 m of any route,
  so a sideways leg only counted against it once the route was long (before this task a tunnel
  3 m in still kept ~70%).
- Change: the transport now carries two tallies along the chosen route. Descent (straight down or a
  descending diagonal) uses the depth curve; sideways and climbing moves multiply in
  `exp(-aside / 2 m)`, so light drops from the first metre sideways at any depth. The route choice
  keeps the same weights, except that a rising diagonal now counts (and costs) both its climb and
  its sideways step: otherwise the route zigzagged down-and-up along a tunnel and roughly halved
  its sideways tally. A side tunnel at 6 m measured 1.00, 0.82, 0.50, 0.30, 0.18 at 1-5 m in; its
  first metre or two stay lit because a tall tunnel mouth still sees the shaft diagonally. Shafts
  and walked ramps are unchanged.
- Tried: an aside reach of 4 m left a tunnel 6 m in at 0.56, still too bright.

## Dark-flash fix and softer sideways iteration (2026-09-30)
- Feedback: while drilling, fresh ground stays dark for about a second before it lights up; and
  sideways is now a bit too dark.
- Cause: each cut restarts the timesliced route rebuild (1.25 ms per frame) and newly exposed
  samples read black until it publishes. The two-tally rebuild walked the whole grid (~160 ms of
  work), so the flash lasted about a second.
- Fix: `ExcavationDaylightGrid.Patch` lights a cut's fresh air and wall samples at once from lit
  neighbours (each node a little dimmer), on the cut and again for cuts made while a rebuild ran.
  The rebuild writes a separate buffer and publishes when complete, so it never overwrites a patch
  mid-way. It resets and recomputes only the nodes the previous and current routes reached: after
  the full rebuild at load (~230 ms, behind loading), a cut on a shallow site costs ~4 ms of work
  and on a 40 m dig with a large room ~30-40 ms. A patched niche reads 0.53 at once and settles to
  0.35.
- Sideways reach 2 m -> 3 m: the 6 m side tunnel now reads 1.00, 0.88, 0.63, 0.45, 0.32 at 1-5 m in.
- Declined: a general minimum ("default") light underground; it would remove the need for lamps in
  the dark zones that the bought-lamp economy relies on.
- Follow-up (blinking while digging long side tunnels): the first patch brightened every open node
  near the cut from its brightest neighbour, lifting correct ground around the face by a few percent
  until the rebuild dropped it back, a pulse on every cut. The patch now fills only fresh nodes (open
  but not air at the last rebuild, or unlit), starting them dark, from lit neighbours at about the
  rebuild's own rates (0.97 from above, the sideways falloff otherwise). Stepping a side tunnel at 6 m
  in 0.4 m cuts, light right after each cut matched the rebuilt light within ~1%, and ground 1-2 m
  behind the face no longer changes.

## Natural daylight iteration (2026-09-30)
- Feedback: at 15 m everything was cleanly visible, which looked unnatural; are we over-engineering?
  Verdict: the route map, sideways tally and fresh-cut patch solve real problems; the look came from
  two compensations: a curve flat for the first ~15 m (built for "readable to 20 m") and a
  scattered-light fill equal on every surface.
- Change: descent now fades from the first metre, `exp(-(d / 12.5 m)^2)`: 0.85 at 5 m, 0.53 at
  10 m, 0.24 at 15 m, 0.08 at 20 m. The scattered light arrives from above (full on floors, half on
  walls, none on overhangs) and is scaled by the route daylight twice, so it fades before the direct
  light; strength raised to 3 to keep the top metres lit. The user accepted needing lamps from about
  12-15 m.
- Screenshots: 3 m lit, 5 m dimmer, 10 m dim but readable, 12 m very dim, 15 m nearly black, 20 m
  black; floors brighter than walls down the shaft. Tried: reach 10.5 m with strength 1.5 and 3.5 left
  10 m nearly black.

## Lamp shadow and dig particle iteration (2026-09-30)
- Feedback: lamps that fell with rocks while digging below them showed black rocks with broken lit
  patches and huge shadows; also remove the soil particles thrown while digging.
- Cause: the light point (thrown up to 0.3 m) was only re-aimed while the lamp moved, so rocks settling
  later could enclose it; and any find right next to a point light throws a room-sized shadow.
- Fix: a shining lamp re-aims every frame (at most eight), sphere-casting out from the dome and backing
  off until the point has 8 cm of clear space; a loose lamp throws straight up. Lamp shadows come from
  the ground only: rendering layers are enabled in the URP asset (no renderer changes layer, so which
  lights reach what is unchanged), ground chunks join `TerrainVolume.LampShadowLayer` ("Lamp shadows")
  and the lamp's custom shadow layers use it. A lamp buried among test rocks no longer darkens the
  room; ground shadow edges into a side branch remain. Finds therefore never shadow lamp light.
- Removed the per-stroke chips and dust (`TerrainVolume.EmitStroke`); pour, crack-break and break-in
  debris stay. Queue entry `031` updated.
