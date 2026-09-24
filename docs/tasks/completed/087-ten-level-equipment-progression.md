# 087 — Ten-Level Equipment Progression

Tool, backpack and fuel capacity share ten sequential levels and one price ladder. The tool automatically advances from shovel scoops to continuous drilling at level seven; the detector remains fixed starter equipment.

## Objective

Implement the user's revised progression: every purchasable equipment track has ten sequential levels. Start with ordinary shovel scoops, then automatically adopt drill-like shaving at tool level seven while preserving earlier investment and held/toggle input.

## Concept reference

- **04 §§1–4:** one evolving tool, no tool swapping or mode chores; hold/toggle repeats digging, all materials remain diggable, and upgrades improve familiar ground. The user's plain-shovel start replaces the old motorized baseline. The visible evolving rig remains task 001.
- **06 §§1–2, 5–6:** independent sequential tracks, shared tier prices, transparent current/next effects, immediate purchases, preserved fuel when capacity grows, and preserved bag contents. Ten levels replace the old flexible/four-tier direction.
- **Detector decision:** keep the existing silent detector as fixed starter equipment and remove its paid track from concept 06. Current eligible content is the authored computer uniques; selling repeated range increases against this sparse roster would imply unproven value. Its aim cues and eligibility remain intact.
- Jetpack mechanics remain task 022, C4 implementation/pricing remains 026; both follow the ten-level convention when introduced. No placeholder purchases.

## Live code analysis

- `ShovelProfile.Defaults` currently defines six tool levels; `EquipmentProgression` defines five capacity levels and separate increments. `FpsPlayer` consumes both and always shaves unless a developer comparison overrides it.
- `StationTrade` binds quotes to state revisions but injects a separate tool-price copy. `GameMenuView` draws level pips, and its max-capacity headline still indexes a nonexistent next increment.
- `WorldSnapshot` hardcodes the tool ceiling. The current codec already persists owned levels and actual capacities; extending valid levels needs no binary layout change or legacy reader.
- The existing scoop/shave terrain kernels share resistance, cleanup, synchronous render/collision publication and find notifications. Reuse both rather than adding another excavation system.
- `FpsInput` has six developer shortcuts; admin tuning iterates the tool ladder. No first-person tool mesh exists.

## Architecture and changes

1. Make `EquipmentProgression` the sole author of the ten tool profiles, capacity increments, shared price ladder and drill threshold. Keep tool state/profile responsibilities and validation in `ShovelState`; remove the superseded profile table and duplicate injected pricing.
2. Derive normal cut motion from the effective tool level. Shop previews derive each displayed level's own motion/cadence. Preserve a session-only opposite-motion comparison, reset it on restore, and reset held input/timing when changing admin levels/motion.
3. Keep all three current shop tracks independent and sequential through their final level. Preserve inventory identities, charge, stale-offer rejection and no-charge-at-max behavior. Show level/count and the shovel-to-drill milestone in the existing row layout.
4. Expand save validation to the shared ceiling. Test both transition and final-level round trips without adding save migrations.
5. Expand admin selection/shortcuts to the full ladder and update the concept, baseline, architecture and developer guide in place. Retain the protected recovery scene.

## Edge cases

- Final-level rows must render without indexing a next profile/increment or spending money.
- Buying the drill must increase measured output, including clay and rock; frequent smaller cuts must not make the milestone a downgrade. Measure sustained cuts as well as fresh-ground output.
- Owned level, developer-selected level and motion override must not contaminate one another or saves. Restoring derives motion from the saved owned level.
- Full bags and collection preserve the cutting deadline in both motions. Air, blockers, stale contact and insufficient fuel remain non-destructive.
- Capacity upgrades preserve absolute current charge and bag records; every track can reach its cap while the others remain at their starter level.

## Acceptance criteria

- New sessions use shovel scoops; levels before the milestone stay scoops, and the milestone onward automatically uses drill cuts with the same hold/toggle controls.
- Every current shop item reaches level ten through nine paid sequential purchases on the same price ladder; the terminal clearly communicates progress, benefits and the drill transition.
- Every tool purchase improves familiar-ground throughput for every supported material, including across the motion transition; collision and cleanup match the resulting cuts.
- Current-format saves round-trip high levels and restore the correct motion; invalid out-of-range levels are rejected. Developer overrides remain transient.
- Relevant core progression, trade, terrain, pickup, input and persistence tests pass; scripts compile without warnings.
- Review live MainGame behavior and shop rendering, then produce a fresh `builds/windows/SomethingDownThere.exe` through the approved build command. Archive this spec and remove only the completed queue entry.

## Validation and remaining scope

- Core checks pass for independent sequential purchases, shared pricing, preserved charge/items, repeated/stale-quote refusal, final-level limits and current-format high-level save round trips. Invalid levels are rejected.
- Fresh and sustained excavation output increases at every level for soil, clay and rock, including the shovel-to-drill transition. The existing collision, terrain cleanup, pickup, full-bag, hold/toggle, input, rescue, menu and persistence checks pass.
- Developer shortcuts cover the entire ladder; changing levels resets digging input while preserving held jetpack thrust. Restoring a checkpoint restores the owned motion and clears the comparison override.
- Disposable MainGame review verifies all tool levels, the actual drill purchase and all three maxed shop rows. Captures and short cut-timing evidence remain under ignored `unity/Logs/`.
- Scripts compile without warnings. The fresh Windows development build succeeds through the normal pipeline and validation profile; its only build warning is the existing disabled Pipeline runtime configuration, unrelated to gameplay.
- The former shallow-content check now funds the shovel phase and requires deeper income to finish the track; the full site funds the current tracks and refills. Full-run purchase cadence remains with the slice/economy gates. The visible tool rig remains task 001; jetpack and C4 mechanics remain with their existing tasks.
