# 105 — Tool Rig Rebuild (quality, physical sense, variations)

**Status:** complete. The first-person tool is the purchased western shovel at every shovel level and the purchased
hand mining drill (its head spinning on a pivot along its axis) at every drill level, with no per-level bolt-ons until
the user designs them; each shovel stroke pries and scoops, removing its dirt at the scoop.

## Objective

Rebuild the first-person tool (`001`) as a believable garage-built machine: a small, well-made shovel that
grows into a powered drill, where every bolt-on has a job, a mount and a power path, and every level adds
something you can see. Offer several shovel heads and drill bits to compare in the Developer admin.

## Concept reference

- `04` §1: one machine, never switched; upgrades bolt onto the same object; motors, battery packs, pipes,
  reinforcement, welded plates, cables and a late nozzle; the silhouette grows ridiculous while staying the
  same machine. Shovel scoops to level 6, continuous drilling from level 7 to 12 (twelve levels).
- `04` §10: the late nozzle is an attachment on the same machine; the final look is a shovel that has clearly
  become a cannon. Not a weapon, not a light source.
- `09` §1: stylized painted low-poly with strong silhouettes and restrained texture detail; custom models share
  one style with the packs; AI-assisted modelling with real references is allowed. `09` §3: deadpan absurdity,
  "a welded, bolted, over-batteried monster", no cartoon touches.
- `001`: tool only, no hands; rises from the bottom edge right of centre, blade face to the view, head and
  bolt-ons near it visible; parts named `L<from>[-<to>]_<Part>__<Material>`; `Spin` parts turn about their
  forward axis; no shadows; perspective-exact scaling.

## Analysis of the current rig (screenshots `unity/Logs/fx/rig_side_sheet.png`, `rig_back_sheet.png`)

- All parts are untextured primitives in seven flat colours; the blade is large (22-29 cm wide).
- L2 "reinforced edge" is a thin bar floating on the blade face. L3 side plates hang beside the blade.
- L4 motor sits under the shaft with a belt into the socket: it drives nothing. L5 battery cable runs to it.
- L6 pipes end in mid-air; the "head guard" is a bar across the blade's face.
- L7 drill bit grows out of the blade's face at an angle, not coaxial with any motor or gearbox.
- L10 nozzle is a tube laid over the top with a hose loop; nothing feeds it. L11 and L12 equal L10.

## Real references

- Round-point digging shovel: forged blade, rolled foot treads on the top edge, closed socket with a back
  ridge (frog) and a rivet, ash handle ~38 mm. Border spade and drain/trench spade for the smaller heads.
- Vibrating/clay spade: an SDS-max breaker driving a clay-spade bit; concrete vibrators (eccentric mass).
- Earth augers (powerhead, gearbox, auger flights with a fishtail point), SDS rock drill bits (spiral flutes,
  carbide cross tip), core bits (thin-wall barrel, cutting segments, pilot drill), 3-jaw keyed chucks.
- Power-tool battery packs on slide rails; inline switches; cable glands; hose clamps and U-bolts.
- Air spade (supersonic compressed-air excavation nozzle) fed from a tank through a regulator; small 12 V
  piston compressors.

## Design: one power path, mounted where it makes sense

Coordinates: tool along +z, grip end at 0, socket ~0.9 m, head beyond. Only the head end shows in play, so
every bolt-on sits on the neck between ~0.6 m and the head, on the visible (upper/right) side.

| Level | Adds (and why it is there) |
|---|---|
| 1 | Small shovel (head variant), closed riveted socket, ash shaft |
| 2 | Welded wide foot tread and a back rib (stiffer blade); steel sleeve with hose clamps over the cracked shaft end |
| 3 | A hardened edge strip bolted along the cutting edge: round the whole point on the round point, across the tip and corners on the spades |
| 4 | Vibrator clamped on top of the socket just behind the blade (the vibration goes straight into the steel; on the wooden shaft it would only shake the hands), one battery pack on a holder behind the sleeve, cable and inline switch |
| 5 | Second battery in a dual holder driving a second vibrator beside the first: both strapped to a saddle plate on the same clamp rings, each with a fan shroud and its own cable |
| 6 | A bent-tube guard over both vibrators, its feet bolted to the saddle plate's ends (the neck goes into the ground when the blade is driven in), an auxiliary side handle |
| 7 | **Drill:** blade off; a welded flange on the shaft end carries a drill motor, planetary gear head and keyed chuck holding the bit (bit variant); the batteries now feed the drill |
| 8 | Second motor beside the first into a summing gearbox with a gear guard |
| 9 | Battery rack (four packs) in a welded frame, cable loom; bigger bit |
| 10 | **Nozzle:** an air lance clamped along the gear head, its supersonic tip beside the bit, fed by a hose from an air tank with a regulator and gauge strapped to the rack |
| 11 | A piston compressor on the frame refilling the tank; a second lance on the other side |
| 12 | A nozzle ring of jets round the bit's root fed by both lances, a welded roll cage over the motors, the biggest bit |

