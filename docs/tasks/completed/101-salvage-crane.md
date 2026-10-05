# 101 — Tower Crane Salvage

**Status:** complete. The purchased tower crane (the pack's smallest model, weathered, just outside the plot's south-east edge) is the salvage machine: holding E bolts a lifting eye onto the unique; an automatic operator works the pack's own levers (speed knob raised, anti-sway assist) to swing from its rest over the set-down spots to the hole mouth; the crane's cable (the pack's two falls, stripped from its mesh and redrawn so they can bend) and hook are the smart rope, and the moment the crane is aligned the hook rides the rope's end in one run from where it hangs down the hole and along the dug route into the eye; the crane reels the unique out, treating a slow creep as a jam and tearing through it; out of the ground the unique swings from the hook by its eye as a physical body, and the crane lowers it onto a free spot beside the camp and lets go, and it stays where it settles; the winch fixture, pads and exhibit stands are gone.

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

## Iteration: the pack's two cables and a harder haul (user, 2026-09-30)

- Feedback: bring back the model's original two cables (the single drawn rope had replaced them), with
  the smart rope during extraction; and extract more aggressively, since after hooking on the load stays
  stuck longer than planned.
- **Two falls.** The stripped cable is two hexagonal strands (radius 1.24 cm, 42 cm apart at the trolley's
  sheaves, 31 cm at the block, on the jib's own material and a small UV strip). `SalvageCraneSetup`
  measures their ends, spacing, radius, material and UV strip; `CraneRopeView` rebuilds both as tubes
  every frame around the cable's centre line (the LineRenderer and `Cable.mat` are gone). Hanging, they
  run straight between the pack's own ends, exactly as the original. On a recovery their pair axis is
  carried down from the sheaves without twisting; the hole draws them together (almost touching
  underground) and they spread again over the last 0.6 m into the block, which turns about the rope so
  its entries meet them. A real two-part reeving pulled through a hole bunches the same way.
- **Rejected cable attempts** (screenshots in a disposable Play Mode fixture, bent tunnel):
  - full spacing along the route, snapping the pair across each bend: a Z-kink at every sharp corner;
  - full spacing with each strand pushed out of the soil: a sharp V where the inner strand met a corner;
  - the same with the spacing smoothed along the rope and the pair laid flat on floors: a crossed pinch
    where the rope cut a corner;
  - rolling the pair to lie along the nearest dug wall (density gradient): the voxel normals 15–25 cm
    from a wall are noisy and the pair zig-zagged, and against side walls the falls stacked vertically.
- **Harder haul.** Measured in simulated time with the Reservoir Computer 60% uncovered, from the hook
  taking the eye to the handoff: a 0.9 m shaft dug straight down onto it among commons took 15.7 s, a side
  tunnel among commons 10.3 s, a narrow side tunnel 7.2 s. The load crept up tight shafts at 0.1–0.4 m/s:
  each creep past a few centimetres reset the jam and retension timers, so the crane never wound up.
  Now progress only counts at a share of the haul speed (`CreepShare`), so a creep along the walls is a
  jam, and the wind-up is shorter, the pull stronger and the chunks about a fifth bigger
  (`RopeSettings.asset`). Result: 10.0 s, 8.7 s and 4.9 s; the creep rule alone saved 2.4 s in the shaft
  and nothing in the side tunnel.
- **Rejected haul attempts:** tearing out a persistent jam's two strongest contacts at once saved only
  0.3–0.7 s, and with chunks 2.6 times the old volume a single full-effort break removed about 1 m³,
  breaking the local-patch rule (a break stays under 0.6 m³, checked by the persistent-jam test). The
  largest break is now about 0.46 m³.

## Iteration: one run into the hole and a physical carry (user, 2026-09-30)

- Feedback: out of the ground the crane turned the load into a set pose to put it down; keep the
  physics instead. And before the hole the hook went down, stopped, then went down again: why not ride
  the smart rope straight away?
- **One run into the hole.** The stop was the hoist lowering the hook to 3 m above the mouth while the
  jib was still swinging in (the hook overshot the hole by 0.8 m and crept back for ~1.5 s), then the
  hoist lowering it down the shaft and handing over to the smart rope at the shaft bottom. The Lowering
  phase is gone, and the hoist no longer lowers the hook at all: the crane's cable and hook are the
  smart rope. While the jib swings in, the hook keeps its resting height; as soon as the trolley holds
  within 25 cm of the park point (slower than 0.3 m/s) the hook leaves the hoist where it hangs, swing
  and all, and rides straight down to the park point (its swing folding into that first straight line),
  then along the route, at the hoist's speed down to the shaft bottom and the rope's pace along the
  tunnel (easing between them). The hoist length follows the descending hook, so a checkpoint on the
  way down resumes from its height. Down the straight shaft the rope hangs straight from the trolley
  (nothing simulated); below it the simulated rope pays out as before. The drawn cable skips route
  points in mid-air (nothing there can hold a bend), so it runs straight from the trolley to the first
  point at or under the ground. Marking to hooking on in the bent-tunnel fixture: 10.3 s before, 9.7 s
  now (the hook comes down from its resting height, no longer during the swing).
- **Physical carry.** At the handoff the load stays a dynamic body: a kinematic point rides the hook's
  seat and a joint limits the lifting eye to its link length from it, free to turn, with a little air
  drag so the swing dies down. The crane lifts it to travel height plus the farthest the load can hang
  below the seat, carries it with the seat over the spot, holds a metre up until the hook is still and
  the load swings slower than 0.4 m/s, then lowers at a fifth of the hoist's speed and lets go as soon
  as the link goes slack (or at the bottom). A new Settling phase waits until the load has been at rest
  for 0.5 s (8 s at most) before storing it where it lies. The upright straightening, the kinematic
  follow and the saved grab point are gone; a load on the hook now keeps its motion in saves (format
  v16).
- Rejected: starting the ride only once the hook had settled within 30 cm (it still hovered for ~1.5 s
  while the swing crept in); gliding the hook onto the route while the drawn cable still passed
  through the park point in mid-air (a kink beside the hook); and lowering the hook with the hoist as
  the jib swung in, switching to the smart rope within 1 m of the park point (user: the smart rope took
  over after the main hook came down; the whole hook and cable are the smart rope, starting the moment
  the crane is aligned).

## Iteration: glitch fixes (user, 2026-10-01)

- Feedback: weird rotations, the two cables behaving weirdly, and the load jumping when set down (the
  crane pushing it down). Reproduced with a sampled recovery (hook and load poses every ~0.04 s):
  - The hook block flipped 100-180 degrees about its rope every frame on the way down. The riding hook
    was eased toward the rope's end (14/s), so at 5 m/s it trailed ~36 cm above it and the drawn cable
    ran past the block's entry and doubled back; that reversed last span had no direction, and the
    block turned to match. The hook now sits exactly at the rope's end (frames interpolate like the
    rope), a last span under 3 cm merges into the entry, spans under 1 mm and doubling-back spans do not
    turn the falls, and the block eases its turn about the rope. Only the turn round the shaft-bottom
    corner and the turn into the eye remain.
  - Out of the haul the load kept spinning at up to 9 rad/s on its 21 cm link, and on arriving the hoist
    dropped it from travel height at full speed (5-7 m/s) and stopped dead: it bounced up and flipped.
    The eye now has pivot friction (a damping drive on the link, 1 N m s/rad per kg), and with a load on
    the hook the hoist eases (1.5 m/s at most, 0.6 m/s for the last metre, 1.5 m/s^2).
  - Letting go when the link went slack failed both ways: a load pivoting on a touching corner kept the
    link taut and was pushed into the ground, and a bounce on the link at the hold height read as
    slack and dropped it a metre. The crane now lets go the moment the load rests on something below it
    (a touching contact from underneath), or at the bottom of the lowering.
- Recovery in the bent-tunnel fixture: about 27 s from marking to storage (lifting and setting down
  are slower but smooth).
- The placed lifting eye shone underground: it was the one object never registered with the
  excavation daylight, so it stayed lit as in full sun and bloomed against the dark dig. Its renderers
  are registered like the finds and lamps; it now darkens with the ground around it.

## Iteration: hook through the ring and a haul that gathers pace (user, 2026-10-01)

- The hook hung beside the lifting eye, not through it: the eye aimed its up axis at the hook but its
  twist was arbitrary, so its ring could lie in the hook's plane. The ring's hole runs along the eye's
  local Z (art/lifting-eye); the setup measures which way the hook's wire runs at its seat (the long
  horizontal axis of the bowl's lower fifth, swivel-local Z) into `TowerCraneRig`, and from the hook's
  ride in to the set-down the eye turns about its pull so its hole runs along that wire.
- Feedback: the load stuck for about a second, broke the ground, and repeated that many times; the
  machine should pull harder the more it gets stuck. The machine's drive (1 = normal) grows by 0.5 for
  every second the load is stuck (wedged against a contact, or crawling below the creep share) up to
  2.5, and eases back with a 6 s time constant while it runs free. The pull multiplies by it, reeling
  by up to 1.35x, impacts break at the impact speed over its square root, break size follows it toward
  the full-effort chunk, retension comes sooner, and a jam that begins with the drive already up winds
  up sooner (the drive gained during a jam never shortens that jam, so the first one is honest; the jam
  tests check it). Haul times, no drive / gradual drive: shaft dug onto the find among commons 9.8 s /
  8.2 s, side tunnel among commons 8.5 s / 5.2 s, narrow tunnel 5.1 s / 3.7 s; in the shaft the drive
  climbs from 1 to 1.5 over the first 2 s of resistance and reaches its cap after about 4 s.
- Rejected: raising the drive 1.5x at every break (up to 3x): breaks come constantly, so after two or
  three it was at full aggression and the load came out almost without resistance (7.2 / 4.2 / 3.0 s;
  user: too fast, it switched to aggressive at once).

## Iteration: destruction effects (user, 2026-10-01)

- Asked for new and better effects for the rope's soil breaks. The old burst (22 billboard crumbs and 7
  puffs, under a second) used an unlit shader, so underground it glowed at full colour. Now each real
  break (never brief contact or a knocked lamp: concept 09, the jam tests) throws:
  - solid clods (a generated faceted mesh, art/soil-debris) that tumble out; each flight is marched
    through the ground's density to where it first meets soil, and a clod landing on a floor then rests
    there and sinks away over 1.6-3 s (visual only: no particle collision);
  - a spray of fine crumbs, a dust cloud that swells, hangs for up to 2.6 s and drifts with the surge, and
    a trickle of crumbs off the upper broken face for about a second;
  - colours from the ground at the break (palette from each ground's albedo texture and tint), and
    `SoilBreak.shader` now lit like the excavation: excavation daylight, sun with shadows, work lamps
    diffused like on the soil, a rounded-lump normal for billboards; the gravel pour shares it;
  - about 40% more of everything at full machine drive, and bigger clods.
- The first palette was saturated orange next to the soil; it now follows the soil texture, a little
  darker for its ambient occlusion.

## Iteration: shaft dust and rupture tell A/B (user, 2026-10-01)

- Asked to build the shaft dust with an admin ON/OFF switch, and variants of a tell before a rupture,
  each switchable in the admin panel to compare. Both are session flags on `FpsPlayer` (Developer admin
  **Shaft dust**, **Rupture tell**; Restore normal rules resets them); the crane reads them.
- Shaft dust (default on): every break adds to a dust load (capped, 7 s settling time constant); while
  it lasts, faint motes (pale soil dust, 6-10 s, slow upward drift with noise) spawn along the route
  from the last break up to where it leaves the ground and 0.6 m above, thickest at the break. The
  first version spawned along the whole route, which runs 3 m above ground to the hoist: motes hung in
  the air; it now stops at the mouth. Dark dust was invisible against the shaft walls and pale ground,
  so it is paler than the break's dust and alpha 0.1-0.2. `SoilBreak.shader` dust now fades softly where
  it meets the ground (scene depth) and scatters sunlight forward, glowing when seen toward the sun.
- Rupture tell (default off, since concept 09 still says debris only appears when ground is removed):
  strain is tracked while a stalled load presses on soil that opposes the pull (opposition > 0.2; finds
  and lamps never count): 0.75 from the rope's wind-up (pull 0.05-0.22 m) plus 0.25 from jam pressure,
  after an 0.08 s gate; a contact dropping out for a step decays it instead of resetting. Dribble:
  crumbs from the soil face around the contact, up to 45/s, with dust wisps above half strain. Cracks:
  5-7 jagged branches (a few fork) projected onto the soil by density search along the contact normal,
  revealed outward and widening with strain; a break removes them at once, a load that slips free fades
  them. Measured on the Reservoir Computer tunnel haul: jams last at most about 0.3 s (the drive breaks
  through quickly), so the tell is a short warning; a 0.15 s gate made it nearly invisible. The first
  cracks (3 cm, quarter-dark) read as grey twigs; they are now 5 cm and near black.

## Iteration: debris that does not linger (user, 2026-10-01)

- Feedback: dust and dribble look good as they appear but not as they stay; the dust did not behave
  like dust and should go sooner; clods sat on the ground for a second and then vanished.
- Landed clods no longer rest and sink (the settling particle system is gone): each crumbles where its
  flight meets the ground (floor, wall or ceiling) into a few crumbs bouncing off the surface's density
  gradient, plus a small puff for bigger clods, and is gone.
- Crumbs (spray, trickle, crumble, dribble) live until their arc meets the ground (the same density
  march, at the crumbs' 0.85 gravity) and vanish into it; only crumbs still airborne fade, at the end.
- Dust has air drag (limit-velocity drag 4): a puff bursts out, stops and thins within 0.7-1.4 s (was
  1.3-2.6 s drifting with the load). Shaft dust settles with a 2.5 s time constant (was 7 s), its motes
  live 2-3.5 s (was 6-10 s) and barely rise. Dribble wisps live 0.6-1 s. The trickle ends within 0.5 s.
- Feedback (screenshot of a shallow hole): a dust blob hung artificially above the hole. Shaft dust
  spawned up to 0.6 m above the mouth with 1.6x motes there; it now spawns only along the route below
  the mouth, at least 0.6 m under the surface (none at all for a load that close to the top).

## Iteration: crossed falls and the rupture tell's visibility (user, 2026-10-01)

- Feedback (screenshot): sometimes the two falls crossed just above the block. The falls follow the
  cable's transported frame but end at the block's real entries, and the block turns toward that frame
  with a lag; a frame that flipped round left the block half a turn behind, so each fall ran to the
  opposite entry. The block's entries are alike: it now turns toward whichever of the frame's two
  directions is nearer (never more than a quarter turn), and each fall enters on its own side.
- Feedback: no visible difference between the rupture tell variants. A stepped recording (1/30 s per
  frame, bent-tunnel scene, dribble + cracks) showed the cracks for one to three frames before the soil
  gave way and the computer surged: the jams this crane breaks last about 0.1-0.3 s, so the tell barely
  exists on screen; dribble crumbs are tiny at a few metres.
- Decision (user): drop the rupture tell and keep the break particles; it may be revisited later. The
  tell (strain tracking in `SalvageCrane.Contacts`, the dribble and the projected crack meshes in
  `SalvageCrane.Feedback`, the admin switch) is removed; concept 09's rule stands (debris only when
  ground is actually removed). If it returns, it needs jams long enough to be seen (or a bolder cue):
  measured jams here last 0.1-0.3 s.

## Iteration: a real lifting point (2026-10-02)
- Request: better shape, textures, shading and dirt for the lifting eye (it was a flat-coloured cylinder,
  hex and torus that tilted as one piece, base plate included).
- New model (`art/lifting-eye/create_assets.py`): `Base` (flange, low cup, three bolts) stays flat on the
  load; `Swivel` (a ball centred on the mark and a forged bow) turns about that centre, so the hook's seat
  stays `HookReach` from the mark and the crane's physics, rope view and seat math are unchanged.
  The ball turns without cutting the cup. One URP Lit material with maps baked in Blender (Cycles):
  worn amber powder coat chipped to steel, scratches, dust on upward faces, caked mud low down and in
  crevices, zinc with rust at the bolts; metallic/occlusion/smoothness packed in one mask.
- Rejected on the way: a tall neck above the ball (cut into the base when tilted); strong metallic
  chips and zinc (read navy-black, mirroring the sky).
- A real hoist ring keeps its body upright and only pivots the bail, but then the bail cannot both
  follow the pull and keep its hole on the hook's wire, so the hook would misalign. The swivel keeps
  the old orientation rule. On a sideways pull one bow leg can still dip into the load, as before.

## Iteration: eye timing, release and straight falls (2026-10-02)
- The eye turned toward the arriving hook from the moment deployment began, long before the hook was
  near. It now stands upright until the hook is ~0.6 s out (`SalvageCrane.EyeTurnStart/End`, by the
  hook's ride speed) and turns smoothly into place (`RecoveryMarkView.Aim` weight).
- The eye comes off the moment the crane lets go (Settling), not when the load has come to rest.
- Falls glitching in a straight shaft (screenshot): they were drawn together wherever the cable was
  below ground level, so a straight drop pinched them mid-shaft with a kink in each before spreading
  into the block. They now hang apart until the cable's first bend of more than 12 degrees from its run
  off the trolley (`CraneRopeView.FirstBend`), tapering together into that bend.

## Iteration: simpler falls (2026-10-02)
- Feedback: the cables still tangled for a second and untangled, again and again ("overcomplicating?").
  Causes: the falls snapped together past a 12 degree bend, which the wobbling simulated rope crossed back and
  forth; their sides were carried down the cable by parallel transport and the block eased its turn toward
  that frame, so the entry-side check flipped while the two disagreed.
- Now `CraneRopeView` squares the trolley's sheave spread to the cable at each point (the falls keep the
  trolley's side; a two-fall reeving does not twist), the block stays square to the sheaves the same way, and
  the falls draw together smoothly with how far the cable is pulled off the straight trolley-to-hook line
  (fully at 0.5 m). Checked frame by frame in slow motion down the straight shaft and the bent tunnel.

## Iteration: solid uniques and riding the load (2026-10-03)
- Feedback: when extracting uniques, give them physics against the player; standing on one, the player is
  dragged along, never passed through. `FindPhysics` now ignores the player only for commons. `LoadRide`
  (player folder, run by `FpsPlayer.Move`) finds a unique under or against the capsule after each move and,
  next frame, moves the player by that unique's rigid motion at the player's feet: all of it when standing on
  it, otherwise only the part pushing into the player. The crane ignores the player as soil (`Contacts`).
- Pushing the rider down onto the load (gravity) made them drop in steps after a descending load: the
  collider sits at the physics pose, up to a step ahead of the drawn, interpolated load. A carried rider gets
  no push down and counts as grounded; a jolt that lifts them more than 5 cm off the drawn surface (the haul's
  first yank) settles back at 1.5 m/s, and settling all the way to the skin pressed them onto the collider and
  jittered again. Measured in the Ground Lab shafts: rider and load vertical speed within about 0.1 m/s while
  lifting, carrying and setting down; a 5 t load driven into a standing player pushes them at a constant gap
  without slowing. On the lab computer the rider stands next to the hook, which bumps them when it swings.

## Iteration: shaft dust kept (user, 2026-10-05)

- Verdict: keep the dust that hangs in the passage after breaks, on by default. The admin **Shaft dust** A/B
  went (`FpsPlayer.ShaftDust`); the crane always adds breaks to the dust load.
