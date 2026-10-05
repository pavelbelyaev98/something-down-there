# 098 — Sealed Break-Through Rooms

**Status:** complete. The first concrete structure in zone 3 (sometimes a second) is a sealed room with ~1.9 m of dark air above a settled silt floor, carved into the seeded density; ordinary finds keep out and 2–5 finds sit half-sunk in the silt; the first tool cut through its cracked wall lets dust drift in.

## Objective

One or two small sealed rooms per zone inside concrete structures (a drowned-village cellar in the
old sediment, a waterworks tunnel section in the deep stone), never connected into passages. Dark
inside until the player's opening or a lamp lights them; the floor is settled silt with finds
half-sunk in it, so the reveal-by-silhouette survives. Every room stays closed until the player
breaks in. The ancient chamber comes with `039`. Feel: the wall gives, dust drifts into darkness,
the first lamp shows shapes in the silt.

## Concept reference

- `03` §5 sealed rooms: small and rare (one or two per zone to start: drowned-village cellar,
  waterworks tunnel section, later the ancient chamber); always sealed, the player always breaks
  in; dark until the player's opening or a lamp lights it; the floor is settled silt with finds
  half-sunk in it, nothing lying fully exposed; rooms never connect into passages or form a maze.
- `03` §1 "no caves or tunnel networks": the only pre-existing air is a few small sealed rooms
  inside buried structures. `03` §5 concrete structure: walls and floor, soil inside, "sometimes a
  sealed room"; cracks mark weak spots and in concrete run toward the rooms behind walls (`03` §4).
- `03` §7: sealed rooms are dark until the player's own opening or a lamp lights them; no daylight
  through solid ground. `03` §8 validation: every sealed room is closed until the player breaks in.
- `09` §4 breaking in: the wall gives, dust drifts into darkness, the first lamp reveals the silt
  floor with shapes half-sunk in it.
- `05` §1: sealed rooms hold finds half-sunk in their silt floor.

## Live codebase analysis

- `TerrainGround.Places` makes two open-topped concrete structures per zone 2 and 3 (walls and floor,
  soil inside). `PondClay` (006) is the soft grey-blue silt/pond clay.
- `ExcavationGrid.Reset` fills the analytic untouched density `min(band, depth)`; `AnyModified`
  compares against it, so any pre-existing air counts as modified (materialized on load; the find
  visibility test wakes nearby finds). `TerrainVolume.InitializeSession` builds only the top chunk
  layer; interior chunks appear where cuts reach.
- `ExcavationDaylight` lights only air connected to the open top, so sealed air stays dark.
- `DiscoveryField.Generate` supports reservations (spheres ordinary finds avoid); finds release
  only when nearly exposed with shallow contact, and exposure ≥ 60 % is needed to collect.

## Design

- **Rooms (`TerrainGround`)**: the first concrete structure in zone 2 and in zone 3 is sealed (a
  second one in each zone with 50 % chance): walls, floor and roof of concrete, a settled silt floor
  (pond clay, 0.45–0.6 m) and air above it. Crack sheets are denser in sealed structures, so the
  walls carry weak lines toward the room. `TerrainGround.Rooms` lists each room's air box and
  silt top.
- **Air from the seed (`ExcavationGrid`)**: a grid built from a seed carves each room's air box into
  its initial density (a smooth signed-distance box) in `Reset`, so rooms exist in a New Game,
  survive the admin reset, and save/load like any cut. `TerrainVolume` builds the room chunks at
  session start so the far walls and floor exist the moment the player breaks in.
- **Finds in the silt (`DiscoveryCatalog`)**: sealed structures are reserved from ordinary
  placement; each room gets 2–5 seats spread over its silt floor, each taken by the next find whose
  depth band covers it, centred just under the silt surface (half-sunk, below the 60 % pickup
  threshold, anchored). Counts and bands are unchanged.
- **Breaking in (`TerrainVolume`)**: when a tool cut first opens into a room's air, a slow dust
  puff drifts into the dark room and `BrokeIntoRoom` fires (audio `028`, particles polish `031`).
  Daylight and lamps then work through the opening as everywhere else.

## Edge cases

- Nothing may carve room air at generation except the room itself; channels, pours, cracks and
  places never open it (channels never replace places; pours remove gravel only; cracks never
  change density).
- A room never touches another place, the surface or the grid edge.
- Loading an old game is not supported (new format data); New Game carves rooms.
- Admin reset restores closed rooms.

## Acceptance criteria

1. Shipped seed and test seeds: one or two rooms in zones 2 and 3, inside their structure's walls
   (≥ 0.4 m shell everywhere), the carved air of each room is enclosed (flood fill stays inside).
2. Room floors are silt with 2–5 finds seated half-sunk: centres just below the silt top, within
   the room, apart; no ordinary find intersects a sealed structure.
3. The room is dark before the break-in and dust drifts into it when the wall gives.
4. Screenshots: breaking in, and the lamp-lit silt floor with shapes half-sunk.
5. Tests pass; build refreshed.

## Results (2026-09-27)

- Shipped seed: zone-2 room at 63 m (5 seats) and zone-3 rooms; every tested seed has one or two per
  zone, closed on all sides (flood fill from the room's air stays inside its structure on a seeded
  grid; admin reset closes them again).
- The first tests found a waterworks room only 1.07 m tall (the zone-3 structures are low): sealed
  structures now grow to keep ~1.9 m of air above the silt.
- Play Mode check (`Logs/098-sheet.png`): from a pocket beside the wall two scoops broke through a
  cracked wall into darkness; a lamp inside shows the concrete cell, crack lines and the grey-blue
  silt with finds half-sunk in it. Dust puffs are subtle; polish is `031`, sound `028`.
- The first full run found the room's air samples passing through the crack field (walls came out
  mostly crack) — only a place's solid shell cracks now, and the sealed-wall crack bias is .35.
  Discovery tests that assumed every generated find starts fully buried now exempt the room seats
  and check them instead: partly exposed, not collectible, anchored. The fresh scene materializes the
  top layer plus exactly the rooms' chunks.
- The room seat takes the next find whose band covers its depth, so the type is whatever the depth
  holds (ore today; village and waterworks items with `012`/`011`).

## Iteration (user, 2026-10-05)

- The drowned village was dropped from the design, so zone 2 holds no concrete structures and the
  sealed rooms sit only in the zone-3 waterworks.