Variants (Developer admin, session-only, like the boundary): **Shovel head** — Round point, Square spade, Trench
spade (all smaller than before); **Drill bit** — Earth auger, Rock drill, Core drill. Part names gain a variant
tag after the level range (`L01-06_S2-Blade__Steel`, `L07-07_D3-BitSpin__Bit`); untagged parts belong to all.

## Art

- Blender MCP recipe `art/tool-rig/create_assets.py`: bmesh/curve geometry with bevels, real dimensions, every
  part touching what holds it; no stretched textures (packed UVs per material).
- Materials baked from procedural graphs like the lifting eye: forged steel, powder-coat paint, black oxide,
  ash wood, battery plastic, rubber; albedo carries only colour, wear and dirt (no light); occlusion and
  smoothness in the mask, a normal map for fine relief.
- One texture set per material family (`Steel`, `Bit`, `Paint`, `Dark`, `Wood`, `Plastic`, `Rubber`, `Red`); the
  bits have their own so the blades keep the steel atlas. Steel, Bit and Paint bake at 2048, the rest at 1024.
  Smart projection, then per-part texel weights (the blade most, the far grip least) and the shaft cut into six
  near-square bands before one pack; a single long strip left the shaft at ~0.5 texels/mm.
- Occlusion in the bake looks only at the part itself: every level and variant overlaps in the bake scene.
- Checked with turntable sheets per level and variant, and in the first-person pose.

## First-person pose

The drill reaches 16-27 cm further than the blade, so its bit came within a few pixels of the crosshair at
level 12. Drill levels are tipped down a few degrees and set back slightly along the tool (`ToolRigPresenter`),
so the bit ends about where the blade did while the motors stay in view. Setting it back alone far enough
pushed the motors off the bottom of the screen and left only the bit.

## Rejected along the way

- Level 5 as a second battery and a fan shroud only: both sit behind the visible neck, so levels 4 and 5
  looked the same in first person; the second vibrator makes the extra battery visible.

- Teeth through the edge (L3) read as horns; the edge strip replaced them.
- Vibrator on the wooden shaft behind the sleeve: out of view in first person, and wood damps the vibration.
- Flat-bar braces from the vibrator mount to the blade's shoulders (L6): with the vibrator on the socket they
  had nothing to brace and crossed the soil's path over the blade.
- First bake: occlusion saw the other levels' parts in the same space, so everything came out mud-caked with
  black chips; occlusion is now local, chips and dirt thresholds raised, wood banding softened.
- Glossy steel (roughness ~0.3) mirrored the ground and sky in Unity, so flights and treads read as
  see-through; steel is satin now (dull patina, brighter where scoured). Wavy scratch lines read as
  procedural; scratches run straight along the tool, and blades scour bright toward the tip.
- The frog as a separate lathe floated 6 mm off the blade's back and the socket poked through the face;
  the face now sits in front of the socket and the frog is lofted out of the socket onto the blade.

## Acceptance criteria

1. Every level 1-12 shows a different machine for every variant pair; levels 1-6 have one blade, 7-12 one
   spinning bit; every powered part connects to a battery by a cable, every attachment touches its mount.
2. The shovel head is clearly smaller than before; the first-person pose still shows the head and bolt-ons.
3. Textured, worn materials in the packs' painted style; screenshots per level and variant reviewed for
   physical sense.
4. Admin switches for shovel head and drill bit; the rig tests cover the naming and per-level rules.

## Iteration: a painted level-1 shovel (2026-10-03)

