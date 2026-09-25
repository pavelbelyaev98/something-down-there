# 03 — World and Site

## 1. The site

The lakebed excavation is a single contained worksite.

- **Setting:** the river canyon of the approved Pure Nature 2: Highlands demo, kept close to the
  demo's terrain and asset placement. The river is widened into a former lake that still holds
  water along the canyon, with a waterfall at its southern end; a bathtub band marks the old shoreline.
- **Drained section:** one large exposed section of the lakebed against the east cliff, sloping
  gently into the remaining water. It reads as a recently drained bed, not a flat yard: packed
  sediment on the flats broken by darker mud patches, damp silt toward the water, pebble strands
  along old waterlines and sandy banks. Shallow, walkable channels still trickle from seeps at the
  cliff foot, wind around the dig plot and run into the lake, with stony beds, damp banks, reeds,
  rushes and tufts of muted, sun-dried grass (never bright lime); one dry gully joins them. Stranded stones, rubble and twigs lie
  along channels and waterlines; boulders on the old bed are bare, water-worn rock. Low sand islands with grassy tops and the odd boulder stand in
  the remaining lake. All lakebed dressing stays clear of the camp and the dig plot.
- **Dig area:** one wide, irregular plot of bare packed sediment inside the drained section, wider
  east-west than north-south and never a circle, with no fence. Its paler ground stands out
  within a band of darker trampled mud. No grass grows on the plot. The stations stand on its south side, looking north up the canyon.
- **Play area:** the player stays on the drained section: invisible walls follow its edge at the
  water line and a flight ceiling stops the jetpack about 16 m above the ground. Scenery that can
  never be seen from inside this volume is left out.
- **Dimensions:** `SiteLayout` owns the opening, rim and subsurface allocation. Depth remains
  at least 100 m; the footprint leaves useful lateral room for branches. Finds keep their
  accepted density beneath the plot and just under its edge; ground further out is plain soil.
- **Boundary:** the rim and lakebed ground cannot be dug from above. The saved rectangular
  subsurface grid remains accessible beneath them for lateral digging.
- **Lighting:** solar-noon presentation with the sun almost directly overhead and short shadows.
  Use the Highlands demo's sky, ambient colour, sun colour and baked canyon reflections; custom
  sun/cloud artwork is retired. The sky's sun reads as a clear disc with a soft glow, not the
  demo's pinpoint.
  Concentrate soft real-time shadows around the playable ground; distant scenery may omit them.
  Treat the surrounding canyon as a backdrop with earlier detail reduction and view-dependent
  occlusion, preserving its silhouette from the ground and flight ceiling. Detail reduction is
  for far scenery only: rocks, trees and bushes keep full detail near the player, the coarsest
  meshes and flat tree impostors stay on the distant backdrop, and switches blend. Foliage reads
  as solid canopies, without speckled dither patterns.
- **Water presentation:** calm, silty slate grey-green lake water, murky rather than blue: the
  bottom shows only in the shallows, stream water fills each channel to a natural waterline and
  runs into the lake without a seam, bright banks never smear across the surface, with restrained
  shore foam and rippled canyon reflections. Neither refraction nor reflected light may turn
  the shore into a glowing white band; the lake remains scenery outside the play area.
- **Underground:** fully diggable voxel ground except permanent boundaries.
- **No pre-existing caves or tunnels:** every opening in the ground is one the player made.

- **Buried structures:** authored walls, machinery and filled interiors are allowed. The
  player digs every opening; no pre-dug rooms or passage network.
- **Buried history & physical connections ("Follow the thing"):** Workshop, household and waterworks
  finds belong together. To give lateral digging an immediate visible reason, objects can physically continue
  through the ground: a heavy cable trailing from a broken generator, a rusted chain disappearing under a slab,
  or exposed pipes heading toward unseen machinery.
  - _The core rule:_ The detector suggests that something exists; the exposed world suggests what to do next.
  - No wiring puzzles, inventories, or repair chores; following a connection means digging.
  - Authored buried arrangements preserve internal relationships and randomize as coherent units.
- **Major connected parts:** Connected finds suggest a buried history. In the current direction,
  they are components and fragments of an ancient structure/mechanism that gradually becomes clear.

## 2. Boundaries (why you cannot dig forever)

Permanent boundaries must look categorically different from any diggable material:

- **Sides:** concrete retaining walls, dam infrastructure, steel pilings — industrial, cracked,
  obviously not soil.
- **Bottom:** solid bedrock shelf.
- **Surface edge:** the drained lakebed, canyon cliffs and remaining lake frame the dig area.
  No swimming or flooding: the play area ends at the water line, the lake is never a hazard, and water
  never enters the excavation.

Rule: never use the same material look for "tough but diggable" and "eternal wall". Players must
know at a glance what will eventually yield. If the buried-structure direction is used, it is never
a boundary: its built surfaces stay clearly different from bedrock and concrete walls, and routes
around it stay open.

