# 101 — Tower Crane Salvage

**Status:** complete. The purchased tower crane (the pack's smallest model, weathered, just outside the plot's south-east edge) is the salvage machine: holding E bolts a lifting eye onto the unique; an automatic operator works the pack's own levers (speed knob raised, anti-sway assist) to swing from its rest over the set-down spots to the hole mouth and lower its hook down the shaft; the crane's one rope (the pack's straight cable stripped from its mesh) then becomes the smart rope, its own hook riding the rope's end along the dug route into the eye, the crane reels the unique out through jams and ruptures and carries it to a free spot beside the camp for good; the winch fixture, pads and exhibit stands are gone and saves are format v14.

## Objective

User request (2026-09-28): the purchased tower crane pack (`unity/Assets/TowerCrane`) becomes the game's
salvage machine. Remove the winch (fixture, rope, hook, recovery art) and the exhibit stands, and let
the crane pull a marked unique out of the hole and set it down near the camp, where it stays. Drive it
with the pack's own crane motion and controls. Purchased-asset workflow, no backward compatibility.

Clarified (user, 2026-09-29): keep the previous smart rope that navigates the tunnel, auto-attaches and
excavates; the crane actually moves right above the hole, the rope goes straight down, then becomes smart
and follows the path to the unique.

Feel: you mark the computer at the end of your tunnel; the crane swings over your hole, the rope snakes
down your own route, drags the computer out through the dirt, and the crane sets it down beside the camp.

## Concept reference (rules this task keeps or changes)

- `05` whole-object recovery: exposure first (manual aim around uniques), mark anywhere on the visible
  object by holding Interact, the mark stays on the object, recovery is automatic (no surface trip, no
  held button), the rope comes down through connected excavated space, attaches, hauls a dynamic body
  through bends with strain, ruptures and momentum chains, never asks for manual clearance, full bags
  never block it, it costs no battery, and a mid-haul save resumes. **Changed:** the rope hangs from
  the crane's hook parked over the hole mouth instead of a rim winch, and arrival is the crane carrying
  the load to camp instead of a pad.
- `07` §1/§5: the yard is compact; a unique arrives, never sold, never auto-placed by a menu.
  **Changed (user):** no exhibit stands or placement step; the crane sets each unique down at its own
  spot near camp and it stays there, readable (name, depth, story).
- `02`/`06`: uniques stay unsellable; recovery keeps no fee.

## Decisions

- **Model:** `TC_old_Base_small`, the pack's smallest (user; 32.6 m tall, trolley 2.5–26.9 m), in
  weathered paint to suit the drained-lake worksite. Clean `TC_Base_small` is the same rig.
- **Placement:** mast at (15, −14), just outside the plot's south-east edge beside the camp: its hook
  reaches about 75% of the plot and three set-down spots east of the camp (where the winch pads stood),
  on flat ground clear of scenery; the jib rests toward the plot centre. For a hole mouth beyond its
  reach it parks as near as it reaches and the rope runs across to the hole.
- **Pack controller can't run here, so its motion is reproduced on its rig.** `Controller_TC` reads
  `Input.GetKey`, which throws every physics step in this Input System–only project
  (`activeInputHandler: 1`); its keys (WASD, arrows, Q/E, Space) collide with player controls; its
  hook stops at world y ≥ 0.3 while uniques lie 8–21 m below ground; and it compiles into
  `Assembly-CSharp`, which the runtime assembly cannot reference. The project-owned variant strips the
  vendor `Controller_TC`/`Hook` components and `TowerCraneRig` applies the same calls to the same
  bodies: slew = relative Y torque on the cabin, trolley = relative Z force on the truck, hook turn =
  relative Y torque on the swivel (all `VelocityChange`, per physics step, times the pack's
  `speed_General`), hoist = the hook `ConfigurableJoint` linear limit changed by
  `speed_Hook × speed_General` per step. Same speeds, drags and joints as the pack; only the key reads
  and the world-height floor are gone. Vendor files stay untouched.
- **Automatic operator:** `SalvageCrane.Operator` drives those axes like an operator (full lever when
  far, easing off with the rig's own inertia and drag near the target). The player never takes the
  controls: concept `05`'s automatic recovery and no-camera-takeover rules stand.
- **Smart rope from the hook:** marking plans the route through connected excavated air to the hole
  mouth (the restored `ExtractionRoutePlanner`); the route gets a straight exit above the mouth and ends
  at the hook's park point (3 m above the mouth, or the nearest reachable point). The crane swings over
  and settles (within 15 cm, still); the rope (restored `RopeDynamics` in `CraneRopeView`) pays out
  from the park point straight down the shaft and along the route, attaches, and the restored spring
  guide, jam/retension, rupture, impact chain, lamp knock and debris logic hauls the load to the hook.
  The drawn cable starts at the live hook tip; its physics pays out from the park point.
- **Handoff:** at the route's end (only when that end is above ground) the crane takes the load: the
  rope rig is released and the load follows the hook tip kinematically (the pack's cargo attach),
  hooked where it hangs. The crane lifts it to travel height over the mouth, carries it (it turns
  upright once clear), settles within 15 cm of the spot and sets it down resting on the ground.
- **Delivery:** first free set-down spot; the object becomes `Stored`: kinematic, permanent,
  inspectable (prompt shows name, depth and story; Interact repeats it). No `Displayed` state or sockets.
- **Hook colliders off:** the crane's hook is a carrier, not a probe, so its 1 m block cannot jam,
  land on the player or hit the flight ceiling.
- **Presentation:** URP Lit copies of the pack's weathered materials under `Content/Salvage/Crane`
  (the vendor materials use the Built-in `Standard` shader and render magenta); the skinned boom mesh
  (jib, trolley, cable, hook) updates bounds off-screen, so the stretched cable stays visible from the
  hole; moving bodies interpolate. `ExcavationDaylight` adapts the Lit copies and the rope's
  `Cable.mat`, so both darken underground.
- **Save:** format v14; `ExtractionSnapshot` keeps the route, attachment, load motion and guide
  progress and gains the crane's grab point, set-down spot and pose (slew, trolley reach, hoist length,
  hook turn); phases Planning → Reaching → Deploying → Attaching → Hauling/Retensioning → Lifting →
  Carrying → SettingDown. `FindSnapshot.DisplaySocket` and `FindState.Displayed` are removed.

## Live codebase analysis

- Removed with the winch: `SalvageWinch`'s rim fixture, lift anchor and pads, `UniqueDisplayStand`,
  `DiscoveryField.StoredUnique`, `BuriedFind.DisplaySocket`/`Displayed`, `Steel`/`Worksite yellow`
  materials, `Content/Salvage/Models` (WinchFixture, Hook, RecoveryPad, ExhibitStand) and their Blender
  sources in `art/salvage-winch`, `SalvageWinchSetup`/`SalvageWinchValidator`.
- Restored and re-owned by the crane: `SalvageWinch.Physics/Contacts/Feedback` → `SalvageCrane.*`,
  `WinchRopeView` → `CraneRopeView` (no separate hook model), `SalvageWinchSettings` →
  `SalvageRopeSettings` (`WinchSettings.asset` → `RopeSettings.asset`, tuned values kept),
  `RopeDynamics`, `ExtractionRoutePlanner`, `ExtractionSnapshot`, `TerrainVolume.ClearLoadSweep`,
  `ExcavationGrid.RemoveBoxSweep`, `FindPhysics.RecoveryContact`, `Cable.mat`,
  `SurfacePerformanceFixture.Recovery`, the recovery tests.
- Shared with the gravel pour: `SoilBreak.shader` (now `Runtime/Terrain`) and the `SoilCrumbs`/
  `SoilDust` materials (now `Content/GroundTextures`).
- Kept: hold-to-mark (`FindExtractionInteraction`), `RecoveryMarkView` + `RecoveryMark.shader`/`.mat`,
  the `BuriedFind` Extracting/Stored lifecycle, `FindPhysics.ClaimForRecovery`.

## Architecture

- `Runtime/Interaction/TowerCraneRig.cs` — the pack's control model on the pack's rig: four levers
  (slew, trolley, hoist, hook turn, −1..1), stepped once per physics step; geometry queries (slew
  angle, trolley reach, hoist length, hook tip, rope for a tip height, aim) and `CranePose`
  capture/apply for saves.
- `Runtime/Interaction/SalvageCrane.cs` — the recovery job: mark, route planning with the park point,
  rope descent/attach/haul ticks, handoff, delivery, the glyph, save capture/validate/restore.
  `.Physics`/`.Contacts`/`.Feedback` — the rope haul; `.Operator` — the crane levers, anti-sway,
  kinematic follow and straightening. `Tick(dt)` runs from `FixedUpdate` (tests step it).
- `Runtime/Interaction/CraneRopeView.cs`, `RopeDynamics.cs`, `ExtractionRoutePlanner.cs`,
  `SalvageRopeSettings.cs`; `Runtime/Persistence/ExtractionSnapshot.cs`.
- `Editor/SalvageCraneSetup.cs` (menu **Configure Salvage Crane**) — builds the project prefab variant
  `Content/Salvage/SalvageCrane.prefab` from the vendor prefab (URP material copies, vendor scripts and
  hook colliders removed, interpolation, off-screen skinning; rig bodies read from the vendor
  `Controller_TC` references), places it, the rope view and the set-down spots in MainGame and wires
  references. `SalvageCraneValidator` checks the wiring before builds.
- Art card `art/tower-crane/README.md`.

## Edge cases

- Mark while the crane is busy or with no free spot: clear feedback, the unique stays untouched. No
  rope route to the opening, or a load that cannot clear the rim: planning fails with a message.
- Save/load in every phase: route, load pose/motion and crane pose restore together; a load on the hook
  snaps back under it; a crane pose outside the rig's limits rejects the save.
- Pause, focus loss or terrain restore: the rope and load freeze, the levers release and the rig
  coasts; the job resumes.
- A route whose end is underground never hands the load to the crane.
- Stored uniques stay kinematic, can't be lifted, bagged or sold, and keep their spot across reloads.

## Acceptance criteria

1. No winch fixture, pad, stand, socket or their art remains; the save format is v14 with no older reader.
2. The crane stands beside the camp, renders with URP materials and reaches all set-down spots (scene test).
3. Marking an exposed unique at the end of a bent tunnel: the crane swings over the hole mouth, the rope
   goes down the shaft and along the tunnel, attaches, hauls through bottlenecks (widening them), the
   crane lifts the load clear, carries it and sets it down upright on a free spot near camp. No player
   input after marking.
4. Jams wind up and rupture locally, momentum chains through the next dirt, commons loosen, mounted
   lamps are knocked loose; permanent walls stop the load without a manual pause.
5. Saving and reloading mid-haul resumes the same recovery; after delivery the unique stays put across
   reloads and shows its name, depth and story.
6. All three computers can be recovered to three separate spots.
7. Code compiles warning-free, tests pass, the Windows build is fresh, docs and a playtest note are updated.

## Implementation notes

- **The crane's rope swings.** The hook joint locks sideways motion in the hook's own frame, so the whole
  hoist rope is a pendulum (a 27 m rope swung the tip 1.3 m at 2.7° of tilt, period ~11 s). The rig's
  anti-sway assist pulls the hook back under the trolley (2 s⁻² stiffness, critically damped against the
  rope's own swing), so the operator steers the trolley straight at the target and waits for a nearly
  still hook before entering the hole (15 cm, 0.2 m/s) and before the last metre of a set-down (6 cm,
  0.1 m/s: the looser gate landed loads up to ~20 cm off their spot). The hook counts as already on its
  way down only 5 cm below the 1 m hold height: reaching the hold height exactly (float rounding of the
  hoist step) used to skip the settle gate and dropped a still-swinging load up to 0.64 m off its spot.
- The operator's levers are critically damped against each axis's own acceleration and drag, and
  saturate to the pack's top speed far from the target.
- A recovery of the Reservoir Computer through a bent tunnel took ~6.5 s for the crane to swing over
  from its rest above the set-down spots, ~1.7 s lowering, ~1 s for the hook to ride the tunnel, ~0.7 s
  hooking on, ~16 s haul (including the bottleneck), ~0.7 s lifting, ~2.3 s carrying and ~4 s setting
  down: ~34 s in all.

## Rejected attempts

- **Vertical lift (first build):** the crane's hook came straight down an open shaft, grabbed the object
  and lifted it; marking required dug-out ground straight above the object. The user wanted the
  winch's smart rope kept (tunnels, bends, auto-attach, excavation), so the rope system was restored
  and hung from the crane. Its anti-sway operator, kinematic carry and set-down were kept.
- Steering the crane by the trolley's own position: the swinging tip missed its target by 0.19 m.
- Letting go as soon as the hoist limit reached its target: the hook was still falling and the load
  rested 6.6 cm above the ground; the release waits for the tip to come to rest.
- Requiring 6 cm precision at the set-down spot: the long travel rope took much longer to settle; open
  ground uses 15 cm.
- Driving the smart rope's physics from the live hook tip: the swaying end paid the cable out slack
  (straightness ratio 1.05–1.07 against < 1.04); physics pays out from a fixed route point and only
  the drawn cable starts at the crane.
- **Steering against the swing with a trolley lead of `2√(L/g)`:** critically damped on its own, but the
  hook still needed ~13 s to swing over and settle on its long rope. Adding a relative sway damper on top
  overdamped it (the hook crept the last metres: 12 s). The rig's stiffness assist replaced both.
- **The crane's small hook riding the rope's end (third build):** a hook moving sideways through a tunnel
  with nothing to move it read as a second rope spawning at the shaft bottom (user). The tracked crawler
  gives that part a visible job.
- **A rope crawler on a reel hung from the hook (fourth build):** a tracked crawler drove the tunnel
  trailing its own line from a reel on the hook; the two-part hand-off still read as artificial (user),
  and one rope from the crane replaced it.
- **The crane starting over during the mark hold:** the swing is worth seeing after the mark is placed
  (user).
- **The rope's physics end at the hook block's cable entry during the ride:** the straight hook cuts
  bends, so that end sat inside the corner soil and dragged cable particles into it (the cable-in-air
  test failed every run); the end stays on the route a hook's length behind the seat.
- **Simulating the whole rope from 3 m above the hole mouth:** ~50 particles for the same solver
  iterations stretched to 4× their spacing at tunnel corners after a mid-haul reload and left particles
  in corner soil (the cable-in-air test failed in most suite runs). Only the rope below the straight
  shaft is simulated (or from 1.2 m ahead of a load already in the shaft); above, it is drawn straight up
  the route to the trolley.
- **A separate rope hanging from the parked hook (second build):** the crane parked 3 m above the mouth
  and a thin extra rope did all the work down the shaft; the user found it weird next to the crane's own
  cable. The crane's block now goes down the shaft itself and its small hook rides the rope's end.

## Open

- Store link and licence of the pack are not recorded yet (`art/tower-crane/README.md`).
- Recovery pacing waits for the playtest.

## Iteration: smallest crane (user, 2026-09-28)

- The user asked for the pack's smallest crane: `TC_old_Base_small` replaces `TC_old_Base_large` (53 m,
  trolley 2.5–33.6 m). The small rig names its swivel and cabin block differently, so the setup reads
  the moving bodies from the vendor `Controller_TC` references and accepts either cabin stop name.
- No point outside the plot lets 26.9 m reach the whole plot. The mast moved from (0, −21) to (15, −14).
  Full-plot hook coverage would need the pack's rail-mounted variant (`TC_old_Rails_small`); with the
  smart rope, a far hole only means the rope runs across from the nearest park point.

## Iteration: smart rope from the crane (user, 2026-09-29)

- The winch's rope system came back from git, re-owned by `SalvageCrane`: the route, rope, haul,
  ruptures and debris work as before, the rope's top end is the crane's hook parked over the hole mouth,
  and the crane carries the load to camp instead of the pad. Tests restored from the winch
  (`UniqueRecoveryIntegrationTests`, `ExtractionTests`, `RopeDynamicsTests`) run against the crane;
  the vertical-lift tests were removed. Save format v14.

## Iteration: the crane's own hook goes down (user, 2026-09-29)

- Feedback: the smart rope hanging from the parked hook read as a separate, weird rope; the crane's
  own cable and hook should be what goes down. The pack's cable is part of the jib's skinned mesh,
  stretched straight between the trolley and hook-block bones (a render test showed the cable end and
  the yellow block both follow `point_hook`, while the swivel bone carries only the small hook), so it
  cannot bend into a tunnel.
- The crane now lowers its own hook block straight down the shaft on its cable (pack hoist) to the
  bottom of the route's straight run under the trolley (new `Lowering` phase; at least 1.2 m from the
  load). There the small hook (swivel) is released from its joint and rides the smart rope's end
  (`CraneRopeView.Carry`) along the rest of the route; the rope pays out from the block's route point
  and is drawn from the live block. While hauling, the block rides up the shaft 1.2 m ahead of the
  load; at the top the hook seats back under the block for the carry. The drawn rope is 2.5 cm wide
  like the crane's own strands. Operator control now measures the swing from the block body.

## Iteration: rope crawler and a brisker crane (user, 2026-09-29)

- Feedback: aligning over the hole and lowering took too long, and at the shaft bottom another rope
  seemed to spawn and navigate on its own. Chosen fix (user agreed): keep the handoff but give every part
  a job, like a deep-sea robot leaving its cage on a tether.
- A rope reel hangs on the crane's small hook (bail on the hook tip) with a tracked crawler docked
  nose-down under its outlet (original Blender models, `art/salvage-crawler`). The rig's working tip is
  the docked crawler's grip (`TowerCraneRig.toolDrop`), so every existing reach, lift and set-down
  height still applies. The small hook no longer leaves the block.
- At the bottom of the shaft the crawler (`SalvageCrawler`) drives the rope's free end along the route:
  it follows the floor under the route (raycast within 1.6 m), hangs on its rope down drops, climbs
  onto the marked point over the last 0.7 m and closes its jaws during the attach. The rope ties onto its
  tether eye and pays out the floor drop too; while hauling it lies clamped along the rope, the cable
  inside its length hidden. At the top it docks with the load; it lets go at camp. Wheels, drum and jaws
  turn; its headlamp lights the tunnel between Lowering and the handoff. Saves are unchanged (its pose
  derives from phase and progress).
- Speed: the pack's `speed_General` 0.01 → 0.02, lever response 2 → 3 rad/s, the anti-sway assist
  above, entry into the hole within 15 cm, and the crane heads for the find while the mark is held.
  Swinging over now takes ~5 s instead of ~13 s; carrying to camp ~2.4 s instead of ~11 s.
- The user may later make the tunnel carrier more characterful (thrusters, creatures); recorded in
  [Open Questions](../../concept/13_OPEN_QUESTIONS.md).

## Iteration: one rope and the crane's own hook (user, 2026-09-29)

- Feedback: the crawler's separate line still looked artificial, and only the extension bent while the
  crane's cable merely hung. Wanted: one rope, the crane's hook attaching to the item, and E placing
  something the hook grabs. Real riggers work that way: a lifting eye goes on the load and the hook
  takes it; a rope bending round tunnel edges does what a pulley at the corner would.
- The pack's hoist cable is the only geometry joining the trolley and hook-block bones of the jib's
  skinned mesh; the variant uses a copy without those triangles, and `CraneRopeView` draws the crane's
  one rope from the cable's measured top (sheave) to its bottom (entry into the block) at all times.
- Holding E bolts a swivel lifting eye (original Blender model, `art/lifting-eye`, sized for the hook)
  onto the find; the glyph remains only as the hold preview. The eye swivels toward the hook.
- The hoist lowers the hook to the bottom of the straight shaft; there `TowerCraneRig.ReleaseHook`
  frees the hoist joint and the hook is placed by hand at the smart rope's end, seat first along the
  route and into the eye (its seat measured from the mesh: the inside bottom of the bowl, ~20 cm above
  the physics box's bottom). While hauling it hangs in the eye along the rope. At the top `SeatHook`
  puts it back on the hoist, the rope taking up the drop. Only the rope below the straight shaft is
  simulated; above it, the same drawn rope runs straight up the shaft to the trolley; the load hangs from the seat at its eye (no
  longer moving to the hull top), and the carry aims so the load's centre lands on its spot.
- The crane rests with its jib over the set-down spots (heading and reach from the spots) and starts
  moving only once the mark is placed. Its slewing colliders (the pack's jib box and trolley stops) are on
  Ignore Raycast: out of reach above the flight ceiling, they no longer block aim, lamp and rope rays
  wherever the jib points. The crawler, reel and their materials are removed.
