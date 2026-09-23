# 002 — Hold-to-Mark Rope Extraction

> Exposure-ready finds use held Interact to dispatch a guided rope through excavated air. A spring connection pulls the dynamic load with reusable local clearance, compiled terrain/exposure calculations and bounded route planning; swing, rotation, blocking-find release and whole-world checkpoints preserve physical recovery.

## Objective and scope

Prove a visible, reliable extraction with [086's buried computer](086-buried-computer-unique.md). The player digs enough to recognize it, aims anywhere on its visible surface and holds the rebound Interact action (E by default). Completion marks that instance and starts rope deployment automatically; no return to a surface button is required.

This task implements the reusable hauling mechanism, receiving pad and in-flight persistence. `086` owns content/category, safe exhibit ownership and the first stand. Oversized content, sale transactions and commemorative records remain with `075`/`018`; bladder-assisted local releases and permanent landmarks remain `076`. Neither task is a prerequisite for this computer proof. The computer content and hauling mechanism are delivered together.

## Concept reference and changed design

- `05` §§1, 3: deliberate exposure precedes interaction; the unique stays physical, is recovered whole, never consumes bag capacity, and never pays money. Rope recovery also supports later oversized content with a different destination/transaction.
- `03` §§1, 2, 6–7: preserve the player's route, permanent boundaries, unrelated terrain and other finds. Visible/collision terrain always agrees. No pre-dug rescue shaft, map or global collapse simulation.
- `06` §§3, 5–8: no loot loss; the winch does not drain the player's dig/jetpack battery or invent a new upgrade/payment requirement. Future salvage is paid through the surface computer, never by repeated tagging.
- `07` §§1, 5: rim equipment and a visible surface destination; the recovered unique is stored safely before manual exhibit placement.
- `08` §§4, 7: use rebound input, respect menu/focus suppression, allow pause, and resume the exact world on load.
- `14` §§2–5: test the real object and an overhang/bent route early; core mechanics and muted-readable feedback precede audio polish.
- The requested behavior replaces the earlier fully-exposed, surface-button-only winch design. Partial exposure meeting the authored threshold is sufficient; the load clears its remaining soil attachment as extraction begins.

## Pre-implementation code and integration constraints

- `FpsInput` already computes held Interact internally, but `FpsInputFrame` exposes only `InteractPressed`. `FpsPlayer.TryInteract` is a tap dispatch to `IInteractionTarget`; `RefreshTargetPrompt` prioritizes buried-find prompts.
- `BuriedFind` supplies geometry exposure and actual-collider targeting. Use it as the source of eligibility; do not infer readiness from object distance, a raycast through dirt or a separate progress counter.
- `TerrainVolume.TryCut` requires a current player ray hit, edits `ExcavationGrid`, synchronously rebuilds affected chunks and fires `Changed`. It is not a suitable winch API as-is. Extend its shared edit-commit path; do not synthesize camera hits or edit meshes independently of density.
- `ExcavationGrid` already provides density sampling, removal accounting, local detached/sliver cleanup and copy-on-write snapshots. Add the needed swept removal there, preserving those invariants.
- `ExcavationDaylightGrid` already derives connected-air lighting, but its coarse resolution and capped propagation are unsuitable as authoritative extraction routing. Do not depend on light reaching the target, change lighting ownership or introduce a second terrain truth.
- `FindPhysics` can re-enable itself on a terrain event. Extraction needs explicit exclusive control, not just a one-time `enabled = false`.
- `WorldSaveController` has atomic whole-world persistence and dirty revisions. Extend it for the job rather than creating another save file or postponing all saves until arrival.

## Player flow

1. Before exposure readiness, retain the ordinary uncover-more prompt. The visible computer cannot be bagged or lifted.
2. Once ready, show **Hold [Interact] to mark for excavation** with a small progress indicator in the existing HUD. Any unobstructed collider point on that instance works; no tiny attachment hotspot. Moving aim between its parts preserves progress.
3. Letting go, aiming away, losing reach/visibility, opening a menu, rebinding or losing focus cancels the unfinished hold. Require a fresh armed interaction after those transitions; an already held key on menu close cannot finish a mark.
4. On completion, reserve the instance once and validate the route. Show a brief preparing state while bounded planning runs. A missing route leaves the computer in place, reports the obstruction in context and permits a later retry; no consumed charge, lost item or terrain carving on failure.
5. The visible hook/rope travels from the rim through the connected opening and down the route. It visibly attaches to the marked local surface point, with a short readable attachment beat.
6. The winch reels in. The computer follows the route back, stripping only the soil its swept hull needs, including its initial remaining burial. The player can move, look, follow or return to the surface; no camera takeover and no need to keep E held.
7. Lift clear of the opening, travel through a validated above-ground span and lower onto the receiving pad. The same object becomes safely stored and available to `086`'s stand. Give clear arrival feedback without automatically selling it or teleporting the player.

Confirmed input: “any place” means any visible point on the computer within normal interaction reach. The initial version runs one job at a time; it does not need a queue.

## Architecture and exact responsibilities

| Owner | Change |
|---|---|
| `FpsInputFrame`, `FpsInput` | Expose armed `InteractHeld`; preserve `InteractPressed` for existing stations. Reuse the existing binding, never raw `Keyboard.eKey`. |
| `FpsPlayer`, new `FindExtractionInteraction` in `Runtime/Player` | Own target identity, hold progress/reset and dispatch precedence. Run one world interaction path per frame; extraction marking cannot also open a station or repeat after completion. |
| `BuriedFind`, `DiscoveryField` | Supply exposure/category/state policy from `086`; validate reservation, exclusive recovery state and arrival transition. Prevent all other collection/handling paths while reserved or hauling. |
| New `ExtractionRoutePlanner` in `Runtime/Interaction` | Pure route search/simplification over density and obstacle queries; bounded incremental work, reusable scratch data, deterministic tie-breaking, no terrain mutation. |
| New `SalvageWinch` in `Runtime/Interaction` | Sole active-job owner: reservation, planning, hook descent, attachment, haul, surface delivery, stop/retry and checkpoint requests. It controls the payload pose, never a competing Rigidbody solver. |
| New `WinchRopeView` | Render the currently paid-out polyline, hook and attachment. Derive visuals from the job; no authoritative physics or separate persistent rope state. |
| `TerrainVolume`, `ExcavationGrid` | Add validated swept-load clearance and reuse one density/cleanup/mesh/collider/notification commit path for player and winch edits. |
| `FindPhysics` | Explicit external-recovery control mode suppresses soil release, settling, pose recovery and terrain-change wakeup while the winch owns the payload; restore the correct mode from lifecycle state. |
| `WorldSnapshot`, `WorldSaveCodec`, `WorldSaveController` | Capture and validate the job with payload/terrain state and observe progress revisions. |
| New `SalvageWinchSetup` under `Assets/Editor` | Idempotent MainGame rim fixture, receiving pad, anchors, rope material and wiring. Simple original fixture/hook/stand art comes through Blender MCP, with recipes, `.blend` sources and a local asset card under `art/salvage-winch/`. Do not run the scene builder or replace MainGame surroundings. |
| `FpsHud` / `GameHudView` | Reuse the existing prompt/feedback surface for hold progress, preparing, obstruction and arrival. No persistent objectives or new menu system. |

Hold duration, travel rates, clearance margins and planning work limits belong in a `SalvageWinchSettings` asset with useful Inspector fields, not scattered constants or item-price data in documentation. Item-specific exposure and shape remain catalog/prefab data. No new package or paid rope asset is needed for this guided first implementation.

## Route planning: follow the excavation before clearing a load-sized passage

1. Capture a read-only density snapshot/revision when the hold completes. Enumerate connected excavated air from the exposed attachment area to a valid top opening; a top exit under the permanent apron is invalid. Attach the fixed rim fixture and pad using a separately checked above-ground route.
2. Search a sparse voxel graph only in the connected air region. Use coarse candidates to accelerate broad spaces with voxel-scale refinement for narrow links and endpoints; accept edges only after density segment-clearance checks. Do not let a coarse grid declare a real player tunnel unreachable. Explicitly bound work per frame and total explored nodes; return an actionable failure instead of freezing or leaving endless “preparing.”
3. Use deterministic A* toward the surface, then remove unnecessary bends only across verified clear segments. The cable path must already fit through open air. The *object* may be wider and clear surrounding soil during hauling. Do not use a low-cost straight vertical tunnel through intact dirt as a substitute for a connected route.
4. Seed from the visible local hit and nearby clear grid points, excluding the target's own hull from obstacle tests. Route the hook to that hit; carry the corresponding fixed local attachment offset when deriving the payload center. Validate the payload envelope against permanent colliders, including at initial release and the rim transition, before committing movement.
5. Simplify only across verified open-air segments. Preserve bends beneath roofs and around walls; never spline or cut a diagonal through untouched ceilings. Include hook/rope thickness in segment clearance, and account for any visual corner rounding.
6. Lock the accepted route for this haul. Subsequent clearance must not tempt the planner into shortening it into a newly cut shortcut. If a new permanent obstacle invalidates an upcoming segment, pause and replan from the current pose through available air, or retain a resumable obstruction state. Normal terrain removal cannot close the route.
7. Persist the accepted terrain-local waypoints, exact attachment offset, current phase and distance. This reproduces the haul on reload without pathfinding against its already widened hole and changing direction unexpectedly. Bound and validate waypoint count, positions and progress.

The route follows the tunnel geometry, not a recording of the player's footsteps. Loops, jumps and backtracking do not create rope knots. Avoid a NavMesh or breadcrumb system: neither describes arbitrary player-carved three-dimensional air reliably enough here.

## Hauling, terrain clearance and rope presentation

- Original approach (superseded by the physics iteration below): use guided kinematic movement along arc length. Keep a fixed payload orientation underground for the first computer, avoiding unstable rotation at corners; any final upright rotation at the pad gets its own validated swept clearance. This is a deliberate readable prototype, not a chain of joints simulating rope tension.
- For each bounded movement substep, compute the swept oriented collision envelope from the previous pose to the proposed pose. Add a small clearance/interpolation margin. Cover the entire segment, including at low frame rates; do not subtract disconnected spheres only at the endpoints.
- Add a swept-box/hull removal operation to `ExcavationGrid`, using bounded interpolated shapes and a conservative padding bound for translation/rotation. Share removal accounting, local crumb/sliver cleanup and changed bounds with existing cuts. Test sweep continuity and locality independently of presentation.
- `SalvageWinch` validates permanent boundaries; `TerrainVolume` commits the bounded density edit, synchronously rebuilds matching collision/render chunks, then emits the existing `Changed` notification once. Move the payload only after that substep's clearance is committed. Handle a zero-removal step as valid movement through already empty air.
- Spend no player battery, trigger no shovel cooldown/stroke count and award no loot for raw removed soil. Other finds survive unchanged; normal exposure updates can reveal them, but a winch event does not invoke automatic collection.
- Time-slice long pulls with a bounded distance/chunk-work budget. Slow the load when geometry work is expensive, rather than clearing the entire route in one frame. Snapshot capture must occur between committed substeps, never between removal and the associated payload pose update.
- The load cannot destroy the apron, bedrock, stations, boundary colliders, other unique props or later lamps. Static blockers are included in route feasibility. Loose ordinary finds retain their identities; avoid explosive collision forces. Preserve the existing no-player-collision policy for finds so the payload cannot crush, carry or trap the player.
- The rope follows the same checked polyline. Deployment renders only paid-out length; attachment uses the saved surface hit; hauling shortens toward the load. Preserve necessary bend points so the rope never straightens through a covered corner. The simple hook connection and rim guide explain the pull without requiring a simulation.
- The first pass uses the original Blender-made fixtures and a procedural rope renderer with an underground-lighting-compatible material. Audio, imported machinery art, dust polish and cable-strain animation are later work. The visible attachment and terrain clearance must communicate the action when muted.

## State, interruption and persistence

Job phases: `Planning → Deploying → Attaching → Hauling → Delivering → Complete`, with a resumable `Obstructed` state. `Planning` reserves the unique but does not remove soil. A preflight failure releases that reservation; after attachment, obstruction retains the last safe pose and job rather than dropping or deleting the load.

- Reserve synchronously against instance identity and current lifecycle; repeated input cannot create another job. Only a valid delivery changes `Extracting` to `Stored` once.
- Save job phase, instance ID, density-local route, hook/payload progress, attach point and current payload pose in the same checkpoint as terrain and find records. Before a route exists, save the pending target and restart only planning on load. Hold progress itself is not saved.
- Increment extraction revision during travel and on every phase transition, including rope-only deployment when terrain/payload does not move. Request event checkpoints for accepted marking, attachment, obstruction and arrival; use normal autosave for intermediate travel, avoiding one disk write per frame.
- Pause, focus loss and blocking persistence/menu states stop deployment, movement and terrain edits together. Restore rebuilds terrain/colliders, recreates the single find, then reconstructs rope and exclusive payload control before resuming. Surface delivery and display rotation are saved too.
- Validate state/job agreement, legal enum values, finite coordinates, normalized rotation, supported route bounds, progress within length, correct content policy and no duplicate owner. Keep the existing last-valid-checkpoint corruption recovery. Old formats are unsupported; no compatibility shims.
- New Game cancels jobs before destroying the old population. Player rescue changes only player state and does not destroy, complete or duplicate an active recovery. If a runtime obstruction persists, contextual retry recomputes from the saved safe pose; no forced teleport fallback.

## Alternatives and trade-offs

- **Guided rope and kinematic payload — selected:** predictable path, bounded terrain edits and exact saves. Cable bends are intentionally stylized; expensive fully physical rope is unnecessary for proving the interaction.
- **Joint-based rope/free rigidbody — rejected for this slice:** jams at corners, solver jitter and force-driven player/loot collisions undermine the guaranteed extraction and make restore harder.
- **Straight vertical lift — rejected:** destroys the shape of lateral excavation and ignores the requested route.
- **Record player footsteps — rejected:** depends on traversal history and creates loops/reload complexity; the terrain already describes valid routes.
- **Teleport to surface — rejected for rope targets:** removes the visible whole-object haul. `076` remains a separate content/release method, not an automatic failure fallback.

## Acceptance criteria and verification

- Hold marking succeeds on the front, side and back of an exposure-ready computer when that surface is visible and within reach. It fails through dirt, at excessive distance and below threshold. Release/look-away/menu/focus/rebind resets unfinished progress; repeated held input starts only one job. Verify an alternate binding as well as E.
- Full bag, empty battery and continued player movement do not block or cancel an accepted job. Shop tap interactions and ordinary dig/collection cadence still work.
- In straight, bent, lateral-under-roof, narrow-bottleneck and branching tunnels, the hook reaches the computer and the whole load exits by a route through pre-existing connected air. The test for bends asserts that a vertical dirt shortcut and unrelated overhang remain intact.
- The initial remaining burial and actual load bottlenecks clear visibly as the load reaches them. Terrain density, rendered meshes and colliders agree immediately; a low-frame-rate sweep leaves no missed sliver or tunnelling gap. Unrelated ledges and all neighbouring finds remain.
- Permanent apron/side/bottom barriers and yard fixtures never disappear. A feasible alternate route is used; an impossible route reports a recoverable obstruction without consuming the unique or cutting terrain during planning.
- Save/load during planning, hook descent, attachment, mid-cut hauling, delivery, pad storage and display restores exactly one computer and a consistent hole/job. Interrupted writes and repeated reloads cannot grant duplicate ownership. Arrival records discovery depth from before the haul.
- Targeted high-value tests: route connectivity/simplification and permanent obstacles; swept density removal/cleanup across chunk boundaries; lifecycle reservation and full-bag exclusion; input cancellation; snapshot validation and interrupted-write recovery. Extend existing common pickup and terrain/save regression suites. Do not test decorative rope geometry or HUD pixel layout with unit tests.
- Profile route search and hauling on a substantially excavated site. Enforce incremental planner work and bounded substeps, measure worst-frame meshing/capture cost, and verify no whole-volume scan or large allocation occurs every frame. A smaller step/work budget is the first response to hitches, not asynchronous visible/collision divergence.
- Warning-free compile, relevant EditMode/PlayMode tests, Odin MainGame validation and direct gameplay review pass. Produce a fresh `builds/windows/SomethingDownThere.exe` through the established Editor build command. Complete/archive `086` and `002` together and update current-state docs only after the finished loop works.

## Implementation decisions

- Original verification (superseded): keep the load orientation fixed during hauling. Surface delivery crosses above the receiving pad before lowering vertically, avoiding the winch base. The exhibit applies its authored outward-facing rotation.
- The coarse air search retries at density resolution; work and node counts are bounded. Accepted waypoints remain fixed during a haul, including after reload.
- Swept oriented boxes reuse existing soil cleanup and synchronous render/collision updates. The measured bent-tunnel fixture has bounded planning and movement calls; occasional meshing peaks remain a later whole-game performance concern.

## Verification

- The EditMode suite passes all 236 cases. The full PlayMode run plus focused reruns verifies all 200 executed cases; old common-only fixture assumptions were corrected for the unique and new yard collision, and an inspection-command timeout was rerun without diagnostic interference.
- The recovery fixture covers a full bag, cancelled/rearmed hold, bent narrow tunnel, swept widening, mid-haul codec/instance restoration, arrival, repeated display interaction, stable identity and unchanged fuel. Focused snapshot tests cover every job phase, storage/display reload and corrupt ownership/progress.
- Direct Play Mode review verifies the final non-emissive computer, visible cable and hook, above-pad delivery, aimed stand interaction, outward display and depth/story prompts. Review captures remain under ignored `Logs/`.
- Warning-free script compilation and Odin MainGame build validation pass. The Windows build succeeds with no errors; its sole warning is the existing disabled runtime Pipeline connection notice. The owned convex hull opts into collision pre-baking.
- Local Editor bent-tunnel profiling measured bounded planning and swept-mesh calls; full-site generation stays inside its existing warm budget. This is a focused check, not long-session release qualification.

## Physics recovery iteration

- Replace the original kinematic, fixed-orientation haul with a dynamic payload and spring connection to a guided winch point. The earlier kinematic choice is superseded. Preserve the accepted route bends; do not teleport the load or overwrite its rotation.
- Clear only the current and predicted rotating hull through the shared terrain path before each physics step. Permanent colliders remain physical obstacles; persistent stalls pause for retry.
- Uncovered unique finds use ordinary release/gravity before marking. The winch temporarily secures a mark during deployment, owns dynamics once attached, and secures the final physical resting pose at the receiving pad.
- Pause and restore retain linear/angular motion alongside guide progress and the actual find pose. Save validation permits bounded rope stretch rather than requiring an exact rail pose; only the current save format is supported.
- Verify falling before marking, rotation and contacts during a bent haul, no soil edits on pause, mid-haul velocity/pose restore, pad arrival and one exhibit identity.
- Verification: top- and side-attachment Play Mode runs cover pre-mark falling, free rotation, wall contact/retry, pause, velocity/pose reload, pad settling and display identity. Extraction/save-format and X-ray regressions pass; a rendered review shows the tilted load hanging from its real attachment. The Windows player builds successfully with only the existing optional Pipeline runtime warning.

## Wedged-load clearance correction

- The reproduced stall had a tilted payload pinned between partly buried, kinematic coal pieces. Velocity-only prediction repeatedly recut the same cavity once motion stopped.
- Clearance follows the intended pull as well as actual velocity, includes voxel mesh tolerance, and locally frees soil around common finds in direct contact with the current or predicted payload. Ordinary support/release rules then make those finds dynamic; no finds are deleted or collected by the winch.
- Keep actual rigid-body rotation, the accepted tunnel route, permanent obstacle blocking and checkpoint ownership. Verify the copied stalled world with its real surrounding population as well as populated angled-passage regression coverage.
- Verification: populated angled, top/side attachment and ordinary release-threshold checks pass. Retrying the copied stalled player checkpoint reaches the pad with the same unique identity and unchanged population size. The Windows player is rebuilt; the current save format is unchanged, and already-paused jobs use the existing Interact retry.

## Recovery frame pacing iteration

- Profiling the copied populated, angled recovery identifies repeated terrain commits and full-population exposure notifications as the expensive path; fixed-step catch-up amplifies those costs into long frames.
- Reuse a locally cleared box until the complete predicted swept hull, including rotated corners and pull direction, leaves it. An additional cell provides reuse distance; every necessary cut still rebuilds collision and visuals synchronously. External terrain edits/reset/restore and rig replacement invalidate reuse.
- `DiscoveryField` queries the existing physics spatial index for local notifications, including moved finds; a saturated query falls back to the complete bounds scan so reset/restore and large edits cannot omit finds. Existing colliders, identities, release policy and save format remain authoritative.
- Exposure and soil-support checks resolve the find-to-terrain transform once per sample batch. The existing standalone surface validation player also accepts an explicitly copied obstructed checkpoint for recovery profiling without profile writes.
- Acceptance: compare the same copied recovery with its complete population and X-ray camera; verify worst-frame and terrain work reductions, tilted recovery completion, rotating-corner clearance, local/repositioned-find exposure, large reset, pause and mid-haul reload. Rebuild the Windows player after focused regressions.
- Verification: terrain/extraction EditMode coverage, all recovery and discovery PlayMode cases, and targeted soil-release/wake checks pass. The populated copied checkpoint reaches storage in standalone runs with X-ray both on and off, preserving the unique identity and population. On the local Ryzen 9700X/RX 9060 XT at 1080p with the normal frame cap reapplied during hauling, measured median/p95 frames are 6.9/8.4 ms normally and 12.5/20.9 ms with X-ray; worst frames are 59.8/74.6 ms, so this is a focused improvement rather than a claim of hitch-free release qualification. The reusable fixture records the actual device cap and uses the same pacing for idle and haul comparisons.
- The Windows player is rebuilt after warning-free script compilation and MainGame validation; the only build warning remains the optional disabled Pipeline runtime connection. Save format and current-profile support are unchanged.

## Ascent hitch correction

- The first performance pass improves total work but retains noticeable normal-rendering spikes. An uncropped frame trace identifies surface-net generation, exposure interpolation and first-cut support workspace allocation; median FPS is not an acceptance metric for this issue.
- Compile the existing surface-net algorithm and local exposure interpolation with Unity's installed Burst/Collections/Mathematics support, reuse and dispose terrain-owned native workspaces, and publish render/collision together. Copy density neighborhoods by contiguous rows and use a checked interior interpolation path; side, bottom and upper-air sample rules remain identical. Allocate the support-search workspace during loading.
- Cap route-search slices by elapsed time as well as node work, run at most one planning slice per rendered frame, and start hauling on a later frame. This prevents first-cut work from stacking with route completion and catch-up physics steps.
- Acceptance: check peak and p99 frames during the same populated, normal-rendering ascent; verify density interpolation/ghost rows, winding/seam normals, same-stroke collider updates, player digging, physical recovery and save/load. Keep current saves and all find identities intact.
- Verification: the copied populated ascent with X-ray off, fixed camera, 1080p and the same device cap improves p99 from 26.7 to 13.6 ms and peak from 66.5 to 29.2 ms; frames above 16.7 ms fall from 64 to 3. The first pull retains a smaller one-off spike. Storage identity, population and removed volume match the original replay. This measures the local machine, not minimum-hardware qualification.
- All EditMode tests and focused terrain, discovery, physical recovery and release/wake PlayMode checks pass. The fresh Windows player includes the compiled native jobs and passes MainGame build validation with no script warnings or errors; its only build warning is the existing optional disabled Pipeline runtime connection.