## 3. The four zones

Each zone changes ground, palette, typical finds and mood. Transitions are gradual; there are no
loading screens or separate levels.

| #   | Zone                    | Ground                                          | Finds typical                                         | Mood                             |
| --- | ----------------------- | ----------------------------------------------- | ----------------------------------------------------- | -------------------------------- |
| 1   | **Recent fill**         | Loose soil, gravel, roots, modern rubbish       | Bottles, scrap, household junk, common ore            | Bright, familiar, hopeful        |
| 2   | **Old sediment**        | Compacted river sediment, clay lenses           | Old tools, machinery parts, first fossils, better ore | Nostalgic, slightly odd          |
| 3   | **Deep clay / stone**   | Hard clay, rock, occasional concrete            | Larger machines, rare ore, deliberate objects         | Heavy, dim, purposeful           |
| 4   | **Ancient constructed** | Unknown compacted material, ancient fabrication | Impossibilities, final components, the final object   | Cold, quiet, wrong in a good way |

Zone names are placeholders; final naming is content work.

## 4. Materials

Working set (exact list TBD): prototype five response groups — loose earth, clay/sediment,
gravel, rock and diggable concrete. Soil, sand and harder variants can look different within these
groups. Each family differs in **behavior**, not just color:

- sand pours and spills quickly;
- clay sticks and clumps;
- gravel trickles;
- compact sediment resists evenly;
- rock chips and cracks;
- concrete sparks and resists, but the starting tool always makes visible progress.

The first playable material pass uses broad rounded soil cuts, narrower smooth clay shavings,
and smaller faceted rock chips. Resistance belongs to the ground inside the cut, including mixed
seams; aiming at a soft patch does not turn its hard neighbor into soft soil. Deposits keep their
identity when excavated and saved. Distinct soil grain, compacted clay and fractured rock textures
follow these deposits across cut faces, with narrow blended boundaries. Debris reinforces the response as it is added.

The tool adapts automatically to the material (see [Tool and Movement](04_TOOL_AND_MOVEMENT.md)); materials reward the
right behavior but never lock it out. These are cutting responses and visual debris, not a global
collapse hazard. Power growth outpaces tougher ground over the campaign.

**Dig along the seam (Signature Action)**: some ground has visible cracks or material boundaries.
Cutting broadly along one frees a larger local section with less work than digging through its center.
For example, follow a clay seam around a rock section and break that section away. The ground offers
a small spatial choice: "where would a cut do the most?"

- Use the same tool and normal digging input; broad, readable cuts along a seam reward the player
  without requiring a pixel-perfect or fully traced perimeter.
- **Physical payoff:** cutting along a seam triggers distinct feedback — a sharp stress crack, a subtle
  physical shift of the worked slab, and a heavy fracturing break as the section gives way,
  frequently exposing multiple buried objects at once.
- **Bounded fracture regions:** fractures operate within bounded, predictable local zones rather than a
  general structural collapse simulation. Seams never crush the player, bury collected objects, or close
  return routes.
- Digging straight through always works. Seams offer an optional efficiency gain from the start;
  stronger upgrades make the resulting cuts larger and more satisfying.
- **Normal cleanup rules apply:** plain dirt crumbs vanish, embedded valuables remain in place without
  bonus duplicates, and interesting finds survive intact for deliberate partial exposure and recognition.
  Breaking a slab never creates extra loot or bypasses recognition.
- Paper-thin soil fins and ribbons crumble as they are carved, even when long or attached at both
  ends. Remove their collision with their visible geometry; thicker useful ledges remain stable.
- Removal stays local to the worked section. Unrelated ledges, tunnels, and overhangs remain stable;
  this does not add a collapse hazard.

## 5. Tough ground: hard pockets

A small number of memorable, optional obstacles (5–8 target) — never walls across the main descent:

| Example                | Feel                                       | Behind it                            |
| ---------------------- | ------------------------------------------ | ------------------------------------ |
| Concrete plug          | Slow but visible progress with early tools | A waterworks alcove with a rare part |
| River-rock lens        | Dense boulder cluster                      | A complete fossil                    |
| Compacted gravel shelf | Slows digging for a while                  | An older, richer pocket of finds     |

Every pocket has **multiple solutions**: the current tool, C4, or routing. Upgrades make excavation
much faster. Discovering one early and demolishing it later is a designed moment of power.

Solutions must be **legible before commitment**: the player can see that a pocket has an answer before
sinking time into it — distinct seams, cracks or fittings that read as C4-friendly, a material
clearly unlike the eternal boundaries, and a tool that visibly chips even the tough ground. "Come back
with more power" is an optional shortcut, never the only answer. An undiscoverable solution is the same
as no solution.

