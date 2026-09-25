# Prototype Baseline Implementation

> **Notice:** This document is a factual inventory of systems currently functioning in the Unity prototype. Feature-specific v1 acceptance is recorded in the [completed task specs](tasks/completed/); other mechanics and art remain provisional and subject to refinement against `docs/concept/`.

## 1. Player & Controls (`unity/Assets/Runtime/Player/`)
- **Controller:** First-person `CharacterController` with smooth crouch (Left Ctrl, 35% speed) and modest sprint (Left Shift, 1.35x speed).
- **Tool ladder:** ten levels authored once in `EquipmentProgression.ToolProfiles()` and no longer serialized into `MainGame`; Developer admin → **Tool tuning** nudges bite/cadence/reach live and prints a paste-ready table beside the save directory (`tuning.txt`).
- **Jetpack & Rescue:** Vertical thrust and hover mechanics. Automatic zero-fuel rescue if stranded.
- **Input & Comfort:** Unity Input System with full runtime action rebinding. Hold-to-dig by default with a persisted toggle option. Camera FOV slider (55–90°), crosshair toggle, and preferences persistence (`Preferences/*.ini`).

## 2. Terrain & Excavation (`unity/Assets/Runtime/Terrain/`)
- **Voxel Engine:** Finite signed density field (24 × 24 × 100 m, authored by `SiteLayout`) running Burst-compiled surface-net meshing (0.125 m resolution) with synchronous collision publication. Reusable native workspaces and contiguous density copies bound per-cut work; support-search scratch memory is allocated during loading. Chunks are materialized on demand: 7,200 possible keys, 144 built on a fresh site, so chunk startup and load cost do not scale with depth.
- **Materials:** seeded undulating deposits of soil, clay and rock share the density lattice. Immutable material IDs persist exactly with the hole and are shared by save snapshots without copying; density alone determines occupancy. Surface meshes blend distinct soil grain, warm compacted clay and fractured grey rock textures from these same IDs. World-space mapping and shared halo weights keep chunk edges continuous.
- **Checkpoints:** dense density (~119 MB) plus compact material IDs (~30 MB for the shipped site) are validated and gzip-Fastest encoded on a worker thread, with bounded combined allocation and a 64 MB packed / 256 MB unpacked budget. Only the current format and excavation layout load; previous formats require a new game.
- **Digging:** Hold/toggle repeats shovel scoops through level six, then automatically uses drill-like thin cuts from level seven with sub-cell contact refinement and matching render/collision updates. Soil yields broad rounded cuts, clay narrower smooth shavings, and rock smaller faceted chips. Each affected sample keeps its own resistance; contact selects cadence and proportional energy cost automatically. Successful tool cuts publish material/position/normal/volume feedback for later audio. Every tool tier can cut every family; upgrades improve output. Detached ground and paper-thin strips clear locally while useful ledges remain. Collection preserves cutting cadence with both tool motions.
- **Admin Tools:** Session-only debug panel (`Ctrl+Shift+F10`) with **Motion: Automatic/Override** for comparing drill cuts with shovel scoops, selection across all tool levels, refill, and X-ray transparent ground (`Ctrl+Shift+X`). X-ray fades soil, hides surface grass and reveals nearby actual finds without changing exposure or collision. Restore normal rules and session load restore opaque ground and motion derived from the owned level.

## 3. Finds & Physics (`unity/Assets/Runtime/Interaction/`)
- **Finds:** full-size plain rocks form a dense layer immediately beneath the turf, with the ore
  ladder (coal first) beginning beneath them and deeper bands shifting the mix toward value.
  Placement uses enclosing spheres around actual visual/collision vertices with shallow soil
  cover; banded placement picks a target depth before searching nearby lateral positions, so
  each band stays populated from its top. A separate lower-reservoir allocation continues the
  progression. Populations are authored in the source catalogs, apply to new games, and saved
  finds keep their positions. Model size and mass remain authored; retired content is deleted.
