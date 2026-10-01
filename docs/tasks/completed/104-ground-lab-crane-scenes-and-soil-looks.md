# 104 — Ground Lab Crane Scenes & Soil Look A/B

**Status:** complete. The development Ground Lab also holds six dug crane scenes around its bays (straight, narrow and rock-lined shafts, an open pit, a deep shaft and a bent tunnel), each with a computer ready to mark; delivered computers clear away from camp and **Restart Ground Lab** reloads straight back in. Developer admin's **Soil look** cycles the dug soil through the authored clay loam, the light clay loam, the brown mud and the user's own soil bakes for the session.

## Objective

User request (2026-10-01), after the salvage-crane effect A/B (`101`): "can u make me a testing ground
just for that somehow like i have testing ground with different soils?" and "create a couple of texture
variations i can test in the admin panel for the soil". Testing a recovery meant digging a hole and a
route to a computer every time; comparing the effects needs the same situations again and again.

## Concept reference

- `14` and the Ground Lab precedent (`100`): developer tools stay out of release builds, never save and
  never change the site or the player's game.
- `05` §5 / `09`: the crane's recovery (route, jams, breaks, debris, shaft dust) is what the scenes
  exercise; nothing here changes those rules.
- Asset rules (AGENTS.md): only approved content. The soil looks use the user's original Blender bakes
  (`art/ground-textures`) and layers of the approved BK Mountains/Highlands packs.

## Decisions

- **Part of the Ground Lab.** First built as a separate Crane Lab (own title button); the user then
  suggested moving the tests into the Ground Lab, which is better: one lab, the effects next to every
  ground, one title button. `GroundLab.Crane` (partial of `GroundLab`) defines the scenes in world space
  outside the bays (south: three shafts; north: pit and deep shaft; east: the bent tunnel), inside the
  plot outline and the crane's 2.5-27 m reach. Their ground is the lab's default soil (rock below 12 m).
- **Lab air:** `ExcavationGrid.LabCarve` (boxes for the thin-roof hollows, round-ended tubes for pits,
  shafts and tunnels), carved on every reset like a sealed room's air.
- **Seeded air needs chunks.** Untouched ground only owns surface chunks; carved air deeper than that
  had no mesh (a black hole at each shaft bottom). `TerrainVolume.MaterializeSeededAir` builds the
  chunks around rooms and lab air at start and after every reset (also used by the admin ground reset).
- **Computers:** each sits in a pocket 15 cm clear of its sides down to 0.2 m below its middle (65-73%
  exposed; marking needs 60%), so its base stays in the soil and the dug way decides the haul. The three
  catalog computers repeat across the six scenes (ids `ground-lab-NN`); the rock scene sets six common
  rocks half into its shaft wall. `DiscoveryField.UseGroundLab` replaces the (empty) lab population.
- **Repeatable:** a set-down computer clears from camp 5 s after it is stored, so the crane's three spots
  never fill; **Restart Ground Lab** (pause and lab menus) reloads MainGame and reopens the lab (a static
  flag the title consumes once it is ready). The aim prompt names a crane scene before a bay.
- **Soil looks:** `SoilLooks` asset (`Content/GroundTextures/SoilLooks.asset`, built by **Configure Soil
  Looks**) lists texture sets; `TerrainVolume.SetSoilLook` applies one to a session copy of the dig ground
  material (the x-ray copy follows), so the authored material is never edited. Looks: clay loam
  (authored), light clay loam (same mud, 1.35x tint), brown mud (Highlands mud, 0.76 tint), original
  soil (current bake, 0.8 tint), first soil bake (restored from git), and the earlier topsoil
  comparison's loam, dark loam and olive silt (the muted current bake, restored from git, with its old
  tints). Pack layers tile at 4 m like the clay loam. Restore normal rules returns to clay loam.

## Acceptance

- Title → Ground Lab: the bays plus six crane scenes, every computer markable; a haul runs, sets down,
  clears after 5 s. Restart reopens the lab with all computers back; Leave reloads MainGame. Nothing saves.
- Every soil look renders with its textures; cycling and x-ray work together; the scene asset only gains
  the `SoilLooks` reference.

## Iteration notes

- First look sheet: the brown mud was labelled "dark loam" but reads pale in a lit pit; the original
  bake and the sand blew out in a sunlit pit at full tint (now 0.8 and 0.6). The user then asked for
  the brown mud darker, twice (0.88, then 0.76).
- The user kept clay loam, light clay loam (favourite so far) and brown mud, dropped stony and sandy
  soil (pack rubble layers), and asked for their own custom ground textures: git history holds two soil
  bakes, a muted albedo and two grass turf bakes; the turfs are grass and were left out.
- The open pit was meant to break at the surface, but a straight lift meets no soil; it is the control
  scene ("a clean lift with nothing in the way").
- Two stepped recordings of the same scene never jam at the same moment (the crane's start depends on
  frame timing), so effect comparisons are recorded per run, not as identical frames.
