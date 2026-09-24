# 05 — Discoveries

## 1. Find categories and tiers

| Tier | Count target | Detector | Destination | Money | Bag Slots |
|---|---|---|---|---|---|
| **Common** | 20–30 types | Always silent | Sell only | Reliable income, worthwhile at any depth | 1 slot |
| **Distinctive** | 30–50 types | Noteworthy ones signal; some stay silent by design | Sell only; no first-copy/duplicate routing | Good money | 1–2 slots |
| **Unique** | Small set (a few) | Signals | Kept on the display forever; unsellable; one-sentence story; no mechanical perk | No sale | **0 slots** (never crowds bag) |
| **Ending parts / keys** | 3–4 finale components; other keys by content | Required parts have a discoverable trail | Unsellable; automatically available when needed | No sale | **0 slots** (never crowds bag) |
| **Oversized Salvage** | Handful of set pieces | Signals | Fixed surface salvage pad | Huge payday | **0 slots** (claimed via tag/clamp) |

Commons include the mineral ladder (coal → copper → iron → silver → gold → emerald → ruby → diamond),
bottles, plain stones, commonplace scrap, packaging and rubbish. "Common" means routine to find
repeatedly, not merely familiar. **Uniques exist exactly once per save — never in multiples.**
During mechanics prototyping, independent unique identities may reuse the computer model; this is temporary art reuse, not multiple instances of the same unique.
Uniques and ending parts consume **zero bag slots**, so players never have to sacrifice income for the discoveries the game most wants them to appreciate.

Each type has one purpose: ordinary and repeatable finds sell; a few special exhibits and keys are
unsellable. No keep/sell sorting. Ordinary hauls pay, and special finds get their own display moment.

**Constant rate, rising value.** A metre of descent keeps meeting finds at a roughly constant rate
to the bottom — depth changes *what* you meet, never whether digging pays. Each find type has a
narrow core depth band where most of it lives, plus a thin scatter band that sprinkles a few
outliers outside it: the odd lump of junk survives deep, the odd valuable turns up shallow. Junk
belongs to the recent fill, the mid ladder to the sediment, and the deep ground is mostly worth
carrying home. Price stays fixed per type; the mix is what rewards descending.
The accepted shallow rock layer stays intact. **The first few metres should feel almost as
full of fresh finds as the first scrape**, with rocks continuing and coal entering as the
player digs beneath the turf. Measure new objects emerging from intact soil at each dig face;
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
and form a dense layer just beneath the turf, so the first shallow scrapes reveal several nearby
pieces. Placement clearance follows the actual rock geometry; empty bounding-box corners must not
force finds deeper or farther apart than necessary.

### The exposure rule (deliberate reveal for all finds)
- **Deliberate exposure applies to all finds, including common rubbish:** No instant vacuum auto-collect through solid dirt (the *Meltopia* anti-pattern). Every object must be dug around and exposed to a defined threshold (typically 50–60% voxel exposure) before it can be collected. The player must always see what they earned.
- **Distinctive objects:** A recognizable fragment creates a brief question. A little more excavation answers it through silhouette recognition. Collection takes a deliberate interaction.
- **Major discoveries & buried scenes:** Scale, arrangement, or unexpected context makes uncovering them an event. The payoff comes from the entire scene, not merely the item name.

## 2. The detector

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
- Required finds have trails of related objects and the existing broad detector cues. Revealed and
 collected finds stop signaling. No required detector upgrade.

## 3. The reveal and recognition loop

1. The player digs normally; the object appears partially.
2. **Interesting objects do not disappear when touched.** They stay physically present; the player
 excavates around them and watches the silhouette resolve.
3. Once enough is exposed, the object becomes interactable and can be collected.
4. **Recognition is the reward:** curved metal → handle → rectangular body → "…oh, it's a washing machine."
5. **Uniques tell a story:** one deadpan sentence, with first delivery decided in play. The
 current leaning is before placement; before pickup versus immediately before placement remains open.
 Placed objects always support inspection and rereading. No inventory reading or inspection of commons.
6. No archaeology: no brushing minigame, no 100% cleaning requirement, no identification timers, no
   mailing objects for appraisal. The game decides when enough is revealed; the player
   decides what is worth revealing.

Small/common finds are quick: a bite or two, instant pickup, clear feedback so nothing is collected
unseen. Interesting finds remain after wide cuts, support cleanup and C4; exposure makes them
collectible without requiring full cleaning or waiting for the player to name them.
For ordinary bag finds, holding the digging action collects an eligible aimed find as soon as it becomes available,
including while it falls and between digging strokes. Loose finds have a more generous pickup
reach than anchored finds or physical lifting; clear aim, exposure and bag capacity still apply.
Collection and its visual feedback never delay the next terrain cut. Held digging can collect
and make its normally scheduled cut in the same frame, without adding a second cut or fuel charge.
Automatic collection also reaches nearby, clearly uncovered ordinary finds in front of the player while
walking or holding dig, including elevated and falling pieces. It is not restricted to foot contact.
The automatic collection area stays close so surrounding loose rocks do not disappear too eagerly.
Soil, walls and partial burial still block it; a full bag stops all pickup (see §9). Deliberately dropped/thrown finds wait
until the player leaves and returns, or deliberately aims to collect them.
Nearly excavated rocks break loose when only shallow surface contacts remain; substantial inner
burial keeps them anchored. Their real colliders and gravity determine the resulting motion.

