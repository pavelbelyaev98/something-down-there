# 05 — Discoveries

## 1. Find categories and tiers

| Tier | Count target | Detector (frozen, §2) | Destination | Money | Bag Slots |
|---|---|---|---|---|---|
| **Common** | 20–30 types | Always silent | Sell only | Reliable income, worthwhile at any depth | 1 slot |
| **Distinctive** | 30–50 types | Noteworthy ones signal; some stay silent by design | Sell only; no first-copy/duplicate routing | Good money | 1–2 slots |
| **Unique** | Small set (a few) | Signals | Kept on the display forever; unsellable; one-sentence story; no mechanical perk | No sale | **0 slots** (never crowds bag) |
| **Ending parts / keys** | 3–4 finale components; other keys by content | Required parts have a discoverable trail | Unsellable; automatically available when needed | No sale | **0 slots** (never crowds bag) |
| **Oversized Salvage** | Handful of set pieces | Signals | Crane set-down spot at camp | Huge payday | **0 slots** (claimed via tag/clamp) |

Commons include the mineral ladder (coal → copper → iron → silver → gold → emerald → ruby → diamond),
bottles, plain stones, commonplace scrap, packaging and rubbish. Each zone has its own pair of
minerals (`114`): coal and copper in the recent fill, iron and silver in the sediment, gold and
emerald in the third zone, ruby and diamond in the deep stone. Pyrite, fool's gold, is a cheap
aside in the sediment: it looks like gold and sells like rock. Every mineral is plain, matte and
earthy like the plain rock the user approved, never glossy or finely detailed, yet reads as its own
material at a glance (user, 2026-10-06): coal is a black, layered lump, not a dark rock; iron is
rust-streaked, never glowing like lava; silver keeps its sparkle; copper is an ore or a native nugget
(an A/B). Gems dug out of the ground are big, dusty crystals, never darkly stained; the clean ones in
old chests are smaller and worth less (user, 2026-10-06: "weird smaller crystals cost more"). "Common" means routine to find
repeatedly, not merely familiar. **Uniques exist exactly once per save — never in multiples.**
During mechanics prototyping, independent unique identities may reuse the computer model; this is temporary art reuse, not multiple instances of the same unique.
Uniques and ending parts consume **zero bag slots**, so players never have to sacrifice income for the discoveries the game most wants them to appreciate.

Each type has one purpose: ordinary and repeatable finds sell; a few special exhibits and keys are
unsellable. No keep/sell sorting. Ordinary hauls pay, and special finds get their own display moment.

### Where finds sit

