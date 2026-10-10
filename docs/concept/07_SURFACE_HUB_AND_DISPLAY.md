# 07 — Surface Hub and Display

## 1. The yard

A compact, authored worksite on the drained lakebed beside the dig area. Everything the player needs
between trips is within roughly ten seconds of the shaft mouth. No walking through buildings,
no doors, no menu mazes, no NPCs.

| Station | Function | Detail |
|---|---|---|
| **The shaft** | The hole itself, physically changing as the player digs | The player sees their own excavation from the rim; the landmark that never repeats |
| **Workshop** | The camp's building and the stash for the run's uniques | A corrugated-iron workshop beside the camp, its wide roller door open toward it; the surface computer stands inside (user, 2026-10-10: the workshop won over a canvas tent). Bag-sized uniques get their own spots inside, crane loads the yard beside it ([§5](#5-recovered-uniques-at-camp)) |
| **Surface computer** | Sell ordinary hauls, then buy sequential upgrades | One standing retro terminal inside the workshop, screen toward the door: selling first when carrying finds, immediate upgrades after the final sale, direct upgrades with an empty bag |
| **Salvage crane** | Recover whole objects marked underground | A small tower crane beside the camp swings over the hole and its cable turns smart: the hook rides its end straight down the hole and along the excavated route into the lifting eye the player bolted on; the crane reels the find in through dirt bottlenecks and carries it to camp. Uniques arrive muddy for optional cleaning, oversized salvage awaits cash-in. Being tested: the same rope hauls the player home at zero battery |
| **Charging point** | Battery refill | Free and automatic: the battery refills by itself while the player is back at camp; a visible cable and charge light, no button, no price ([why](06_PROGRESSION_AND_ECONOMY.md#5-fuel-shared-battery-and-recovery)) |
| **Recovered uniques** | Special unsellable exhibits | Bag-sized ones on their own spots in the workshop, crane loads in its yard; readable (name, depth, story) |
| **Cosmetics rack** | Tool skins and yard decorations | Purely visual; another late sink |

There are no characters, dialogue, or quest-givers. The story is told by the uniques standing at camp
and what the player digs up.

## 2. Diegetic learning (no tutorial)

Learning happens through the world, not a compulsory tutorial or popup chain.
Contextual action prompts and the optional controls reference are allowed:

- Stenciled signs and painted arrows: **SELL**, **UPGRADE**.
- The standing computer has a visible screen and keyboard for both selling and upgrades.
- A compact pause reference lists controls, and settings explain options.
- **First-session full loop:** an unguided newcomer must find, sell and buy without a wiki or video —
 the stenciled signs and the shared computer are the teaching tools, and the
 session ends with a completed loop and a reason to come back (see [Prototype validation](14_PROTOTYPE_PLAN.md#4-validation-metrics-playtest-gates)).
- First interactions work on the first try: stand at the machine, press the obvious button.
- **Every system explains itself where it is used:** signs and objects cover the stations,
 lamps and the detector. Players can learn the loop without a guide or a compulsory tutorial.

## 3. Selling at the computer

- One press processes everything sellable in the bag.
- With finds in the bag, interaction opens the selling table: one row per kind of find (how
 many, their value) and a single Sell All. After Sell All, the same open computer immediately
 shows upgrades and the updated balance.
- With nothing to sell, interaction opens upgrades directly. No automatic sale on approach
 or interaction, no second machine, and no waiting for a physical hopper animation.
- No manual depositing and no selling finds one at a time (user, 2026-10-10).
- Uniques and components are never sellable and are never at risk of being included.
- Closing the computer before selling preserves the haul.

## 4. Upgrades at the same computer

- Shows each implemented track with its current level out of ten and the next level; the tool row identifies the drill milestone.
- Every row: current stat/behavior → next, cost, and a one-line practical benefit.
- Locked future levels are visible with their unlock requirement, so there is always a next goal
 (the anti-dead-end rule).
- Purchases are sequential; the computer never lets you skip ahead.
- Buying produces a visible, immediate change on the machine in your hands — the core reward loop of
 the surface.

## 5. Recovered uniques at camp

The emotional record of the run: every unique the player brought home, gathered at the workshop.

- **A few special exhibits:** ordinary and repeatable valuables sell; no first-copy exceptions.
- **Inside the workshop (119):** each bag-sized unique has its own spot, on the racking, the workbench or the
 floor. While the player carries it, its spot glows (a chalk outline of its footprint); Interact there sets
 it down for good. One deliberate set-down, no free arranging; the HUD counts the uniques secured.
- **In the yard:** the crane sets each big unique down beside the workshop, lying as it landed, where it
 stays; a crystal trophy is set upright on its base. Recovery never sells either kind.
- Each exhibit shows **name and depth found**. Never a price, condition or rarity label.
- Recovered uniques support inspection and story rereading where they stand. The story line first
 appears when the unique is cleaned or recovered, whichever comes first.
- **Optional cleaning.** A unique arrives caked in mud. Holding dig on it at the yard sprays the mud
 off in big chunks: a few seconds, not careful brushing. Its real colours and details appear, and its
 story line with them. It is never required: it can stay muddy, be cleaned later or never, and there
 is no percentage, meter or grade. Only uniques get this (a few per game); ordinary finds never do.
  - *Feel:* the PowerWash moment. Mud slides off, a shape you recognised underground becomes an
    object you want to keep looking at.
  - *Why:* reviewers of all four reference games recommend them to PowerWash Simulator fans, so
    this satisfaction lands with this audience. It stays optional and quick because required cleaning
    is an archaeology chore ([Anti-Patterns](15_ANTI_PATTERNS.md#discovery-and-content)) and surface
    time must stay short; the developer's own Meltopia notes reject waiting at the surface.
- The collection grows only through play, one unique at a time.
- Recovering the whole exhibit collection feeds the 100% definition.

## 6. Yard progression (cosmetic only)

The worksite can visibly grow as milestones pass: more lamps, a shelter over the computer, a tarp over
the recovered uniques, small decorations bought with late-game money. Purely cosmetic, never functional
gates, and never a base-building system. After the ending, the media
wall appears: clippings, a radio and a small TV recording the discovery — the only station the
story adds to the yard. Reports are printed/captioned; radio/TV props add no voices, music or faces.
A clipping can show the actual yard and a discovery from this save. A few reusable lamp appearances
can personalize it too, without changing lighting effectiveness.

## 7. Rules

- No station may be farther than ~10 seconds from the shaft.
- No station requires a menu to *reach*; menus only appear when the player chooses to interact.
- Stations are machines, not people: no dialogue, no quest text, no relationship systems.
- Surface time stays short: the yard exists to sell, upgrade, and show off, then get out of the way.