**Buried connections: "Follow the thing"**:
Extensions of the cluster system where discoveries physically connect through the ground:
- A heavy cable leads away from a broken generator; a rusted chain disappears beneath a concrete slab; matching floor tiles outline a drowned workshop; an industrial pipe bends toward machinery not yet visible.
- **The rule:** *The detector suggests that something exists. The exposed world suggests what to do next.*
- This gives lateral exploration an immediate visual reason rather than asking players to trust random sideways digging blindly.
- **Keep it simple:** No wiring puzzles, cable inventory, or repair chores. Following the connection means doing more of what is fun: digging.
- Built from authored, fully buried arrangements with preserved relationships, seeded and oriented as units.

**Whole-object rope recovery (mark underground, haul automatically)**:
The first proof is the unique buried retro computer; later oversized discoveries reuse the same mechanism.
- **Deliberate exposure:** Excavate enough to meet the object's authored exposure requirement. Full cleaning is unnecessary. Rope targets remain in the world; digging, proximity pickup and physical grabbing cannot collect them.
- **Mark anywhere on the visible object:** Aim at any unobstructed part within interaction reach and hold Interact (E by default). A short progress indicator confirms the mark; releasing or losing the target cancels the unfinished hold. No special attachment hotspot.
- **Automatic deployment:** Marking starts the rim winch. The rope comes down through connected space in the player's excavation and visibly attaches. No surface-button trip or continued button hold is required.
- **The haul:** Rope tension pulls a dynamic rigid body at the marked attachment point along that excavated route, including lateral passages and bends. The object swings, rotates and collides naturally; its pose is never locked to a rail. The player remains free to move and watch from underground or the rim; no camera takeover.
- **The rope:** The cable itself has gravity, inertia and length constraints. Slack hangs and sways; taking up tension straightens it, and the load's recoil sends motion back along it. The dug route supplies its initial shape, not fixed intermediate anchors: the rope rests and slides against the actual tunnel, reacting when supporting dirt is removed. Payout and reeling preserve its motion instead of rebuilding a rigid polyline.
- **Contact before breakage:** Rope tension first tries to pull, swing and rotate the load through normal physics. Nearby dirt and brief impacts remain intact. During a short sustained jam, the winch reels faster and builds tension until a small patch at the actual obstruction gives way. The freed object surges above ordinary hauling speed and swings through physical spring recoil before settling back to cruise. If one chip leaves it wedged, continued physical contact needs only a short retry; lost contact or useful progress starts a fresh attempt. Every real soil break ejects local dirt crumbs and a small dust puff. Common finds that remain pinned against the load can be loosened from their soil and pushed aside, retaining their identities. Mounted lamps in the way are knocked loose by sustained contact and remain lit, physical and recoverable. Follow the existing route and preserve unrelated ground and permanent obstacles. Once marked, recovery never asks the player to clear a jam or press Interact again: persistent wedging automatically builds a stronger pull, continues chipping actual retaining contacts and carries tension around a tight bend. Sideways rocking alone does not count as escape. Saving or pausing during a jam resumes this automatic effort.
- **Arrival:** Lower the whole object onto the receiving pad. A unique is stored safely for individual display placement and never sold; oversized salvage becomes eligible for once-only cash-in at the surface computer and a display record.
- **Continuity:** Full bags do not block recovery, the winch does not spend the player's battery, and saving mid-haul preserves the object, rope progress and changed terrain together. Dense finds and automatic ground clearance must keep movement and camera control responsive throughout the haul, including each fresh cut during ascent; a high average frame rate does not compensate for recurring stalls.

**Moving discoveries**: objects, including unique rope targets, fall and settle physically once surrounding soil no longer supports them. The buried computer starts deeper to allow a substantial approach tunnel and recovery test.

**Finds inside finds**: occasional authored containers hold another discovery. Dig out a
suitcase, expose and open it with the existing tool, then see coins or an odd keepsake inside.
The outside gives a clue; the contents deliver a second reveal and a small piece of buried history.

- Use a few selected objects, not every box or appliance. Contents fit the container and its scene.
- Opening happens in the world with the same tool; no lockpicking, extra keys or inventory search.
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
| Buried retro computer | Unique exhibit, recognizable computer silhouette and whole-object rope recovery; a different model from the trading terminal |

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
is transferred to its surface destination. For suitable heavy finds (like a vehicle or boiler), this
local release can feature bladder-assisted unsticking: attaching salvage bladders that inflate with a
hiss, heave the find with a satisfying mud-release pop, and unstick it from the ground.

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

## 8. Display integration

- The surface display is a growing shelf/wall/column with **compatible spaces**.
- **Empty spaces are visible from the start; undiscovered shapes stay hidden**.
- A special exhibit is stored safely on pickup. At the yard, bring it out and place it individually;
 choose any compatible shelf space or stand, with neat snap placement. No carrying task underground.
- The display records name + depth found. No prices, no condition, no rarity labels.
- Placed objects support story inspection and rereading. Completion follows the exhibit collection,
 not an assigned arrangement. Basic display capacity never requires a frame purchase.

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
