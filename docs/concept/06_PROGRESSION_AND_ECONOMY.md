# 06 — Progression and Economy

## 1. The shape of progression

- **One currency: money.** No second currency, no crafting, no blueprints, no license tiers, no RNG
 gates.
- **Money is banked only when sold at the surface**. This makes every trip a risk decision
 without ever deleting anything on failure.
- **Strictly sequential purchases:** you always buy the next level of a track; a lucky find cannot
 skip levels.
- **Fully transparent shop:** current → next effect, cost, and practical benefit always shown;
 locked items visible with their requirement.
- **The final meaningful purchase lands near the end** of the run so its power is used
 (target about 75% of first completion; prototype-tuned).

## 2. Upgrade tracks (five)

| Track | What it improves | Meaningful changes (cadence prototype-tuned) |
|---|---|---|
| **Tool** | Power, bite size, adaptation quality | New head/attachment, dramatically faster digging, much faster excavation of previously tough ground |
| **Battery** | Capacity and efficiency of the shared dig+jetpack battery | Longer expeditions; efficiency that makes old trips trivial |
| **Jetpack** | Speed, efficiency, assists | Sustained ascent, steering assists, hover hold |
| **Bag capacity** | Bag capacity | Strong steps; a full trip becomes a real haul |
| **C4** | Blast size, pack size, efficiency | Room-clearing blasts; cheaper demolition |

**Every track has twelve levels**, including its starter level, with eleven sequential purchases. Every purchase must noticeably
improve the next outing — no "invisible +5%" upgrades or cosmetic bolts standing in for power.
Purchases apply immediately, without a blocking animation. The tool uses shovel scoops through
level six and gains continuous drill motion at level seven; subsequent levels strengthen it.