- Verdict: the rebuilt shovel and drill looked worse than before. The user liked Poly Haven's Rusted Spade 01
  (too realistic) and a toy-like shovel (blue D-grip, yellow shaft, grey round blade; too simple), and asked
  for something in between as the level-1 shovel, in 2-3 variants, to build the upgrades on later.
- Now: Garden spade (square blade, pressed-steel T-grip, graphite paint), Round shovel (round point with a
  pressed crease, red paint, honey shaft, blue plastic D-grip) and Trench spade (narrow tapered blade, Y-grip,
  green paint). Each has a closed riveted socket whose end is forged into a frog on the blade's back, a plate
  ground thin toward its cutting edge, and rolled foot treads.
- Look: one `Shovel` texture set, baked from a single painted graph driven by each part's colours (object
  properties): clean colour with soft tonal variation, bare steel along the cutting edge with an uneven
  border, light edge wear found with a rounded-normal test (pointiness blotched the small round parts), broad
  wood grain, soil toward the tip only. `MODE = 'look'` renders it in Cycles before baking.
- Pointed tips collapsing to a single point shaded into a dark blob; the plate now ends where the tip is 1 cm wide.
- The level 2-6 bolt-ons (tread plate, rib, hard edge, vibrators) still fit the new socket and blades but keep
  the earlier baked look; they are to be redone on top of the chosen shovel.

## Iteration: a detailed round shovel and a low-poly spade (2026-10-03)

- Verdict: the round shovel is liked but lacks details; also wanted, a shovel like A Game About Digging a Hole's.
- Round shovel: a bead pressed round the blade 2.4 cm inside the cutting edge, a deeper crease, a maker's label
  in the upper corner (yellow card, black border and stripes standing in for print), soil scuffs streaking the
  face's lower half, treads worn bright by the boot with pressed dimples (normal map), a ribbed rubber cushion
  grip, and a D-grip of a steel yoke ending in eyes round a pin with a grooved plastic hold.
- Low-poly spade (after Steam screenshots of that game, not copied): a thick cream plate whose face slopes to
  every edge round a flat panel, a ten-sided orange handle lying on its face to a steel point, a bent bracket
  bolted over it, and an eight-sided T-grip; flat-shaded, hardly any wear. Its face sits behind the shaft's axis
  (`face_y`, `plate` per head).
- Rejected: an oval label across the crease bent into a coffee-bean shape; ribs on the treads read as comb
  teeth from behind; tread shells sharing exact vertices with the blade flipped the top edge's normals.
- `MODE = 'rebake'` re-bakes only the shovel's textures on the scene the last final run left loaded (about four
  minutes instead of twenty), for colour changes.

## Iteration: two weathered shovels (2026-10-03)

- Verdict: keep the round shovel and the low-poly spade, drop the garden and trench spades, and redesign both with
  more detail: dirt and dust like real tools, not shiny or vivid. Only level 1 matters for now.
- Round shovel: a slightly bigger blade (18 x 24.5 cm) with dents knocked into its cutting edge, a wooden hold on
  a steel-yoke D-handle (the blue plastic and the rubber cushion went).
- Low-poly spade: a faceted spine down the plate, rivets in its top corners, a steel ferrule where the handle meets
  the blade, and black tape wound round the handle.
- Weathering (one graph, amounts per part): paint faded by the sun, soil scrubbing the blade's lower part down to
  dull scratched steel with a border streaking along the dig, chipped edges with rust at the chips' borders (the
  cutting edge stays bright), fine scratches, sharp-edged soil stains toward the tip, droplets up the socket and
  handle, dried mud caked into crevices and the treads, a dust film heavier on upward faces, grime where hands
  hold, the label mostly peeled. Rejected on the way: a heavy dust film and sun fade washed the red to pink;
  large soft noise blotches read as CG camouflage, not dirt; a strongly streaked scrub border read as icicles.
- Contour lines over the round blade's paint were DXT1 banding: its 5-6-5 colours step the paint's soft gradients and
  the colour grade lifts the steps into lines (they stayed with contact shading and the normal map off). Tool rig
  textures import as BC7 (`TextureImporterCompression.CompressedHQ`). Also kept: the edge test ignores the pressed
  crease and bead, caked mud needs a real hollow and a noise patch together, the blade mesh is fine enough
  (84 x 64) for the bead to shade smoothly, soil is light and patchy (a solid dark cap read as chocolate), and bare
  steel is rough and only partly metallic (it read as chrome).

