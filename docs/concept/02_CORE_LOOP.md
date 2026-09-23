# 02 — Core Loop

## 1. The minute loop

```
DIG → SIGNAL → INVESTIGATE → REVEAL → RECOGNIZE → COLLECT
 ↑ ↓
UPGRADE ← SELL ← SURFACE ← (bag full / battery low / curiosity satisfied)
```

Rules for each beat:

| Beat | What happens | Rule |
|---|---|---|
| **Dig** | Hold-to-dig bites chunks of voxel ground; dust and material fall; the tool adapts automatically to what it's biting | Always satisfying from the starting shovel, never requires clicking speed |
| **Signal** | The detector reacts silently — the tool glows/shivers, a subtle edge-of-screen hint grows with proximity and general direction | One target at a time; quiet intervals; never reveals value; can be ignored |
| **Investigate** | The player chooses to follow the hunch, dig sideways, or keep going down | Signals suggest, never prescribe; ignoring one is never wasted work |
| **Reveal** | Digging around an object exposes it little by little; shape becomes readable before identity | Deliberate exposure required for all finds (50–60%); no vacuum auto-collect through solid dirt |
| **Recognize** | "Wait… is that a—" The object's silhouette resolves into identity | This moment is the game's core reward; objects must read at partial exposure |
| **Collect** | Ordinary pickups enter the bag; rope-recovered finds are marked underground and hauled whole to the surface | Exposure precedes interaction; hold Interact on a visible part to mark; uniques remain unsellable exhibits |
| **Return** | Climb your own hole with the jetpack; battery is an action budget; return-power warning shows safe/risky/critical | No normal surface teleport or added return system; jetpack, reusable lamps, no map |
| **Sell** | The surface computer sells the ordinary haul, then immediately shows upgrades | One Sell All button; no deposit chore or second station |
| **Upgrade** | Buy the next level of a track; visible change on the tool; practical benefit shown | Sequential, transparent, each purchase changes the next outing |

## 2. The session loop (30–60 min)

1. **Plan (1 min):** check the special display, the fat wallet, the next upgrade. Pick an intention:
 "reach the next zone", "chase that signal", "afford the drill".
2. **Dig (20–45 min):** descend, chase signals, explore sideways, discover, get greedy.
3. **Tension (optional):** the bag fills, the battery drops, the return warning turns orange.
4. **Decide:** keep going for one more thing, or leave with everything. This decision is the game's
 entire risk.
5. **Return and cash in (5–10 min):** climb, sell, recharge, upgrade, glance at the display wall.
6. **Repeat** because a discovery or the next meaningful upgrade makes another outing appealing.

These are session targets, not timed stages. A session can contain several outings; a calm, voluntary
return is as valid as pushing for one more find.

## 3. Campaign arc (3–5 hours, four zones)

| Phase | Zone | Experience |
|---|---|---|
| Hour 1 | **Recent fill** | Slow shovel, believable finds, first purchases, first hard pocket |
| Hours 2–3 | **Old sediment** | Real capability jumps, clusters, first "too modern for this depth" oddity |
| Hours 3–4 | **Deep clay/stone** | Richer finds, lamps, connected major parts; growing power exceeds the tougher ground |
| Hours 4–5 | **Ancient constructed** | Large, fast excavation; the final object reveals what the major finds were for; ending and Continue Playing |

The last meaningful purchase should land near the end of the run so its power gets used (timing
prototype-tuned).

## 4. Pacing rules (generation enforces these)

- A noteworthy discovery is guaranteed early (first ten minutes), staged near the shaft mouth and
  readable within its first bites; the first sale must afford the first purchase so the loop closes
  in the first session (see [Economy tuning targets](06_PROGRESSION_AND_ECONOMY.md#9-tuning-targets-validated-in-prototype)).
- Dry spells between noteworthy discoveries are bounded along representative exploration routes;
  this is not an elapsed-time guarantee for every possible path.
- Related objects cluster; unrelated major finds never clump.
- One guaranteed major-scale discovery per zone; the major parts connect to each other, so the mystery grows with depth (the exact payoff is still open).
- New object silhouettes keep appearing until the end; the late game is never "more dirt".
- Novelty is never dumped early: strong finds are distributed across all four zones.
- These rules validate a candidate layout before it is accepted; an accepted population persists and
 is never rerolled by a patch.

## 5. Anti-straight-down design

The reviewed failure: the optimal strategy becomes "ignore the game, dig straight down". This game
answers structurally, not with friction:

1. **Clusters and signals pull sideways** — the best discoveries are rarely on the main shaft.
2. **Fixed value per type:** deeper zones can contain more valuable things, but a gold bar has the
 same price at every depth. Clusters make lateral discoveries worthwhile; compare earnings in play.
3. **Hard pockets are optional and sideways**; the main descent is never hard-blocked.
4. **Components and uniques live off-shaft**, so the ending and the display reward exploration.
 Related finds and the existing detector give required parts a discoverable trail; no blind final hunt.
5. **No friction mechanics are used to stop rushing** — no stamina, no idle drain, no cooldowns, no
 enemies. The game respects the speedrunner and simply hides its best moments to the side.

## 6. The core test (if the prototype works, this happens repeatedly)

> "I should probably go back…"
> *a quiet cue suggests something nearby*
> "…fuck it, one more thing."
> Sideways. A weird shape. More exposure. Recognition. Collected.
> Bag almost full. Battery uncomfortable. Barely make it home.
> **SELL ALL.** Finally afford the ridiculous upgrade.
> *remember the hard pocket at 14 m*
> …and immediately go back down.

A calm return followed by eagerness to use the upgrade also passes this test. Battery anxiety is not
required; the discovery and the new capability are what pull the player back.

## 7. Never in the loop

- No unskippable cutscenes mid-run (story is object-based; the ending is the only sequence).
- No forced combat, stealth, parkour or puzzles.
- No random inventory loss, no loot deletion from failure.
- No timed pressure, no daily systems, no FOMO.
- No walking through buildings or menus between digging and upgrading.
