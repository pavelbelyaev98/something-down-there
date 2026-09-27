# 001 — Evolving Motorized Tool Rig

**Status:** complete. The tool is an original Blender kit whose parts are named by level range; `ToolRigPresenter` shows it rising from the bottom edge right of centre (head and the bolt-ons clustered behind it: shovel, bolt-on machine, drill at level 7, nozzle cannon at 10), turns only the tool about its socket per material family and lowers it away for menus, held finds, placements and recovery marking.

## Objective

Show the one machine the player digs with, lower right, no hands: an ordinary shovel that grows
bolt-on parts with every tool purchase, becomes a drill at level 7 and ends as a welded,
over-batteried cannon of a shovel. Its motion shows what the automatic material response is doing
(which head is biting) and how much stronger each purchase made it, without ever blocking digging,
pickup, aiming or the view, and a purchase changes it instantly.

## Concept reference

- `04` §1: exactly one tool, never switched; upgrades bolt onto the same object. Tool only, no
  hands. Visual escalation: motors, battery packs, wider heads, pipes, reinforcement, a late nozzle,
  welded plates, cables; the silhouette grows ridiculous while staying recognisably the same
  machine. Levels 1–6 are shovel scoops, level 7 adds continuous drill cutting (kept to 10).
- `04` §2: hold-to-dig (toggle available); the rig must communicate the shovel-to-drill change;
  Developer admin keeps its session-only motion comparison.
- `04` §3: no mode button; the machine reads the ground: a fast precise bite, a wide cheap scoop and
  eventually a blast head; the player sees and hears which head is active; every behaviour digs
  everything; the final head keeps distinct responses. Following a tell is just digging.