## Iteration: the purchased western shovel (2026-10-04)

- Verdict: both made shovels were rejected; the level-1 shovel is now the Stylized Western Shovel the user bought
  (an exact copy of a paid model was declined; buying and modifying it was the licensed route). Its 32 cm blade
  was too wide: Configure Tool Rig builds copies narrowed beyond the socket's straps to 100, 80 and 65% (normals
  and tangents follow the squeeze), a copy of its shaft clipped where the drill's flange covers it for levels 7-12,
  and a URP material on its maps as imported (read as linear, its sRGB-imported metallic map turned the blade black).
- The recipe lost its shovels, their per-blade bolt-ons (tread plate, rib, hard edge), the shovel texture set and
  the look and rebake modes; level 3 now adds a second clamp on the sleeve. Upgrades are to be redone on this shovel.

## Iteration: original width, smaller, stroke styles (2026-10-04)

- Verdict: the bought width is right (the 80 and 65% copies and the shovel variant tag `S<n>` went); the tool must
  be a bit smaller overall; and stroke animations to try. Developer admin **Tool size** scales the whole rig to 90,
  80 or 70% toward its socket (its screen place holds); **Stroke** picks Thrust, Scoop and lift (turning about the
  hands), Chop, Pry (levering on the blade's tip) or Scoop and toss (`ToolRigPresenter.StrokeMotion`).

## Iteration: rounded drill bits (2026-10-04)

- Verdict: the bits' thin, wide edges (auger flights, flutes, the core drill's rim) drew the eye while spinning; the
  user wanted the first rig's kind of ending. Now three short bits whose threads are rounded tubes (`thread`):
  Cone screw (two threads up a tapering cone, the first rig's bit refined), Round auger (one thick flight seated
  half in its core; a flight standing off the core read as a loose spring) and Rock screw (one big thread round a
  fat cone), in a darker, less scoured steel.
- Then too thin: the bodies are now `BIT_GIRTH` (1.75x) wider, about as wide as the motor, flaring from a short
  shank through a collar; the threads ease in and out at their ends and use 18-sided tubes.
- Then "way too small; look at real ones": one-person earth augers carry 8 x 36 in (20 x 91 cm) bits, bigger than
  the engine. The bits are now real width (20 cm, growing 12 and 25% at levels 9 and 12) and shortened to 55 cm, on a
  pinned coupling with a spring shock absorber (flex coil): Earth auger (one flight, fishtail point), Rock auger (two
  flights, rounded carbide teeth, pilot cone) and Cone screw (a ground-screw cone with one thick thread). Flights are
  5 mm plates with a rounded bead along the rim, so their edge does not flicker; the short rounded bits are gone.
- With real-size bits the drill levels sit further back along the tool (0.17 model metres) and tip down 7 degrees, so
  the bit ends below the crosshair; 0.35 hid the motors entirely, 0.02 put the bit's tip at the crosshair.
- Then only the cone screw was kept, in four variants (`CONE_SCREWS`: single thread, twin thread, stubby, fine
  thread), the same bit at every drill level (no growth at 9 and 12); the earth and rock augers went. Its base is
  fatter: a 9 cm hex collar flaring into the cone, in place of the thin sprung coupling.
- The joint where the bit meets the drill was still thin: an 8.6 cm chuck on a 3 cm spindle read as a neck
  between a 13 cm gearbox and a 15 cm cone. The chuck became a 12.5 cm drive hub with a bolted flange straight on
  the gear head or gearbox face (no spindle), the bit's hex collar matches it, and the jet ring moved out to 17.6 cm.

## Iteration: one thick drill, 75% size, pry strokes (2026-10-04)

- Verdict: the cone screws read as plugs (a smooth cone flaring from a narrow collar), and the base was still thin.
  Now one bit, thick all the way and widest at its base: a 13.6 cm drive hub, a 16 cm bolted collar, a straight
  11.6 cm screw body wound with one heavy rounded thread (about as wide as the collar) and a short cutting point.
  The bit variants, their `D<n>` tags and the admin **Drill bit** went; lances, the jet ring and the roll cage moved
  out round it. Drill levels need no set-back now (the bit is 42 cm), only the 7 degree tilt.
