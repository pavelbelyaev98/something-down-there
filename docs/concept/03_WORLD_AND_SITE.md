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
  the remaining lake. All lakebed dressing stays clear of the camp and the dig plot. A few metres
  beyond the plot's tape, patches of the canyon's green grass ring it (fewer on the camp's side),
  so the worked ground sits in a little green.
- **Dig area:** one wide, irregular plot of bare ground inside the drained section, wider
  east-west than north-south and never a circle, with no tall fence. It reads as a construction or
  excavation site: one red-and-white barrier tape tied round heavy timber posts, each cast into a square
  concrete footing, on the permanent ground outlines it. It should feel solid, a built structure rather than
  flimsy sticks pushed into the ground,
  so where digging starts and stops reads at a glance. Its edge bumps in and out every few metres, so
  cuts along it never follow one clean curve, and no step or line marks the edge before digging. The
  whole plot is evenly a light mud, only a touch darker than the ground around it, never a deep dark;
  the lightening starts on the permanent ground just beyond the edge, through a damp band with no
  visible texture line, and reaches the lighter lakebed within about ten metres. No grass grows on the plot; cuts expose lighter clay loam coloured apart
  from the cracked surface mud, with no stone shapes that could pass for finds. Where a hole meets the
  untouched ground its mouth is softly rounded, about a hand's width, the light top fading into the dug soil
  across the round: never a straight vertical cut at the surface. Up close the top reads as dry soil with
  grain, not a blurry smear. The canyon beyond the
  old lakebed keeps the demo's ground, with one grass everywhere: a muted, warm olive-yellow turf in the same colour as
  the grass blades growing on it, so ground and blades read as one from every angle and sit close to
  the warm mud (a muddy vibe, never a vivid or cool green). Every plant around the site shares that
  one style: grass, ferns, bushes and the grass painted on rock tops wear the same muted olive, nothing
  stands much above hip height, and all of it sways gently together in the wind; nothing is frozen
  beside something waving. The stations stand on its south side, looking north up
  the canyon.
- **Play area:** the player stays on the drained section: invisible walls follow its edge at the
  water line and a flight ceiling stops the jetpack about 16 m above the ground. Scenery that can
  never be seen from inside this volume is left out.
- **Dimensions:** `SiteLayout` owns the opening, rim and subsurface allocation. The site is
  **150 m deep**, giving each of the four zones enough depth for its main ground to be learned
  (§3); 200 m is the next step only if playtests say the shaft still feels short. The footprint
  keeps its current width: it fits a few ground places per zone and keeps sideways routes short
  enough that nobody gets lost. The research names two risks of depth: a longer trip home (Keep
  Digging 2.0's 5,000 m map was called a slog) and visible save stutter (Meltopia's save freezes
  cost it trust). Jetpack progression and background saving must keep both invisible at full
  depth, and the extra depth must be filled with finds, never "big but empty" (One Man's Trash). Finds keep their
  accepted density beneath the plot, wholly inside its edge; ground further out is plain soil.
- **Boundary:** the rim and lakebed ground cannot be dug from above. Under them, the top 1.2 m is
  a permanent soil bank, so a pit edge is a solid soil wall, never a thin roof over a hollow. The
  saved rectangular subsurface grid remains accessible below the bank for lateral digging.
- **Lighting:** solar-noon presentation with the sun almost directly overhead and short shadows.
  Use the Highlands demo's sky, sun colour and baked canyon reflections, with daylight fill that
  comes from the sky above and from the sunlit ground at the sides and below, as real daylight does,
  so walls facing sideways (a pit's walls, cliffs) are lit even under the high sun; custom
  sun/cloud artwork is retired. The sky's sun reads as a clear disc with a soft glow, not the
  demo's pinpoint.
  Concentrate soft real-time shadows around the playable ground; distant scenery may omit them.
  Treat the surrounding canyon as a backdrop with earlier detail reduction and view-dependent
  occlusion, preserving its silhouette from the ground and flight ceiling. Detail reduction is
  for far scenery only: rocks, trees and bushes keep full detail near the player, the coarsest
  meshes and flat tree impostors stay on the distant backdrop, and switches blend. Foliage reads
  as solid canopies, without speckled dither patterns. Grass, props and scenery in and around the
  play area never disappear from anywhere the player can stand or fly.
- **Water presentation:** calm, silty slate grey-green lake water, murky rather than blue: the
  bottom shows only in the shallows, stream water fills each channel to a natural waterline and
  runs into the lake without a seam, bright banks never smear across the surface, with restrained
  shore foam and rippled canyon reflections. Neither refraction nor reflected light may turn
  the shore into a glowing white band; the lake remains scenery outside the play area.
- **Underground:** fully diggable voxel ground except permanent boundaries.
- **No caves or tunnel networks:** every passage is one the player dug. The only pre-existing
  air is a few small [sealed rooms](#sealed-rooms) inside buried structures, and the player always
  breaks into them.
- **Buried structures:** authored walls, machinery and filled interiors are allowed. The
  player digs every opening; no pre-dug passage network.
- **Buried history & physical connections ("Follow the thing"):** Workshop, household and waterworks
  finds belong together. To give lateral digging an immediate visible reason, objects can physically continue
  through the ground: a heavy cable trailing from a broken generator, a rusted chain disappearing under a slab,
  or exposed pipes heading toward unseen machinery.
  - _The core rule:_ The ground hints that something is near (§4 tells); the exposed world suggests what to do next.
  - Trails pay along the way: small finds sit along a cable or pipe, so following it is never a blind gamble.
  - Each zone has its own kind of trail: chains and cables in recent fill, pipes and scattered
    machine parts in old sediment, cracks and mineral veins in deep stone, matching grooves and
    fittings in ancient ground.
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
around it stay open. The same applies to rock-zone ground and buried concrete structures (§3, §5):
they must never read as the bedrock shelf or the retaining walls.

## 3. The four zones

Each zone has **one main ground** that fills most of it, plus its own places, tells, finds and mood.
Transitions are gradual; there are no loading screens or separate levels. Depth splits are roughly
even quarters of the site (the ancient zone may be shorter) and are tuned in generation code. Zone
names are placeholders; final naming is content work.

| #   | Zone                    | Main ground                          | Places and purposeful mixed spots                                                                                              | Typical finds                                        | Mood                             |
| --- | ----------------------- | ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------- | -------------------------------- |
| 1   | **Recent fill**         | Soil, with gravel lenses             | Buried stashes and rubbish pits (disturbed ground), the first gravel channels, stray concrete rubble                            | Bottles, scrap, household junk, coins, coal, copper  | Bright, familiar, hopeful        |
| 2   | **Old sediment**        | Clay                                 | Clay basins, winding gravel channels (old riverbeds)                                                                           | Old tools, machine parts, bones and fossils, better ore | Nostalgic, slightly odd       |
| 3   | **Deep clay / stone**   | Rock, veined with clay               | Rock masses with crack networks and ore veins, soft clay/gravel paths through the rock, waterworks concrete, deep sealed rooms | Larger machines, rare ore, deliberate objects        | Heavy, dim, purposeful           |
| 4   | **Ancient constructed** | Its own ancient material (defined with the zone-4 work) | Constructed architecture, an ancient sealed chamber                                                         | Impossibilities, final components, the final object  | Cold, quiet, wrong in a good way |

### Why one main ground per zone

The first generator repeated one thin 8 m stack (soil, a gravel band, clay, rock) all the way down.
No material lasted long enough to learn, so materials felt like nothing more than "slower here,
faster there". A zone that is mostly one ground lets the player learn how it digs, what it hides
and what its tell looks like, and it makes arriving in the next zone an event: "it's rock from here
on; time to read the cracks and bring C4". Keep Digging reviewers single out layers that "each
have their own surprises"; this is the same pull.

### Zone rules

- **No zone feels like a restart.** At the tool level a player typically owns on arrival, a zone's
  main ground digs no slower than the previous zone felt near its end. The drill milestone (tool
  level seven) is timed to arrive around the rock zone, and cracks give a fast way through it. This
  guards against Meltopia's #1 complaint: a new material ("blue snow") that suddenly made digging
  slow and made players feel their upgrades were wasted.
- **Never a wall.** Every hard zone and hard place has soft paths (clay or gravel veins winding
  through the rock), so the player can follow a soft path, grind straight through, or use C4. Depth
  is never gated by tool level; One Man's Trash's tier walls read as walls and forced grinding.
- **Variety inside a zone.** One main ground must not become one repeated wall. Colour bands,
  cracks, places and tells act as landmarks. No glaring pale surfaces: a white rock layer strained
  Keep Digging players' eyes, and Meltopia's identical tunnels got players lost.
- **Edge bands.** Where two zones meet, a short mixed band tells the player they are entering new
  ground.
- **Mixed spots only with a job.** Mixing exists where it does something: a gravel channel to
  follow, a soft path through rock, an odd spot around a unique (§4), an edge band. Random mixing
  elsewhere is noise and is not generated.

## 4. Materials and their tells

Six ground types, each different in **how it digs, what it hides and how it points somewhere**.
Hardness shows mostly as **bite size** and only a little as rhythm: harder ground takes smaller,
shallower bites at a slightly slower stroke (softer ground and tells take bigger ones). Strokes throw
no particles (user, 2026-09-30, kept for the drill 2026-10-05); the ground's own look and the cut's shape carry it. A player who hits
rock sees the bites shrink instead of feeling the machine stall.

| Ground   | How it digs                                    | Its tell                               | What it tends to hold                               |
| -------- | ---------------------------------------------- | -------------------------------------- | --------------------------------------------------- |
| Soil     | Fast, broad rounded cuts; plain ground that never collapses | Disturbed ground                       | Rubbish, junk, plain rocks                          |
| Gravel   | Loose grainy cuts; trickles; pours when undercut | Channels: winding old riverbeds      | Heavy things: coins, tokens, metal, nuggets         |
| Clay     | Steady, narrow smooth shavings                 | Disturbed ground; basins               | Bones, wood, leather, fossils, organic things       |
| Rock     | Small faceted chips                            | Cracks and veins                       | Ore                                                 |
| Concrete | Smallest square chips and sparks, but the starting tool always makes visible progress | Cracks from old damage | Waterworks items; rooms behind it |
| Backfill | Loose and mixed; digs fast; slumps when undercut | It *is* the tell                       | Whatever someone buried                             |

The ancient zone adds its own material with its contact signature
([Ending and Mystery](11_ENDING_AND_MYSTERY.md#1-the-mystery-trail)).

Resistance belongs to the ground inside the cut, including mixed seams; aiming at a soft patch does
not turn its hard neighbor into soft soil. Deposits keep their identity when excavated and saved.
Distinct soil grain, compacted clay, loose gravel, fractured rock and pale concrete textures follow
these deposits across cut faces, with narrow blended boundaries. Debris reinforces the response as
it is added. The tool adapts automatically to the material (see
[Tool and Movement](04_TOOL_AND_MOVEMENT.md)); materials reward the right behavior but never lock
it out. Power growth outpaces tougher ground over the campaign.

### The one rule: easier ground means you are on to something

Every ground type has a tell, and every tell works the same way: **the ground suddenly gets easier
to dig, and following it leads somewhere.** This turns the old weakness ("materials are just
different dig speeds") into the clue system. The tells are also the intended replacement for the HUD
detector, which is frozen until a playtest decides ([Discoveries](05_DISCOVERIES.md#2-the-detector)).

Shared rules for all tells:

- **World data, not reactions.** Tells are generated from the save's seed with the rest of the
  ground and stay hidden inside solid ground until a cut exposes them, so to the player they seem to
  appear as they dig. They are identical after reload and need no save data beyond the material IDs.
- **Presence, never value.** A tell says "something is this way", never what or how much. Some
  cracks and channels fade out with nothing at the end; a tell that always paid would be a treasure
  radar. Backfill is the exception: every pit holds something, even if only rubbish.
- **Straight digging always works.** Following a tell is the smarter, faster way, never the only way.
- **Felt as well as seen.** The dig-speed change is felt in the dark and by colour-blind players;
  the visual tell needs a lamp. Tells never rely on colour alone: lines, chunks and grain differ too.
- **Sideways as often as down.** Tells run in every direction, which is how they pull players off
  the main shaft. Keep Digging's biggest design flaw was that digging straight down beat the game.

#### Cracks and veins (rock, concrete)

- A crack shows as a pale, mineral-filled line with dark edges where a cut crosses it, inside a paler
  band of shattered rock broken into angular shards; the rock's own texture is full of dark hairlines,
  so a dark line alone would not read. A cut into the band breaks it loose along the crack in one go
  (about a metre, further with a bigger tool), with pale shards and dust; cutting across the rock
  beside it is ordinary rock.
- Cracks branch. Some open into an ore vein or end at a find; some thin out. Minerals really do
  collect in rock cracks, so veins and cracks are one feature.
- In concrete, cracks run from old damage toward weak spots and into the rooms behind walls.
- C4 on a crack breaks along it ([Tool and Movement](04_TOOL_AND_MOVEMENT.md#8-c4)).
- *Feel:* hit the rock once, read the line, choose a branch, and feel the tool bite faster.

#### Gravel channels and the pour

- A channel is an old riverbed: a winding band of gravel through clay or soil, running sideways as
  often as down. Heavy finds settled in it, as they do in real rivers.
- It is not a tunnel or a path. It is solid ground the player digs, and they can follow or ignore it,
  like a cable in "follow the thing".
- **The pour:** dig underneath a gravel section and it lets go in one rush. Its loose gravel
  disappears as debris and its finds tumble down to the player. The pour stays inside the undercut
  section (a few metres), never buries the player, never closes a route home, never leaves floating
  specks and never deletes a find. C4 under gravel triggers it too.
- *Feel:* a rushing slide and rattle, then quiet and a small pile of finds: a harvest earned by
  digging in the right place.

#### Disturbed ground (backfill)

- When something was buried, a hole was dug and filled again. Above and around selected buried
  things, the generator leaves a pit or column of loose, mixed backfill that cuts across the natural
  layers.
- It reads in the wall as a messy, chunky patch breaking the clean banding, and the tool suddenly
  sinks in faster. Follow it down or sideways and something waits at the bottom.
- Only what was buried on purpose leaves a pit: a **stash** (an old chest, opened where it lies; see
  [finds inside finds](05_DISCOVERIES.md#3-the-reveal-and-recognition-loop)) or **rubbish** (junk
  such as old TVs, found only in rubbish pits). Things that sank or were lost leave none and lie
  scattered, so ordinary digging keeps its own surprises. Every pit holds something, never an empty
  decoy, and pays well for following it: a rock-priced find at the bottom would be a letdown.
- It reads by grain, not a colour jump: the same soil turned over, lumpier and a little darker, with
  stones churned in, drawn from its own texture made from the site's soil and rubble.
- Pits stay in the recent fill for now: a refilled hole far down in clay or rock needs a story
  first. The first one lies a few metres under the plot centre, where an early shaft meets it in
  daylight.
- *Feel:* "why did it just get easy? Someone dug here before me."

#### Odd spots for special finds

Each unique sits in ground that does not match its zone: a gravel pocket in the rock, a small
concrete room in the clay, a clay lens in the rock. **The odd one out is the clue**, seen with the
player's own eyes instead of on a HUD. The generator shapes this ground around the space it already
reserves for each unique before placing ordinary finds.

### Host ground: each ground holds its own kind of find

Find placement prefers the ground a find belongs in (table above). It is a soft bias with scatter:
the odd coin in clay still happens. Prices stay fixed per type; host ground changes where things
are, never what they are worth, just as depth already changes the mix. The player builds a mental
map ("a gravel channel off to the left, worth a look"); that is knowledge, not a value radar.

### Local cleanup rules

- These are cutting responses, visual debris and the bounded releases (gravel pours, backfill
  slumps, crack breaks), never a global collapse
  hazard.
- Paper-thin soil fins and ribbons crumble as they are carved, even when long or attached at both
  ends. Remove their collision with their visible geometry; thicker useful ledges remain stable.
- Removal stays local to the worked section. Unrelated ledges, tunnels and overhangs remain stable.
- Plain dirt crumbs vanish, embedded valuables remain in place without bonus duplicates, and
  interesting finds survive intact for deliberate partial exposure and recognition.

## 5. Ground places

Big bodies of one ground inside a zone, a few per zone. They replace the earlier "hard pockets"
list: a concrete structure or a rock mass *is* the hard pocket, now with a reason to exist.

| Place              | Where       | What it is for                                                                                                         |
| ------------------ | ----------- | ---------------------------------------------------------------------------------------------------------------------- |
| Rock mass          | Mostly zone 3 | A block several metres across, criss-crossed by cracks and veins: read the cracks, pick a branch, or blast it        |
| Concrete structure | Zone 3      | A buried waterworks section: concrete walls and floor, soil inside, cracks marking the weak spots; sometimes a sealed room |
| Clay basin         | Zone 2      | A thick bowl of old pond clay: clean, calm digging around bones and organic finds; softer and greyer than the clay around it, so it is clay's tell |
| Gravel channel     | Zones 1–2   | See §4                                                                                                                 |

Rules:

- **Several solutions:** the current tool always makes visible progress; cracks and soft paths make
  it faster; C4 makes it fast; coming back after upgrades is an optional shortcut, never the only
  answer. Discovering a tough place early and demolishing it later is a designed power moment.
- **Legible before commitment:** cracks, fittings and a material clearly unlike the eternal
  boundaries show that a place will yield before the player sinks time into it. An undiscoverable
  solution is the same as no solution.
- **Never across the main descent as a wall.**

### Sealed rooms

The thrill of a cave is the break-through: dig, the wall gives, and there is darkness behind it.
Sealed rooms keep that moment without cave networks.

- Small and rare: one or two in the stone zone, each a section of old waterworks tunnel, and in the
  ancient zone a constructed chamber.
- Always sealed: the player always breaks in. Inside it is dark until the player's opening or a lamp
  lights it.
- The floor is settled silt with finds half-sunk in it, so the reveal-by-silhouette loop survives:
  nothing lies fully exposed on the floor.
- Rooms never connect into passages and never form a maze.
- *Why:* One Man's Trash's pocket areas "broke up the digging"; Keep Digging players loved its large
  hand-built caves. Meltopia's network of identical tunnels got players lost, so there are no
  networks, and cave zones were rejected ([Open Questions](13_OPEN_QUESTIONS.md#closed-ideas)).

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
- **Tells come from the seed**: zones, places, cracks, channels, backfill and sealed rooms are
  generated with the ground and stored as the same material/density data; they add no separate
  save records.

## 7. Lighting, marking and navigation

- **The "No Map Ever" pillar:** No minimap, compass, or GPS radar, ever.
- **Inherently vertical navigation (why this differs from _Meltopia_):** In _Meltopia_, players suffered navigation fatigue because the world was a sprawling, flat maze of identical horizontal tunnels. _Something Down There_ is fundamentally different: **it is vertical**. Near the surface, looking up reveals the open sky and the shaft of daylight; deeper down, the lamps you leave behind become the way home. The player's own carved shaft always points straight up.
- **Optional world markings:** For complex lateral branches off the main vertical shaft, the tool can apply simple, free, reusable chalk/spray symbols (arrow, home, return-here) readable by shape.
- **Early route lighting:** a few lamps come with the starter kit; every further lamp is bought one
  at a time at the computer for a flat price and kept for good. Lamps are not an upgrade track: one
  lamp is as good at the end as at the start, and a run leaves around a hundred of them lighting the
  hole. Lamp ownership is separate from the find bag.
- **Place anywhere:** a small, hand-sized lamp goes wherever the player aims: floors (upright),
  walls and ceilings (spike first), corners, next to finds or other lamps, at the player's feet.
  A spot too tight for it moves the lamp back along the aim; with nothing solid under it, it falls
  and settles, and remains recoverable. Only an empty kit refuses. The preview emits no light;
  only a placed lamp illuminates the work. Retrieve a lamp to place it again.
- Markings conform to the worked surface, can be rotated and erased for free, and save with the
  hole. Excavating their painted surface removes them. Lamps release physically when their
  support disappears, remain lit nearby, and retain their kit ownership. Sustained contact from a
  load on the crane's rope can knock a mounted lamp loose; portable lighting never permanently blocks recovery.
- **Sky light reaches down open shafts** and fades with depth.
- **Daylight reach:** daylight fades from the first metres, as it would down a real shaft:
  noticeably dimmer by 5 m, dim but readable at 10 m, lamps wanted from about 12-15 m, dark by
  about 20 m. The dug ground is lit like the surface (sun, sky and the light the sunlit ground
  throws back), so a hole reads as the same ground as the field around it; there is no extra fill
  inside holes.
  A walked ramp darkens almost like a shaft; the first underground work area never needs a lamp.
- **Sideways travel loses daylight at once.** Brightness follows the open route from the
  surface: going down costs little until the dark depth, but every metre dug sideways (or back
  up) dims it, like light turning a corner. A side tunnel is clearly darker a few metres in and
  near-black about 10 m in, at any depth; only its first metre or two still catch the shaft's light.
  There is no permanent ambient fill keeping soil or finds visible in unlit ground.
- **The shaft reads from below:** its light column and drifting dust are landmarks where the shaft
  is visible. Light does not pass through overhangs; the jetpack and reusable lamps support returns.
- **True darkness.** Below the reach of sky light, covered tunnels are near-black: material color,
  seams, visual tells and find silhouettes stay unreadable until light reaches them. Digging,
  movement and the felt tells (the ground suddenly digging easier) still work in the dark — light
  withholds information, not ability.
- **Sealed rooms are dark** until the player's own opening or a lamp lights them; no daylight
  reaches them through solid ground.
- **More sideways reasons, same short routes.** Tells and places pull players sideways, so the
  modest site width, lamps and marks keep every branch short and the way home readable.
- **Placeable lamps are the light.** They are the only light underground: place them to work, reveal
  finds and hold the route home. Owned lamps are reusable, repositionable and do not expire or drain
  charge; lost support leaves them recoverable nearby. Digging and C4 cannot destroy them.
- **Diffuse all-around lamps:** a neutral lantern illuminates every direction. Nearby soil retains
  texture instead of becoming an orange-white hotspot; solid ground still blocks the light.
  One lamp lights a useful stretch of tunnel or chamber with a broad, gradual falloff, not just a small pool beside its housing.
- **No personal light.** The tool does not act as a headlamp; that is what makes lamps the way you
  see and the way you remember the hole.
- **Zone lighting moods:** warm daylight near the surface → shade lower in the first zone →
  true darkness from there down, broken only by lamps.

## 8. Randomization rules

- Authored: zone layout and main grounds, depth ranges, boundary placement, general difficulty
  curve, which places and tells each zone uses, and relationships between buried places and the
  connected major finds.
- Randomized per save: find positions, depths within bands, rotations, cluster layouts, the exact
  shape and position of places, cracks, channels, backfill and sealed rooms, some surrounding junk.
  Variation preserves how related objects and major parts fit together.
- Validation also checks the ground: every unique sits in an odd spot, every rock body has a soft
  path, and every sealed room is closed until the player breaks in.
- The generator produces a candidate layout and validates [discovery pacing](02_CORE_LOOP.md#4-pacing-rules-generation-enforces-these)
  before accepting it.
- Every seed contains all special exhibits, ending parts and achievement-relevant finds, reachable
  and discoverable with baseline equipment.
- The accepted population is finite and persisted; patches never reroll an existing save.

## 9. No hazards

No lava, gas, oxygen, hunger, earthquakes, temperature damage, or monsters. The only pressure
is the shared battery, the bag's capacity, and the player's own greed — all soft, all fair, all
recoverable (see [Progression and Economy](06_PROGRESSION_AND_ECONOMY.md)). The gravel pour, the
slumps and the crack breaks are rewards, not hazards: each is a few metres of removal only, and never
harms, buries or traps the player.

*Why:* in the research, survival friction is the steadiest complaint: A Game About Digging a
Hole's fall damage and exploding battery, One Man's Trash's worms, Keep Digging 2.0 "patched in
stress". Some Keep Digging reviewers asked for cave-ins and danger, but the players this game is
for came for calm digging.