- `04` §4 / `06` §2: one Tool track, ten levels; every purchase noticeably improves the next outing
  and applies without a blocking animation. `04` §10: not a weapon, not a light source, never
  disabled by the story; the late nozzle is an attachment ("a shovel that has clearly become a
  cannon").
- `09` §1/§3/§4: custom Blender models share one style guide with the licensed packs; no
  AI-generated images or textures. Deadpan absurdity: a welded, bolted, over-batteried monster; no
  cartoon eyes. **The camera never shakes or jerks from digging**; power is felt in the hands; tell
  feedback is felt first (crack: bites faster and crisper; backfill: sinks in). Earlier first-person
  tool experiments were removed only because modelling was postponed, not for a design reason.
- `10` §1: no digging shake; head bob off by default; motion comfort is a release gate.
- Research: Meltopia players praised visible bolt-on changes and asked for no upgrade animation
  ("keep the visible change, cut the wait"); One Man's Trash players disliked a tool that never
  evolves; A Game About Digging a Hole's drill "feels so good after making a mess with the shovel".

## Live codebase analysis

- No tool mesh, viewmodel layer or rig code exists; dig feedback is only the crosshair pulse
  (`FpsPlayer.DigPulse`, off with the default steady crosshair).
- Rig inputs already exposed on `FpsPlayer`: `EffectiveShovelLevel`, `ShavingEnabled` (drill motion,
  honours the admin Motion override), `LastDigMaterial`, `LastDigInterval`, `DigPulse`,
  `SuccessfulStrokes`, `LastScoopVolume`, `CrouchAmount`, `Pitch`, `IsJetpackActive`,
  `GameplayActive`, `HeldFind`; `TerrainVolume.ToolCut(TerrainCutFeedback)` fires per committed cut
  (material, point, normal, volume). Upgrades raise no event (poll the level; purchases happen with
  time stopped in the station menu, so the new rig is simply there when the menu closes).
- `EquipmentProgression.MaterialResponse` gives each ground a width/length/penetration/interval
  response; these group into three readable families: **wide scoop** (soil, gravel, backfill),
  **precise bite** (clay, pond clay) and **hard head** (rock, cracks, concrete).
- Camera: FOV is a player setting (55–90), near clip 0.1 but lowered to 0.005 while crouched in
  tight spaces; URP with two renderers (default + NoContactShading), SSAO feature, no camera stack,
  no viewmodel layer. Held finds sit lower right (`FindHandling`), exactly where a tool would.
- Art precedent: `art/salvage-winch`, `art/work-lamps` (Blender MCP originals, `create_assets.py` +
  `.blend` recipes, README cards, project materials under `Assets/Content`).

## Proposed architecture

- **Art (Blender MCP, original):** one modular kit in `art/tool-rig/` (`create_assets.py`,
  `tool-rig.blend`, README card): a base shovel plus bolt-on parts per level, one shared trim
  material, exported as a prefab with named part groups under `Assets/Content/ToolRig`.
  Escalation map (to confirm):

  | Level | Adds |
  |---|---|
  | 1 | Plain steel shovel, wooden shaft |
  | 2 | Taped grip, reinforced blade edge |
  | 3 | Wider blade, bolted side plates |
  | 4 | Small motor and belt on the shaft (vibrates while digging) |
  | 5 | Strapped-on battery pack and cable |
  | 6 | Second battery, pipe frame, wider head |
  | 7 | **Drill head** replaces the blade tip: continuous spin while shaving |
  | 8 | Twin motors, welded plates |
  | 9 | Bigger head, cable bundle, extra batteries |
  | 10 | Late **nozzle** (blast head) on top: the shovel has become a cannon |

- **Rendering:** the rig is small and close (about 0.3 m to the tip), inside the player's 0.3 m
  capsule, so it can never poke through a wall and needs no render-pipeline change; its width and
  height scale with the FOV so it keeps its screen place (perspective-exact). It casts no shadow and
  is lit like the world, so it is dark in the dark. *Considered:* a URP Render Objects overlay layer
  (extra pipeline passes on both renderers and depth tricks for self-occlusion) and a plain
  full-size camera child (clips into shaft walls).
- **Presentation (`ToolRigPresenter`, Runtime/Player):** reads `FpsPlayer` each frame; swaps part
  groups on level change (instant); drives procedural motion (no Animator): per-stroke scoop arc
  timed by `LastDigInterval` for levels 1–6, continuous spin and small chatter for the drill,
  motion family from `LastDigMaterial` (wide sweep / short fast bites / hammering head), a quicker,
  crisper stroke on cracks and a deeper sink into backfill. Only the tool moves; the camera never
  does.
- **Never in the way:** stays in the lower-right quarter, below the reticle; lowers out of view while
  a find is held, during pickup presentation, worksite placement previews, extraction marking,
  menus and rescue; hidden in photo mode later (`036`).
- **Admin:** the existing Motion override drives drill versus scoop visuals, so the comparison
  shows what it compares.

## Edge cases

- Crouch near-clip change and FOV 55–90 must not distort or clip the rig.
- Toggle-dig and held-dig look identical; collection never pauses the rig's motion (`09` §4).
- Restore/loading shows the saved level's rig on the first frame; no pop-in animation.
- Deep darkness: the rig is not a light source and must not glow; lamps light it.
- No detector attachment while the detector is frozen.

## Acceptance criteria

1. Ten visibly distinct stages of one machine; a purchase changes it before the next stroke.
2. Levels 1–6 scoop per stroke, levels 7–10 spin continuously; the three material families read
   differently in motion; cracks and backfill feel different on contact.
3. The rig never clips into walls, never covers the reticle, lowers for held finds/placements/menus,
   and never moves the camera.
4. Frame cost negligible (static meshes, seven shared materials, no shadows); screenshots per stage.
5. Art card and Blender recipe in `art/tool-rig/`; tests only for the level→stage mapping.

## Decisions

1. **Art route:** original modular kit built through Blender MCP (like the salvage winch and work
   lamps); no licensed pack evolves one tool.
2. **Rendering:** small rig inside the player's capsule (see above), not an overlay pass.
3. **Escalation map:** as in the table above; adjust after the first in-game look if a stage reads
   poorly.

## Results

- Blender kit: 56 parts; export bakes the axis conversion (identity part transforms; the first
  export came in mirrored along the tool and was fixed at the source). Stage sheet
  `Logs/001-stages.png`: ten distinct stages; the drill bit appears at 7, the cannon at 10.
- Pose: lower right, pitched into the ground with the blade face turned to the view (four poses
  compared in `Logs/001-poses.png`; the flatter ones read as a lance with the blade edge-on).
- Strokes move along the tool axis (toward the ground, never into a wall ahead). Drill spin is
  capped at 70° per frame so the two-flute bit never appears to turn backwards.

## Iteration (playtest 2026-09-27)

- The user found the first pose wrong: the shaft entered from the right edge and its right part sat
  too high. Like A Game About Digging a Hole, the tool now rises from the bottom-right corner and
  takes little space: only the head shows. The bolt-ons (motor, batteries, pipes, welds, cables,
  nozzle) moved up the shaft next to the head so each stage still reads (`Logs/001-poseE.png`).
- Strokes now turn the tool about its socket (a lever from the far grip swung the head too far).
- Rejected: the diagonal lance pose across the lower half (`Logs/001-stages.png`), and a roll that
  turned the blade edge-on (`Logs/001-poseD2.png`).
- Second playtest: still too large and too busy. The rig is about three quarters of its previous
  size (only the head shows in the corner) and every stroke, shudder and drill chatter moves about
  half as far (`Logs/001-small.png`).
- Third playtest: a bit more visible (scale 0.30) and a small thrust along the tool instead of a
  dig swing.
- Fourth playtest: moved a few centimetres toward the centre so it rises from the bottom edge, not
  the corner (`Logs/rig-x.png`, bottom left).