- The shovel is fixed at 75% of its first size and the drill at 90% (at 75% the drill looked thin again on screen;
  the admin **Tool size** went). Strokes: Pry is the default, then
  Scoop and lift, and a new Pry and scoop (pry on the blade's tip, then a slight scoop as the lever eases); Thrust,
  Chop and Scoop and toss went. The drill keeps its own push.

## Iteration: one shovel, a placeholder drill (2026-10-04)

- Verdict: the thick drill was rejected too ("leave a placeholder, I will take care of it later"), and the user had
  not wanted any per-level upgrades on the shovel ("keep one shovel visually for all levels"). The recipe now builds
  only a placeholder drill shown at levels 7-12 (a flange, motor, gear head, chuck and a plain two-flute bit); every
  bolt-on (sleeve, vibrators, batteries, guard, side handle, twin motors, rack, air tank, compressor, lances, jet
  ring, roll cage) and their unused materials (Steel, Plastic, Rubber, Red) went.
- Pry and scoop lifted too little: it now levers sooner and lifts the blade clear (3.2 cm and 13 degrees) before the
  return, over a longer stroke. Pry itself is unchanged.

## Iteration: pry and scoop only, dig on scoop (2026-10-04)

- Verdict: Pry and scoop is the stroke; Pry, Scoop and lift and the admin **Stroke** went (`ToolRigPresenter`
  keeps only `PryScoop` and the drill's push).
- Developer admin **Dig on scoop** (session-only, off by default): the press starts the stroke (`StrokesStarted`,
  which the rig now follows instead of `SuccessfulStrokes`) and the cut waits `ToolRigPresenter.ScoopDelay` (60% of the
  stroke, as the blade lifts) before `FpsPlayer.CompletePendingScoop` cuts where the player then aims. The next press
  waits for a pending cut; menus, suppressed input and resets cancel it. Drill levels cut on the press as before.

## Iteration: dig on scoop always, the purchased drill (2026-10-04)

- Verdict: dig on scoop is right and the toggle went: a shovel stroke always starts on the press and cuts at the
  scoop; drill levels still cut on the press.
- The user bought the Hand Mining Drill (CGTrader, Royalty Free no AI) to replace the placeholder; the Blender drill
  recipe (`art/tool-rig`), its model, materials and textures, and the shovel-shaft clip went. Configure Tool Rig
  copies the drill's four meshes into the rig (its +x along the tool, its axis on the rig's, the head's point at
  1.3 m) with the head under `L07-12_BitSpin`, and two URP materials on 2K copies of its maps
  (`art/hand-mining-drill/make_textures.py` packs metallic, occlusion and smoothness from roughness).
- In first person the drill at the shovel's framing showed only its head; drill levels now shift 0.3 model metres
  forward and tip 10 degrees, so the body shows in the lower right and the head points below the crosshair.
- The scoop's cut lifts the dirt along the press's aim, not wherever the player looks by then, and applies to the
  ground only (validation targets show no dirt and are struck on the press). `FpsTuning.CutAtScoop` (on) holds the
  timing; the pickup-cadence tests switch it off to test cadence alone, and TerrainIntegrationTests covers the scoop.

## Iteration: drill size, vendor cleanup (2026-10-04)

- The drill read too small at 90%. Developer admin **Drill size** (session-only, `FpsPlayer.AdminDrillSizes`:
  90, 100, 115, 130, 150, 175 and 200%) scales it about the socket; at every size its body stays in the lower right
  and its head below the crosshair. The first entry stays the default until playtest 001 names a size, then the
  winner becomes the default and the admin goes.
- Unity imported the drill's twelve 4K maps (about 340 MB) only so `make_textures.py` could read them: the ten
  it reads moved to `art/hand-mining-drill/source/` (LFS, outside Unity) and the two unused height maps went.
  The shovel pack's demo scene, prefab and Built-in materials, which nothing referenced, went too; the shovel
  model's material slot had pointed at the demo material (the validator stopped the build), so Configure Tool Rig
  now points it at `WesternShovel.mat`.