The silent detector is fixed starter equipment, not a paid track, and it is frozen: it may be
removed in favour of the ground tells ([Discoveries](05_DISCOVERIES.md#2-the-detector)). Never sell
detector range or direction upgrades. Jetpack and C4 tracks enter the shop with their mechanics, not as
placeholder purchases.

### Complete player upgrade freedom
Players have total freedom to invest in whichever tracks fit their personal playstyle:
- If a player wants to pour all their earnings into creating an absurdly overpowered machine cutter while keeping a starter backpack, they can.
- If a player prioritizes an enormous battery for deep endurance runs, or maxing bag slots first, the game fully supports it.
- No forced synchronization or locked track dependencies; all five tracks are independent, sequential, and additive.
- **Power outpaces resistance:** Returning to earlier layers visibly demonstrates overwhelming cutting power, while deeper ground introduces distinct material behavior without ever resetting the player's speed back to square one.
- **Final major tool timing:** The final major machine upgrade arrives around the last quarter of the campaign (about 75%), leaving substantial deep excavation to enjoy its full power.
- **Tuned against the zones:** each zone's main ground is balanced with the tool level a player typically owns on arrival, so no zone feels like a restart; the drill milestone lands around the rock zone ([zone rules](03_WORLD_AND_SITE.md#zone-rules)).

## 3. Money in

- Commons and repeatable distinctives sell for money; rare finds can pay for a major purchase.
 Uniques give display and story, without money or mechanical perks.
- **Uniques and ending parts consume zero bag slots.** Players are never forced to sacrifice income for the discoveries the game most wants them to appreciate.
- **Whole-object salvage payouts:** Crane-recovered oversized set pieces yield a once-only lump-sum payout at the surface computer after the crane sets them down at camp. Crane-recovered uniques remain unsellable exhibits; the recovery method does not change their purpose.
- A fixed price per item type: deeper zones contain richer types or mixes, but a gold bar always
 has the same price.
- Depth pays through the mix, not through a price bonus: each type keeps one fixed price, and the
  deeper roster is made of better types instead of the same junk at a premium.
- Rare finds excite without breaking the curve; a rare find should afford one big upgrade, not half
 the tree.
- Cluster hauls and saleable large finds provide occasional big paydays. Display completion has no
 cash reward; the collection and story are the payoff.

## 4. Money out (including late game)

Primary: the five tracks.

Late-game sinks support remaining discoveries and optional decoration after the tracks are maxed:

- extra C4 charges;
- reusable lamps (buy individually; useful for lighting and photography);
- yard decoration around the recovered uniques;
- cosmetic tool skins and yard items;
- spare C4 charges.

The site is finite. Once its discoveries and upgrades are complete, money may stop mattering. No
extra upkeep or repeated chores are added just to sustain spending.

## 5. Fuel (shared battery) and recovery

- One battery powers **digging and jetpack**.
- Drain occurs only during powered actions; reading, standing, thinking and inspecting never drain.
- **Surface recharging is free and automatic:** back at camp the battery refills by itself at a
  visible charging point: no button, no price, no menu. Bigger tanks fill completely.
  - *Why:* a paid refill is a tax with no decision in it; the player always pays. It also punishes
    battery upgrades, because a bigger battery costs more to refill; Keep Digging reviewers said they
    felt "punished for increasing your battery". Meltopia's free refuelling drew no complaints. The
    battery's job is to limit how long one trip lasts and create the "go home or one more thing?"
    decision; money has its own sinks (upgrades, C4, cosmetics).
- **Return-power warning:** an adaptive indicator with safe / risky / critical states — never exact
 required-energy math. It serves as an estimate, accounting for depth and ascent cost.
- **Recovery as a supported service:** At zero fuel underground (or called intentionally from pause as an "I'm stuck" option), recovery returns the player to the surface with full fuel, **all finds kept**, and a depth-scaled fee. If broke, interest-free debt is applied; the next outing is never blocked. With free recharging, this fee is the only money cost tied to the battery, so pushing your luck still carries a price.
- **Crane self-rescue (to test):** instead of moving the player up automatically, the salvage crane parks over the player's hole and lowers its rope down it, hooks them and hauls them up the dug route, just as it hauls uniques; the player can look around and watch their hole go by, and the fee is paid on arrival. Keep whichever of the two feels better in play. Digging your own way out with a dead machine is rejected: at 150 m it would be long, punishing survival friction.
- Recovery never blocks progress, never deletes items, and never permanently ruins a save.

## 6. Capacity (the bag)

- **Generous starting capacity**: the first expedition must already feel good; upgrades improve a
 loop that works, they do not repair a miserable one.
- Capacity grows in strong steps; the HUD shows count/capacity continuously.
- **Hard stop when full:** you cannot pick up; the find stays in the world exactly where it is and
 can be retrieved later.
- **Nothing is ever deleted**: no overflow deletion, no inventory destruction, no loot loss on
 failure of any kind.
- Uniques and ending components never consume capacity. Full bags stop pickup, not digging or
 movement; nonblocking overflow remains in the world. No discarding.

## 7. Selling

- Selling happens at the surface computer shared with upgrades; there is no inventory screen.
- A carried haul opens the selling screen first. **One-button Sell All** banks the payout and
  immediately opens upgrades; selling the final individual item does the same.
- An empty bag opens upgrades directly. Closing the selling screen leaves the haul untouched.
- Clear money feedback accompanies the sale; there is no extra confirmation or animation wait.
- Individual selling available at the machine for players who want it.
- Money is banked instantly on sale; there is no bank/branch/ATM system. The haul animation is
 nonblocking; the player can move on and buy an upgrade immediately.

## 8. What the economy never does

- No RNG-gated progression (no blueprints replacing shops).
- No condition/grading system.
- No expiring coupons, time-limited offers or temporary boosts, including limited-use boosts.
- No found passive upgrades; all permanent mechanical power comes from the existing shop tracks.
- No multi-currency.
- No gambling, casino or betting mechanics.
- No dead end where everything is purchased halfway through the run.
- No loot deletion as a failure consequence.
- No item durability; tools never break.
- No paid battery refills; recharging at camp is free (recovery keeps its fee).
- No object whose sale value increases by combining, stacking or re-merging — value is fixed per
  type (value-stacking exploits cannot exist by construction).
- No paid power, no premium currency, no microtransactions.
- Fix duplicated value, repeated credit, purchase bypasses and premature ending triggers.
 Harmless physics comedy and free relocation of an owned lamp can remain; moving property is not
 itself an exploit.

## 9. Tuning targets (validated in prototype)

| Metric | Target |
|---|---|
| First purchase | affordable from the first sale; within the first minutes |
| Median time between milestone (capability) purchases | prototype-tuned across twelve levels; major capability milestones remain more widely spaced than ordinary purchases |
| Purchases affordable at any moment | ≥ 3 |
| Maxed tracks before credits | 50–85% of players (i.e., some left for Continue Playing) |
| Rare find value | ≈ one big upgrade, never several |
| Recovery fee | noticeable, never progress-blocking |
| Resources left at credits | enough for a few remaining sinks, never an absurd pile |