- **Detection & Pickup:** Ordinary finds use aim-assisted reveal, 60% voxel exposure threshold, held aim pickup between digging ticks and immediately after a revealing cut. `FindProximityCollection` also collects clear finds within a close camera-centred area in front of the player during walking or held digging, at any height, with direct visibility and throw/drop exclusion. Released/falling finds have longer aimed reach; station/lifting reach stays separate. Aimed and nearby collection can share a held-input frame with its scheduled terrain cut; neither collection nor its animation adds a delay. With a full bag, the cutting ray passes through common finds to reachable ground while walls, equipment and uniques still block it; finds remain physical and recoverable.
- **Release:** Nearly exposed finds with clear interiors and only shallow surface contact become dynamic; substantial burial still anchors them. Gravity and collision determine falling/settling.
- **Handling:** Physical lift/drop (RMB) and throw (LMB). Carried finds track motion and settle physically on release; a slow creep on a slope counts as quiet, so finds stop instead of rolling away forever.
- **Prototype uniques:** Independent computer identities reuse Cosmic computer 7, with reserved underground envelopes before common placement. Each exists once per save, requires manually aimed cuts around its covering soil, remains anchored while buried, falls and settles once freed, cannot enter the bag or be sold, and records discovery depth on visible targeting. Source authoring lives in `art/retro-computer/catalog.json`.
- **Silent detector:** A separate bottom-right HUD panel shows three bars based on aim alignment toward a nearby, fully buried eligible find; direct aim gives the strongest level regardless of distance within range. Looking away, leaving range or uncovering any part turns that object's signal off. No arrow, direction/height label or revealed fallback remains. Small angular/range margins prevent flicker; deliberate changes of aim select another source immediately. Commons, observed/held finds and recovery/storage/display are silent. It costs no fuel or bag space, pauses with gameplay and evaluates only its small authored-source cache each frame, rebuilding that cache when the population changes.
- **Worksite equipment:** a reusable starter lamp kit is separate from the bag and battery. Rebindable lamp/mark actions preview placement, primary confirms, secondary cancels, and rotate adjusts orientation. Lamps attach to rough ground, walls, ceilings and fixed props; open-air placement or loose props use physics. Interact retrieves lamps or erases arrow/home/return-here stencils. Terrain edits release unsupported lamps physically and erase paint on removed surfaces; sustained winch contact knocks blocking lamps off their mounts. Lamp ownership survives both. Placement never emits light or digs behind the preview.
- **Rope recovery:** Aim at any visible part once sufficiently exposed and hold Interact. A surface glyph previews the eligible point, fills during the hold and becomes a checked mark following the load through deployment, rotation and reload. Terrain and objects occlude it; completion or planning failure removes it. The winch deploys through connected excavated air and pulls a dynamic computer at the marked point with a spring guide following the accepted bends. The bounded particle cable retains gravity, inertia and tunnel collision; hauling tension takes up slack without paying the spring's extension back out. Brisk reeling builds stronger force against a jam, ruptures a local patch and releases a fast, freely rotating surge. Hard opposing impacts can immediately break the next dirt contact; the committed removal spends impact energy and only residual momentum continues. Break size follows effort, while soft brushes and nearby untouched soil remain intact. Continued wedging uses short retries and automatically stronger pull; jitter alone cannot reset the jam. Real breaks synchronously update density and mesh/collision and emit pooled crumbs/dust. Blocking commons loosen without losing identity; mounted lamps can be knocked loose. Permanent obstacles remain intact, player fuel is unaffected, and pad delivery uses stronger damping. The load never waits for manual clearance or a retry. Pause/focus loss freezes load and cable and clears transient contact pressure; checkpoints retain the load's pose and motion and resume recovery automatically.
- **Dense-world cost:** meshes enclosed by pristine soil stop rendering; conservative bounds and nearby terrain edits reactivate them before small fragments can be missed. Terrain notifications use the existing physics spatial index to refresh nearby finds, with a complete scan on query overflow for large edits/reset/restore. Exposure uses a Burst-compiled batch over each find's local density neighborhood and one shared terrain workspace; support checks share the transform and fast density interpolation. Route planning has both work and elapsed-time limits, with at most one slice per rendered frame before hauling starts. Anchored physics callbacks sleep until a terrain change or explicit handling/restore; collision and save records remain intact.