- **Host ground:** each find type prefers the ground it belongs in (rubbish in soil; each zone
  ground brings its own family). It is a soft bias with scatter, and prices stay fixed per type
  ([host ground](03_WORLD_AND_SITE.md#host-ground-each-ground-holds-its-own-kind-of-find)).
- **Tells point toward finds:** backfill pits and geodes lead somewhere
  ([ground tells](03_WORLD_AND_SITE.md#the-one-rule-different-ground-means-you-are-on-to-something)).
- **Uniques sit in lenses:** ground that does not match their zone, so the player can spot them
  with their own eyes ([lenses](03_WORLD_AND_SITE.md#lenses-around-uniques)).
- **Geodes** hold crystals on their inner walls, seen in the lamp's light before they are collected
  ([geodes](03_WORLD_AND_SITE.md#5-geodes)).
- **Buried on purpose:** a backfill pit holds what someone buried, a stash in an old chest; rubbish
  such as old TVs, and whatever sank or was lost, lies scattered like any find
  ([disturbed ground](03_WORLD_AND_SITE.md#disturbed-ground-backfill)).

**Constant rate, rising value.** A metre of descent keeps meeting finds at a roughly constant rate
to the bottom — depth changes *what* you meet, never whether digging pays. Each find type has a
narrow core depth band where most of it lives, plus a thin scatter band that sprinkles a few
outliers outside it: the odd lump of junk survives deep, the odd valuable turns up shallow. Junk
belongs to the recent fill, the mid ladder to the sediment, and the deep ground is mostly worth
carrying home. Price stays fixed per type; the mix is what rewards descending.
The shallow rock layer stays dense but not crowded. **The first few metres should feel almost as
full of fresh finds as the first scrape**, with rocks continuing and coal entering as the
player digs beneath the surface. Measure new objects emerging from intact soil at each dig face;
loose rocks falling down from earlier layers do not count. Farther down, the mix shifts toward
richer types while the encounter rate stays roughly constant.

Dense buried layers must keep excavation responsive. Hide meshes enclosed by untouched soil
and suspend anchored motion updates until digging reaches them; preserve every find's identity,
size and physical behaviour when it emerges.

Development X-ray makes excavation ground transparent and reveals nearby actual objects,
with no dot markers. This session-only inspection aid never changes soil, exposure, collision
or collection/extraction eligibility; turning it off restores normal concealment and lighting.

The **top metre belongs to plain rocks** — the junk you meet while the starter machine is still weak —
and the ore ladder starts just beneath it: coal first, then the rest in order. Rocks stay full size
and form a dense layer just beneath the surface, so the first shallow scrapes reveal several nearby
pieces. Placement clearance follows the actual rock geometry; empty bounding-box corners must not
force finds deeper or farther apart than necessary.

### The exposure rule (deliberate reveal for all finds)
- **Deliberate exposure applies to all finds, including common rubbish:** No instant vacuum auto-collect through solid dirt (the *Meltopia* anti-pattern). Every object must be dug around and exposed to a defined threshold (typically 50–60% voxel exposure) before it can be collected. The player must always see what they earned.
- **Distinctive objects:** A recognizable fragment creates a brief question. A little more excavation answers it through silhouette recognition. Collection takes a deliberate interaction.
- **Major discoveries & buried scenes:** Scale, arrangement, or unexpected context makes uncovering them an event. The payoff comes from the entire scene, not merely the item name.

## 2. The detector

> **Status: frozen.** The detector stays exactly as built: no new features, no new dependencies,
> no content that needs it. The ground tells ([03 §4](03_WORLD_AND_SITE.md#4-grounds-and-their-tells))
> are the intended replacement. A detector-off playtest decides: if testers find **every unique
> without help** and follow at least one tell unprompted, the detector is removed; otherwise it stays
> frozen and the reason is recorded.
>
> *Why:* the developer found a HUD panel to watch a messy way to build a hunch, and it pulls the eye
> away from the world the game wants players to read. One Man's Trash's scanner, an X-ray that
> revealed common items, was one of the worst parts of that game. The pass rule exists because the
> research also shows the opposite failure: Keep Digging 2.0 players finished "without finding a
> single fossil", and One Man's Trash completionists were locked out of 100% by collectibles they
> could not find.

The detector is passive equipment: the player never equips it. They simply dig.

- **Silent and visual by default.** A separate HUD detector shows three signal levels as the player sweeps their aim: weak near the edge of a buried find's detection cone, medium when closer to its bearing, strongest when looking directly toward it. Bars communicate alignment, not distance. No direction arrows, turn instructions, height labels or target markers; the aiming reticle stays independent. Optional accessibility audio and high-contrast cues remain planned.
- **Usually off:** The panel is completely hidden outside a close range or when looking away. Small angular and range margins prevent boundary flicker. A nearby object behind the player cannot light the detector; ordinary surface exploration stays quiet.
- No activation, battery drain or bag space is required. Any later physical tool attachment follows this same quiet aiming behavior.
- **Communicates presence, never value:** It reveals **neither exact identity, exact rarity, nor sale price**. Signal level follows aim alignment; the detector never spoils the item or labels ordinary finds as waste.
- **Aim owns the signal:** Respond to the buried eligible find most closely aligned with the current view. Looking toward another find changes the reading immediately; no timed lock holds an off-axis target. Equal bearings resolve consistently.
- **The reveal ends the hint:** As soon as any part of an object is uncovered, that object stops signaling, even before collection or extraction is available. Revealed objects never become fallback signals. A different, still-buried find can continue signaling if the player aims toward it.
- **Useful baseline:** Provides useful starting guidance without requiring expensive upgrades to locate mandatory story content.
- **Eligibility is authored per object**, never decided by price, size or metal content. Some
 distinctive finds deliberately do not signal, so that digging itself keeps rewarding the player
 outside signal-chasing.
- Signals can always be ignored; required parts matter when the player chooses to finish the story.
- Required finds have trails of related objects and ground tells and never depend on the detector.
 Revealed and collected finds stop signaling. No required detector upgrade.

## 3. The reveal and recognition loop

1. The player digs normally; the object appears partially.
2. **Interesting objects do not disappear when touched.** They stay physically present; the player
 excavates around them and watches the silhouette resolve.
3. Once enough is exposed, the object becomes interactable and can be collected.
4. **Recognition is the reward:** curved metal → handle → rectangular body → "…oh, it's a washing machine."
5. **Uniques tell a story:** one deadpan sentence, with first delivery decided in play. The
 current leaning is on arrival at camp; before pickup versus on arrival remains open.
 Recovered objects always support inspection and rereading. No inventory reading or inspection of commons.
6. No archaeology: no brushing minigame, no required cleaning (the yard cleaning of uniques is optional and quick), no identification timers, no
   mailing objects for appraisal. The game decides when enough is revealed; the player
   decides what is worth revealing.

Small/common finds are quick: a bite or two, instant pickup, clear feedback so nothing is collected
unseen. Interesting finds remain after wide cuts, support cleanup and C4; exposure makes them
collectible without requiring full cleaning or waiting for the player to name them.
For ordinary bag finds, holding the digging action collects an eligible aimed find as soon as it becomes available,
including while it falls and between digging strokes. Loose finds have a more generous pickup
reach than anchored finds; clear aim, exposure and bag capacity still apply.
Collection and its visual feedback never delay the next terrain cut. Held digging can collect
and make its normally scheduled cut in the same frame, without adding a second cut or fuel charge.
Automatic collection also reaches nearby, clearly uncovered ordinary finds in front of the player while
walking or holding dig, including elevated and falling pieces. It is not restricted to foot contact.
The automatic collection area stays close so surrounding loose rocks do not disappear too eagerly.
Soil, walls and partial burial still block it; a full bag stops all pickup (see §9).
There is no carrying or throwing finds by hand: a find is dug out, bagged or craned. A chest's ingots
and crystals are taken into the bag by hand instead: each piece waits for Interact while aimed at it,
one at a time, and neither digging nor walking past ever takes one (user, 2026-10-06).
Nearly excavated rocks break loose when only shallow surface contacts remain; substantial inner
burial keeps them anchored. Their real colliders and gravity determine the resulting motion.

**Buried connections: "Follow the thing"**:
Extensions of the cluster system where discoveries physically connect through the ground:
- A heavy cable leads away from a broken generator; a rusted chain disappears beneath a concrete slab; matching floor tiles outline a buried workshop; an industrial pipe bends toward machinery not yet visible.
- **The rule:** *The ground hints that something is near. The exposed world suggests what to do next.*
- **Trails pay along the way:** small finds sit along a cable, pipe or vein, so following it rewards
  each step instead of asking for blind trust.
- **Each zone has its own trail type**, decided with its main ground: chains and cables in recent
  fill, matching grooves and fittings in ancient ground.
- This gives lateral exploration an immediate visual reason rather than asking players to trust random sideways digging blindly.
- **Keep it simple:** No wiring puzzles, cable inventory, or repair chores. Following the connection means doing more of what is fun: digging.
- Built from authored, fully buried arrangements with preserved relationships, seeded and oriented as units.

**Whole-object crane recovery (mark underground, haul automatically)**:
The first proof is the unique buried retro computer; later oversized discoveries reuse the same mechanism.
The yard's small tower crane (the purchased pack's rig and motion) does it with its own rope and hook, the way riggers do: a lifting eye goes on the load and the crane's hook takes it. The crane's cable is one hoist rope, reeved in two falls from the trolley's sheaves to the hook block as on the pack's model, at all times: hanging, both run straight down; on a recovery the same cable is the smart rope: down a straight drop both falls hang apart as when idle and never cross; where the cable is pulled round the hole's rim or a tunnel's corner both wrap that same corner, so they come together into it and run on along the tunnel as one doubled cable, spreading again into the hook's block, with the tunnel's edges doing the work of a pulley at each corner. The idle crane rests with its hook over the set-down spots beside the camp, so the swing out to a hole is part of the show.
- **Deliberate exposure:** Excavate enough to meet the object's authored exposure requirement. Unique finds require manually aiming at the dirt around them: digging directly at the exposed object never redirects a stroke to its remaining covering soil. Ordinary finds retain that uncover assist. Full cleaning is unnecessary. Crane targets remain in the world; digging and proximity pickup cannot collect them.
- **Mark anywhere on the visible object:** Aim at any unobstructed part within interaction reach and hold Interact (E by default). A surface marker previews the chosen point and fills during the hold; releasing or losing the target cancels the unfinished hold. Completing the hold bolts a lifting eye on at that point: a worn, dirty swivel lifting point (a bolted flange, a ball swivel and a forged bow in chipped yellow paint, sized for the crane's hook). It stays through deployment and hauling, including after saving/loading; its flange stays flat on the load. The bow stands still while the hook is on its way and turns only in the last moments before it arrives, so the crane's hook threads through it, and swivels toward the hook once hooked. It is hidden by soil and objects, never a through-ground locator, and comes off the moment the crane lets go of the load at camp (or when planning fails). No special attachment hotspot.
- **Automatic deployment:** Marking finds the rope's way through connected space in the player's excavation and calls the crane; it starts moving only once the mark is placed. It swings over the hole mouth (as near as its jib reaches for a mouth beyond it) with its hook at its resting height. The crane's cable and hook are the smart rope: the moment the crane is aligned with the hole, the hook goes straight down from wherever it hangs, even mid-swing, into the hole and along the dug route to the object in one run, never waiting for the swing to settle, and visibly hooks into the lifting eye. From marking to the hook entering the tunnel takes several seconds, not tens. No surface-button trip or continued button hold is required.
- **Crane motion:** The pack's own levers (slew, trolley, hoist, hook turn), worked briskly by an automatic operator (the pack's overall speed knob raised). The long hoist rope trails the trolley's starts and stops like a pendulum, but an anti-sway assist settles it instead of letting it swing on, so the hook comes to rest within moments and loads touch down gently.
- **The haul:** Rope tension pulls a dynamic rigid body at the marked attachment point along that excavated route, including lateral passages and bends. The object swings, rotates and collides naturally; its pose is never locked to a rail. The player remains free to move and watch from underground or the rim; no camera takeover.
- **The rope:** The cable itself has gravity, inertia and length constraints. Slack hangs and sways while deploying, but a hauling cable visibly straightens under the load, including its last span at the hook. Stretching the pulling spring must not pay out extra cable. Recoil can briefly unload the rope before the reel catches up. The dug route supplies its initial shape, not fixed intermediate anchors: the rope rests and slides against the actual tunnel, reacting when supporting dirt is removed. Payout and reeling preserve its motion instead of rebuilding a rigid polyline.
- **Strain, rupture and momentum:** Recovery is a forceful, uneven haul. A slow jam first stretches the cable and winds up the motor; retaining dirt then tears away in a local chunk and the object surges, swings and rotates. Stronger pulls and harder impacts break larger chunks. If that surge hits the next dirt obstruction hard enough, its remaining kinetic energy breaks it immediately instead of restarting a careful waiting cycle. Breaking ground spends momentum; a spent or newly wedged load winds up again. The machine grows more aggressive the more it gets stuck: the longer the load stays wedged or crawls, the harder it pulls and the faster it reels, so a stubborn haul gathers force jam after jam instead of repeating the same stop and break, while an easy one stays at normal force and the ground still resists at first; it eases back slowly once the load runs free. Nearby untouched dirt, soft brushes and glancing contact remain intact. Every real break ejects crumbs and dust. Blocking commons loosen without losing their identities; mounted lamps can be knocked loose and remain recoverable. Follow the existing route and preserve unrelated ground and permanent obstacles. Once marked, recovery never asks for manual clearance or an Interact retry: persistent wedging builds a stronger pull and carries tension around bends. Sideways rocking alone does not count as escape, and neither does creeping along the walls far slower than the reel: that is a jam too, so the load never grinds up a tight shaft for long; the crane winds up and tears the retaining dirt away. Saving or pausing during a jam resumes automatic effort; arrival still lowers the load safely onto its pad.
- **Arrival:** At the top the hook goes back on the crane's hoist with the load hanging from its eye, still a physical object that swings and turns on the hook, never posed. The crane lifts it clear, carries it to a free spot beside the camp and lowers it until the ground takes its weight, then lets go; it lands and settles however it hung. A unique is solid to the player throughout, never a ghost: the player can stand on it and ride it up out of the hole and over to camp, and one that swings or is dragged into the player shoves them aside. It stays there for good: never sold, never moved into the bag, no stand or placement step. Oversized salvage becomes eligible for once-only cash-in at the surface computer.
- **Continuity:** Full bags do not block recovery, the crane does not spend the player's battery, and saving mid-haul preserves the object, rope progress, the crane's pose and changed terrain together. Dense finds and automatic ground clearance must keep movement and camera control responsive throughout the haul, including each fresh cut during ascent; a high average frame rate does not compensate for recurring stalls.

**Moving discoveries**: objects, including uniques waiting for the crane, fall and settle physically once surrounding soil no longer supports them. The buried computer starts deeper to allow a substantial approach tunnel and recovery test.

**Finds inside finds**: occasional authored containers hold another discovery. Dig down a stash
pit into the pocket of air its old chest stands in, hold Interact to force its rusted lock, and the
lid opens on what it holds: a prospector's ingots of bronze, silver and gold and a few gem crystals,
worth far more than anything loose in the soil around it, each taken by hand. The lock is forced from
in front of it (user, 2026-10-06). The deeper the chest, the richer: a shallow one holds mostly bronze,
some silver and the odd gold ingot, a deep one mostly gold and gems. The pieces lie heaped at the back
as if tossed in, never laid out in rows (user, 2026-10-06).
The outside gives a clue; the contents deliver a second reveal and a small piece of buried history.

- Use a few selected objects, not every box or appliance. Contents fit the container and its scene.
- Opening happens in the world by hand: hold Interact on it, the same hold that marks a unique for
  the crane; no lockpicking, extra keys or inventory search. A lock buried for decades is rusted
  shut, and it gives. A key far from its lock is a blind search, and one beside it adds nothing.
- A container too big for the bag opens where it lies; it is never craned or sold. Once emptied it
  goes the next time it is out of the player's sight and untouched, so the pits do not fill with
  empty chests and nothing pops away in front of the player or from under their feet (user,
  2026-10-05; a first version).
- Contents become visible before normal collection. Ordinary valuables sell; special exhibits keep
 their existing display/story role. The container's own collection must not hide or lose its contents.
- Contents belong to the save's finite find population; opening or reloading never rerolls or
 duplicates them. Full-bag overflow follows the normal rules.

## 4. First-slice objects (built before bulk production)

| Object | Tests |
|---|---|
| Washing machine | Large appliance: box + circular door readability |
| Hand drill | Small handheld silhouette recognition |
| Gearbox / engine block | Machine parts; natural lead-in to a cluster |
| Mammoth bone / tusk | Organic curves; ties to the Danube bones inspiration |
| Buried retro computer | Unique exhibit, recognizable computer silhouette and whole-object crane recovery; a different model from the trading terminal |

These five are prototyped and reviewed before any large content batch.

## 5. Related-object clusters

Five authored micro-scene templates to start, rotated and placed procedurally with slight jitter:

1. **Bone scatter** — skull fragment, ribs, tusk piece spread over a few meters.
2. **Vehicle parts** — wheel, axle, bumper, engine block, arranged as if a car sank here.
3. **Household cluster** — plates, bottles, stove, sewing machine in a collapsed heap.
4. **Machine fragments** — gears, drive shaft, boiler plate leading toward something larger.
5. **Odd arrangement** — deliberately placed objects (a circle of bottles around a tool), feeding
 the mystery.

Rules: authored relationships, no pre-dug chambers, counted once in the finite population, validated
for spacing. A cluster is a suggestion, never a quest marker.

Clusters should read as parts of a coherent buried place — a household, workshop or waterworks. The relationship gives the next dig a reason beyond another signal.

## 6. Large discoveries

A few per run (target 3–5): a car, a large appliance pile, a machinery section. The player
excavates most of it first; a short local extraction gives the physical payoff, then the whole object
is transferred to its surface destination. The salvage crane is the default release. Whether some
heavy finds (a vehicle, a boiler) need a special local release, such as salvage bladders that
inflate with a hiss and unstick the find with a mud-release pop, is decided from the first oversized
salvage playtest, only if the crane cannot deliver the load.

Once released, the whole object transfers to the surface automatically. No crawler sled, widened routes,
car-wide shaft to the sky or cinematic camera takeovers; the player stays in control. Test the transfer
presentation beneath ceilings and overhangs without visible cable clipping.
Extraction always delivers the whole object — a large find that yields only a token part reads as a
letdown. Some very large discoveries may remain in place permanently as landmarks; their
non-extractable nature is clear and discovery is credited in place.

Major discoveries are connected. In the current experimental direction they are pieces of one huge
buried structure the player keeps meeting along the route — a curved wall here, another piece
deeper down, pieces that only later prove to share an edge, a joint or a material. Matching joints,
seams and fittings connect them; the final object explains what they belong to. Another option
being considered is parts collected to open something, possibly at ground level. What the parts add
up to is still an open question. Smaller finds continue around the major ones.

## 7. Value and rarity

Value and collection roles:

1. **Common** — reliable early income, always worth collecting; the roster shifts with depth
   instead of the price, so late trips are not paid in coal.
2. **Distinctive** — good money; the "that haul paid for the drill" tier.
3. **Rare** — several expeditions' worth, never enough to buy half the upgrade tree at once.
4. **Unique** — permanent display and story, no sale or mechanical effect.

No jackpots that finish the economy; no trash that feels like a waste of a slot.

The same item has the same price at every depth. Deeper zones can contain richer types or mixes;
a gold bar never receives a depth bonus. “Rare” describes a payout, not another upgrade system.

## 8. Recovered uniques at camp

- The crane sets each recovered unique down at its own spot beside the camp, where it stays. No
 stands, shelves or placement step; no carrying task underground.
- Each records name + depth found. No prices, no condition, no rarity labels.
- Recovered uniques support story inspection and rereading where they stand. Completion follows the
 recovered collection.
- **Optional cleaning:** a unique arrives caked in mud; the player may spray it clean with the tool
  where it stands. The story line appears when it is cleaned or recovered, whichever comes first
  ([recovered uniques](07_SURFACE_HUB_AND_DISPLAY.md#5-recovered-uniques-at-camp)).

## 9. Inventory behavior for finds

- The bag is abstract; there is no physical carrying of buckets or crates, and no inventory screen.
- **Hard stop when full:** the player cannot pick up; the find stays exactly where it is in the
 world, and can be retrieved on a later trip.
- **Nothing is ever deleted.** No overflow teleport, no inventory destruction, no drop-on-death.
 Full capacity stops pickup, not digging or travel; excess valuables persist without blocking the route.
 Aiming through a common find with a full bag still cuts the ground behind it within normal tool reach;
 walls, equipment and unique objects retain their normal blocking behavior. Cutting keeps its fuel cost and cadence.
 Show a persistent inventory-full HUD banner in the same style as the low-fuel warning; both remain readable together.
 No discarding; sale, extraction and reload preserve discovery credit.
- Uniques and ending components never consume capacity and are never lost.

## 10. References and humor

Original parody only: objects may evoke an era or a region without naming real brands, games
or people. The humor comes from what the object is and how the economy treats it — deadpan, not
loud. No body-sound jokes.

## 11. Mystery objects

The three-step escalation is delivered entirely through finds:

1. **Anachronistic junk** — a soda can too deep, a rubber duck in an ancient layer.
2. **Too correct** — a rustless tool, a bottle standing upright under tons of sediment, a part
 matching no nearby machine.
3. **Constructed impossibilities** — machinery built from ancient materials with a modern function.

The trail ends at the final object: **a modern object built in impossibly ancient materials**.
Its exact identity stays open; its construction makes the earlier major parts fit together.
An unusually intact ordinary object in undisturbed sediment can foreshadow this without an explanation.
See [Ending and Mystery](11_ENDING_AND_MYSTERY.md).

Impossible materials carry one small, consistent contact signature: an unusually clean cut, a
glass-like resonance, dust that settles too neatly. It is material feedback at the moment of
contact, always with a visual counterpart so the game works muted — never a direction, proximity or
value cue, and never something that reads as responding. See
[Game Feel, Art and Audio](09_FEEL_ART_AND_AUDIO.md).