## 6. Terrain technology and cleanup

- **Full voxel**: every diggable cube can be removed; tunnels, overhangs and trenches are legal. Chunk size prototype-tuned.
- **No floating specks**: disconnected valuables become visible pickups and collect if the bag has
  space; plain dirt crumbs vanish. Full-bag overflow persists nearby without blocking movement.
- **Interesting finds survive cleanup:** terrain removal and C4 never delete them or bypass deliberate
  collection.
- **Debris is visual only**: particles never collide and never deal damage.
- **Collision always matches the visible mesh.**
- **Substantial structures survive**: ledges, tunnels and overhangs the player built are preserved;
  only unsupported crumbs are cleaned.
- **Progress never resets**: the terrain edit history is saved; loading restores exactly the hole.

## 7. Lighting, marking and navigation

- **The "No Map Ever" pillar:** No minimap, compass, or GPS radar, ever.
- **Inherently vertical navigation (why this differs from _Meltopia_):** In _Meltopia_, players suffered navigation fatigue because the world was a sprawling, flat maze of identical horizontal tunnels. _Something Down There_ is fundamentally different: **it is vertical**. Near the surface, looking up reveals the open sky and the shaft of daylight; deeper down, the lamps you leave behind become the way home. The player's own carved shaft always points straight up.
- **Optional world markings:** For complex lateral branches off the main vertical shaft, the tool can apply simple, free, reusable chalk/spray symbols (arrow, home, return-here) readable by shape.
- **Early route lighting:** Basic placeable lamps are available at the first major branch to illuminate lateral chambers and photography spots.
- The starter kit is available immediately. Preview placement on exposed ground or permanent
  any solid worksite surface, including rough ground, walls, ceilings and props, then retrieve
  the lamp to place it again. A lamp released into open space falls and remains recoverable. Lamp ownership is
  separate from the find bag. The preview emits no light; only a placed lamp illuminates the work.
- Markings conform to the worked surface, can be rotated and erased for free, and save with the
  hole. Excavating their painted surface removes them. Lamps release physically when their
  support disappears, remain lit nearby, and retain their kit ownership. Sustained contact from a
  winched load can knock a mounted lamp loose; portable lighting never permanently blocks recovery.
- **Sky light reaches down open shafts** and fades with depth.
- **A generous daylight reach:** shallow and middle-depth excavation stay comfortably readable,
  including short covered branches. Darkness develops gradually along substantially deeper or
  longer routes; the first underground work area must not immediately need a lamp.
- **Sideways travel loses daylight faster.** Brightness follows the open route from
  the surface, so a long covered branch can become near-black even at shallow depth.
  There is no permanent ambient fill keeping soil or finds visible in unlit ground.
- **The shaft reads from below:** its light column and drifting dust are landmarks where the shaft
  is visible. Light does not pass through overhangs; the jetpack and reusable lamps support returns.
- **True darkness.** Below the reach of sky light, covered tunnels are near-black: material color,
  seams and find silhouettes stay unreadable until light reaches them. Digging, movement and the
  detector still work in the dark — light withholds information, not ability.
- **Placeable lamps are the light.** They are the only light underground: place them to work, reveal
  finds and hold the route home. Owned lamps are reusable, repositionable and do not expire or drain
  charge; lost support leaves them recoverable nearby. Digging and C4 cannot destroy them.
- **Diffuse all-around lamps:** a neutral lantern illuminates every direction. Nearby soil retains
  texture instead of becoming an orange-white hotspot; solid ground still blocks the light.
  One lamp lights a useful stretch of tunnel or chamber with a broad, gradual falloff, not just a small pool beside its housing.
- **No personal light.** The tool does not act as a headlamp; that is what makes lamps the way you
  see and the way you remember the hole.
- **Zone lighting moods:** warm daylight near the surface → shade in covered shallow ground → true
  darkness in the deep zones, broken only by lamps.

## 8. Randomization rules

- Authored: zone layout, depth ranges, boundary placement, general difficulty curve and relationships
  between buried places and the connected major finds.
- Randomized per save: find positions, depths within bands, rotations, cluster layouts, some
  surrounding junk. Variation preserves how related objects and major parts fit together.
- The generator produces a candidate layout and validates [discovery pacing](02_CORE_LOOP.md#4-pacing-rules-generation-enforces-these)
  before accepting it.
- Every seed contains all special exhibits, ending parts and achievement-relevant finds, reachable
  and discoverable with baseline equipment.
- The accepted population is finite and persisted; patches never reroll an existing save.

## 9. No hazards

No lava, gas, oxygen, hunger, earthquakes, temperature damage, or monsters. The only pressure
is the shared battery, the bag's capacity, and the player's own greed — all soft, all fair, all
recoverable (see [Progression and Economy](06_PROGRESSION_AND_ECONOMY.md)).