## 4. Hub & Economy (`unity/Assets/Runtime/Player/`, `Runtime/Interaction/`)
- **Surface computer:** one Cosmic retro terminal opens selling for a carried haul, then upgrades immediately after Sell All or the last individual sale; an empty bag opens upgrades directly. The prompt reads simply `Use`, with no key prefix. Tool, battery and bag each have ten levels with independent sequential purchases from `EquipmentProgression` and its shared price ladder; parameters stay in code. The tool row previews the shovel-to-drill milestone; final-level rows remain visible and cannot charge again.
- **Refill Economy:** Paid battery recharge ($1 minimum, whole-dollar `$`).
- **Recovery yard:** One winch chooses an unoccupied receiving pad for each computer; the saved route retains that destination. Independent exhibit stands place the same stored objects into their own saved sockets; subsequent interactions show each find's name, original depth and story.

## 5. UI & Presentation (`unity/Assets/Runtime/UI/`)
- **UI Toolkit:** Single UI Document (`FpsHud`) driving the HUD, Pause menu, Settings tabs, and Station trading interfaces with unified grayscale styling.
- **Capacity warning:** Full inventory uses the same persistent resource banner as low fuel; simultaneous warnings stack above the bottom edge, without overlapping feedback.
- **Focus loss:** the game still pauses when the window loses focus, but the dim overlay and pause card are hidden while focus is elsewhere, so external screenshot tools capture the game rather than the pause screen.
- **Frame pacing:** startup is capped at 144 FPS before the scene loads; device settings then apply the saved frame limit or VSync choice. Display reset also defaults to 144 FPS.
- **Resolution:** rendering is fixed at 100%, with no scale control or stored scale preference. Borderless startup and switching use the current monitor's desktop resolution and aspect ratio; its resolution row reads Desktop. Fullscreen/windowed modes retain supported output choices, including 4K and higher, with timed Keep/Revert confirmation.
- **Graphics settings:** sun shadows (Off/Low/Medium/High), MSAA, texture mip quality and anisotropic filtering apply immediately and persist as device preferences. Defaults and Graphics reset use High sun shadows, 2× MSAA, full-resolution textures and High filtering; explicit saved choices remain unchanged. Sun shadows use a smaller two-cascade map with smooth filtering, with coarser Medium/Low tiers on the runtime URP clone; local lamp occlusion and the excavation daylight field remain active at every quality level. Depth priming is disabled, so MSAA Off renders the same surfaces as the multisampled settings.
- **Computer screens:** selling and upgrades share the existing fixed-size parts-board table (`Station.uss` + `ToolkitStationRows`) — money-only header, categories in their own columns, one clickable row per upgrade track, refill service or carried find. One click buys; nothing is selected first and nothing resizes.
- **Drained lakebed site:** `LakebedSiteSetup` regenerates the surroundings from a 1000 m window
  of the Pure Nature 2: Highlands demo's river canyon: demo terrain, cliffs, peaks, boulders,
  rubble, ruins, trees, rivers and waterfall as vendor prefab instances. The canyon floor is
  flooded into a widened lake with an exposed bathtub band; one drained mud section on the east
  shelf holds the round meadow opening. A terrain hole under a narrow soil collar
  (`Surface/Excavation rim`, the dig meadow's own material) keeps an exact circular edge and a closed
  roof above the grid corners, which stay reachable for lateral digging. One merged mesh of small
  pebbles borders the circle; a tinted copy of the Highlands grass layer and sparse terrain grass
  fade from the rim into the mud, with a few larger stones. One generated lake surface replaces the
  demo's sea-level planes and never crosses the dig column; the demo's baked canyon probe uses
  URP box projection and blending. Project-owned lake/river materials and the adapted BK water
  shader retain ripples with bounded foam and refraction that does not relight the riverbed.
  Walls and a 16 m flight ceiling on the Ignore Raycast layer keep the player on
  the drained section; scenery objects and terrain trees unseen from that volume are removed at
  setup (ID-colour renders plus terrain line-of-sight). Scenery reports the permanent-boundary
  prompt; terrain shadow casting is off because its casters ignore holes. Terrain detail and
  layered shading fall back sooner outside reachable ground. Scenery and project tree/bush
  variants keep full detail within about 50 m even at the widest FOV, the coarsest meshes and
  tree impostors only appear beyond about 200 m, and every switch blends briefly. Project copies
  of the foliage materials hide edge-on leaf cards over a narrow angle band instead of
  speckling canopies. The vendor waterfall
  splash particles are omitted because their material lacks its textures. Backdrop shadow casters are omitted
  beyond a buffer around the play area. Permanent rocks supply baked occlusion for the ground,
  flight and underground camera volumes; mutable soil, its preview, terrain and foliage never
  become baked occluders. Performance refresh preserves placements and terrain sculpting/paint.
  The computer, recharge, return anchor, winch, pads and stands keep their tested cluster on the
  south rim, lifted onto the lakebed ground; the spawn looks north up the canyon over the opening.
- **Meadow and soil:** eleven pack grass/flower/fern layers, seeded in change-driven instanced
  batches. Root support removes uprooted plants; a change-driven surface-density mask clips
  wind-displaced foliage over openings without clearing intact neighbours. The project grass
   shader also clips the round perimeter. Pack turf uses continuous top projection and a soft,
   textured soil transition; noon shadow bias prevents a tessellated self-shadow rim. Pack soil covers the top layer; compacted clay and fractured rock use separate approved texture/normal sets below it. Custom soil art/materials remain stored but unbound. Linear masks,
  preserved alpha, normal-map imports and the dry smoothness cap prevent white glare.
- **Presentation:** the Highlands demo's sky with a larger soft-glow sun disc, flat ambient, warm
  sun colour and exponential haze, a 3 km camera range and almost overhead midday sunlight; custom grass, clouds,
  sun and trial-tool art/imports are deleted. No first-person rig is present; the shaving/scoop
  transition follows the tool level, with a comparison override in development admin. The full licensed vendor pack remains available.
  The project color profile uses the Highlands ACES grade with restrained bloom, warm soil and
  rich surface colors. Lighting/water can be refreshed without regenerating scenery or terrain;
  existing project water-material and color-profile tuning survives setup runs.
  WindowsBuild always targets MainGame.
- **Underground lighting:** `ExcavationDaylight` derives daylight from connected excavated air, keeping shallow and middle-depth ground and short branches readable before gradually fading along deeper or longer routes. Soil and adapted URP Lit finds/boundaries attenuate sun, sky fill and reflections together; sealed rooms admit no daylight and there is no ambient brightness floor.
- **Work lights:** compact neutral lanterns illuminate a broad area in every direction with local soft shadows, gradual distant falloff and bounded close-range brightness, preserving soil texture beside the light. The point-light shadow atlas fits the entire kit; distant route lamps stop submitting lights while retaining their visible diffuser. No personal light, fuel drain or expiry. Marks conform to collision surfaces and remain readable by their shape when lit.

## 6. Persistence & Lifecycle (`unity/Assets/Runtime/Persistence/`)
- **Saving:** Current-format v11 whole-world snapshots (`WorldSaveController`), atomic disk write, 10 s background autosave, recovery from interruptions. Find lifecycle, display socket, accepted rope route, attachment, payload pose/velocities, guide progress, lamp kit ownership/motion and route marks share the terrain checkpoint; invalid ownership or progress is rejected.
- **Windows Process:** Single-instance reservation (`DesktopInstance`) focusing existing window on relaunch.
