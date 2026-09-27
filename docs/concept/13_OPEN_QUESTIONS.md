# 13 — Open Questions

Genuinely undecided choices, with current options and leanings. None block current work unless the
row says so. Once a choice is made, document it in the relevant chapter and remove it here. Ideas
that were considered and rejected are listed under [Closed ideas](#closed-ideas), with the reason,
so they are not proposed again without new evidence.

## Content and systems

| Topic | Open choice | Leaning / next step |
|---|---|---|
| Detector | Keep the frozen HUD detector / remove it in favour of ground tells | Frozen as built: no new features or dependencies. A detector-off playtest on a fresh save decides. Pass: testers find **every unique without help** and follow at least one tell unprompted, and the detector is removed (HUD, code, concept references). Fail: it stays frozen and the reason is recorded. See [Discoveries §2](05_DISCOVERIES.md#2-the-detector) |
| Winch self-rescue | Current automatic recovery / the rim winch hauls the player up their own hole | To test alongside recovery fees and debt: at zero battery (or "I'm stuck" in pause) the winch lowers its rope, hooks the player and hauls them up the dug route; fee on arrival. Keep whichever feels better. See [Progression §5](06_PROGRESSION_AND_ECONOMY.md#5-fuel-shared-battery-and-recovery) |
| Sealed rooms | How many, how large | Start with one or two small rooms per zone; tune from playtest. Never connected into passages ([World §5](03_WORLD_AND_SITE.md#sealed-rooms)) |
| Find rosters | Exact common and distinctive object lists | Keep the 20–30 / 30–50 type targets, organised by zone and host ground; new silhouettes keep appearing to the bottom of the 150 m site. Uniques and ending parts consume zero bag slots |
| Mystery payoff | Ancient junk-making machine / one huge buried structure / parts collected to open something (possibly at ground level) | Undecided, no leaning. Decide before the mystery and ending work; earlier finds only need to be able to carry fragments of whichever is chosen |
| Final meaningful purchase | Exact point in the campaign | Test around 75% of first completion, leaving substantial deep excavation for the final machine |
| Zone names | Final names | Keep the descriptive placeholders until content work |
| Achievements | Final 5–10 achievements | Use the [candidate set](12_ACHIEVEMENTS_AND_COMPLETION.md#3-candidate-set-to-be-finalized-with-content); natural accomplishments, no grind or seed-exclusive requirements |

## World and art

| Topic | Open choice | Leaning / next step |
|---|---|---|
| Final object | Ancient household technology / modern machine in ancient materials / ancient original of the player's machine / another object | Choose with the mystery payoff. Its recognizable modern function must explain the major connected finds; no candidate is selected |
| Ancient material | Look, dig response and contact signature of the zone-4 ground | Defined with the zone-4 structure and signature work; must read as clearly unlike bedrock ([Ending §1](11_ENDING_AND_MYSTERY.md#1-the-mystery-trail)) |

## Interface

| Topic | Open choice | Leaning / next step |
|---|---|---|
| Price on hover | Small fixed price once a sellable find is exposed enough to collect / price only when selling | Decide from the playable-slice playtest. No hidden-item or unsellable-item prices; no delayed common pickups |
| Text scale and screen reader | Optional later additions | Scope undecided; sound captions are already required |

## Tuning

Tune recognition, discovery spacing, net income, upgrades, charge and return burden together, and
tune each zone's main ground against the tool level players typically own on arrival so no zone feels
like a restart. Numerical targets in [Progression and Economy](06_PROGRESSION_AND_ECONOMY.md) and the
[Prototype Plan](14_PROTOTYPE_PLAN.md) are starting points to validate.

## Closed ideas

Decided against. Reopen only with new evidence, and say what changed.

| Idea | Why not |
|---|---|
| **Seam cleaving / boulder fields** (cut along a seam so a whole slab or boulder comes loose) | The only payoff was saved time: "a boulder comes loose, for what?" Tool upgrades and C4 already cover hard ground. Cracks now give the same "read the ground" choice with a real reward: faster digging *and* a direction ([World §4](03_WORLD_AND_SITE.md#cracks-and-veins-rock-concrete)) |
| **Clay lifting out as clean blocks** | Same problem as seam cleaving: speed without a reason to care |
| **Stuck machinery** (dig away the dirt jamming a flap or hatch so it swings open) | Hard to understand underground, hand-built per object and close to a puzzle. Crackable containers give the second reveal more simply |
| **Cave zones or tunnel networks** | They break "the hole is yours", mean less digging, show finds fully exposed (skipping the reveal) and recreate Meltopia's getting-lost complaint. Small sealed rooms keep the break-through moment ([World §5](03_WORLD_AND_SITE.md#sealed-rooms)) |
| **C4 weak in clay** (or against any common ground) | Keep Digging's dynamite was "beyond useless"; Meltopia's dynamite nerf against dirt made its shovel hated. C4 is strong everywhere and stronger on cracks ([Tool §8](04_TOOL_AND_MOVEMENT.md#8-c4)) |
| **Tells that always lead to treasure** | That is a radar in disguise. Some cracks and channels fade out; backfill pits always hold *something*, not necessarily something valuable |
| **Ending timelapse** | Needs every cut recorded from the save's first minute, all for about twenty seconds at the end. Before-and-after shows the same scale cheaply |
| **Paid surface recharge** | A tax with no decision that punishes battery upgrades (Keep Digging's backlash); the battery's job is trip length |
| **Digging your own way out at zero battery** | At 150 m it is long, punishing survival friction of the kind reviewers hated in A Game About Digging a Hole. The winch self-rescue is tested instead |
| **A wider site** | Not needed: the current width fits a few places per zone and keeps sideways routes short enough that nobody gets lost. Revisit only if places crowd each other |
| **Putting dirt back** (Keep Digging's fill, One Man's Trash's dirt-spitting) | Loved in both games, but the jetpack already solves getting back up, and building would be a second verb. Not now |
| **Detector upgrades** (range, direction) | Would turn the hint into a value radar; the detector is frozen anyway |

## Later work

- Localization languages.
- Store copy, content disclosure, tags, trailer and exact demo timing.
- Steam Cloud timing.
- Performance budgets and final UI art treatment.
- Final pause-menu layout and any additional assist beyond hold/toggle digging.
- Mono audio option, if the audio pass supports it.
- Pricing beyond the working $6.99–9.99 range.
