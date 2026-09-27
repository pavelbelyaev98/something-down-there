# 15 — Anti-Patterns (never ship these)

Design boundaries that protect the digging, discovery and progression loop. Related chapters explain
the intended behavior in detail.

## Progression and economy

See [Progression and Economy](06_PROGRESSION_AND_ECONOMY.md).

| Never | Rule here |
|---|---|
| Let players max everything halfway and make money pointless | Meaningful upgrades through the campaign, useful remaining play after the final purchase; a fully completed finite economy may end |
| Introduce a new tool that invalidates old upgrades | One machine, shared upgrade track |
| Sell invisible stat upgrades | Fewer stronger steps; practical benefit on the next outing, not cosmetic feedback alone |
| Gate progress behind RNG, blueprints, licenses or rare drops | Money-only, sequential, deterministic |
| Make rare finds buy half the tree | Rare = several expeditions, never half the tree |
| Force repeat trips or grinds for the finale | Discoverable component trails; owned parts insert without retrieval trips; no grind wall |
| Irreversible build choices | No permanent branches; all purchases additive |
| Gate progression behind a minigame or non-digging activity | Every zone stays diggable with current equipment; money buys power and convenience, never a compulsory depth gate |
| Ship an upgrade that is bigger but slower in the ground it replaces | Every tier improves familiar-ground output; power outpaces new ground resistance and material responses stay distinct |
| Add gambling, casino or betting mechanics | No gambling; money sinks are explicit purchases with visible value |
| Wall off depth and force in-area grinding | No mandatory depth/tool-power walls, even when labeled clearly; ground places always yield visible progress to the current tool and every rock body has a soft path (One Man's Trash's tier walls) |
| Make a new zone or material feel like a restart | At the expected tool level each zone's main ground digs no slower than the last felt; no material needs a new tool (Meltopia's blue snow and Tesla gun) |

## Pressure and failure

See [Fuel and recovery](06_PROGRESSION_AND_ECONOMY.md#5-fuel-shared-battery).

| Never | Rule here |
|---|---|
| Delete carried loot on failure | Recovery keeps everything; fee + debt only |
| Add stamina/commute mechanics that interrupt digging | Battery is the only pressure; no commuting design |
| Punish falls with health/chip damage | Harmless landing feedback; no battery loss, input lock or ordinary-fall recovery |
| Add hunger, oxygen, warmth, food, overheating or other upkeep meters | No additional upkeep meters |
| Charge for battery refills | Recharging at camp is free; a price that grows with battery size punishes upgrading (Keep Digging) |
| Make the ground itself dangerous | Gravel pours and C4 never harm, bury or trap the player; no cave-ins |
| Make darkness a hazard or horror | Unlit ground is near-black, but lamps are always available and nothing in the dark can hurt the player |
| Drain battery for looking, reading or standing still | Drain only on powered actions |
| Add a day/night cycle, forced rest or time-gated content | The battery and the player's greed are the only pressure; the clock never gates digging or content |

## Discovery and content

See [Discoveries](05_DISCOVERIES.md) and [World and Site](03_WORLD_AND_SITE.md).

| Never | Rule here |
|---|---|
| Turn the detector into a value radar | No value/rarity hints, ever |
| Make ground tells a treasure radar | Tells show presence, never value; some cracks and channels fade out |
| Generate caves, tunnel networks or identical corridors | Every passage is player-dug; only small sealed rooms exist, and the player breaks in (Meltopia's getting-lost complaint) |
| Make C4 weak against a common ground | C4 is strong everywhere and stronger on cracks (Keep Digging's "useless" dynamite, Meltopia's dirt nerf) |
| Leave floating specks that snag movement | Plain crumbs vanish; valuables collect only with space, overflow persists nonblocking; interesting objects remain |
| Require archaeology chores (brushing, 100% cleaning, analysis timers) | Recognition without bureaucracy; cleaning a unique at the yard is optional, quick and has no meter |
| Ship a game where the trailer is the whole game | Four zones, escalating silhouettes, density rules |
| Clump all novelty early / empty late game | Validated pacing, late silhouettes |
| Depth-only value with nothing sideways | Fixed price per type; deeper zones can contain richer types; coherent scenes and connected finds reward sideways digging |
| Unbreakable decoy obstacles, or solutions the player cannot discover | Every boundary looks different; every pocket has solutions, and at least one is visibly telegraphed |

## Tone and honesty

See [Fantasy and Tone](01_FANTASY_AND_TONE.md).

| Never | Rule here |
|---|---|
| Unadvertised horror or scary content | No horror or scary content |
| Bait-and-switch ending or genre shift | Normal systems at the finale; honest ending |
| Disable the player's tools for drama | Tools always work |
| Imply something conscious in the impossible materials | A small material signature only — clean cut, resonance, dust, with a visual counterpart; never a response, direction or value cue |
| Toilet/body-sound humor or joke spam | Deadpan object humor, rare, no sound gags |
| NPCs, dialogue trees, or quest-givers | Machines and finds only |
| Fake difficulty through controls getting worse | Jetpack only improves; control quality never degrades |
| Real-world politics, partisan slogans, or living controversies | Fictional setting; original deadpan parody only |

## Interface and body

See [Interface and Controls](08_INTERFACE_AND_CONTROLS.md) and [Accessibility and Comfort](10_ACCESSIBILITY_AND_COMFORT.md).

| Never | Rule here |
|---|---|
| Click-per-dig, no hold-to-dig | Hold-to-dig from minute one |
| Fixed keybinds or no rebinding | Full rebinding everywhere |
| FOV/shake/bob without controls | Comfort suite with safe defaults |
| Complete toggle that doesn't remove the content | Every toggle fully removes its content |
| Settings that don't persist; ESC that doesn't close menus | Immediate persistence; correct back behavior |
| Color-only information | Shape + label redundancy |
| Audio-only clues in a music-less game | Visual counterparts for every cue |
| A permanent tutorial or popup spam | No compulsory tutorial; world labels, contextual action prompts and optional controls reference |
| Land an interaction anywhere but where the player aims | Results land at the aim point or at a clearly previewed valid surface |
| Surface achievements as in-game checklists or task lists | Achievements live on Steam, never as HUD chores |

## Saves and trust

See [Save system](08_INTERFACE_AND_CONTROLS.md#7-save-system-player-facing) and [Completion](12_ACHIEVEMENTS_AND_COMPLETION.md).

| Never | Rule here |
|---|---|
| Lose the hole, inventory or display on load | Full state persistence |
| Delete or corrupt saves; no backups | Autosave + manual slots + independent rolling backup generations with validated fallback |
| Make balance patches reroll an existing world | Population persists; no forced rerolls |
| Lock achievements behind NG+, RNG or bugs | All required content in every seed; fair one-run Steam achievements separate from current-save completion |
| Delete or invalidate the display / collection | Special exhibits persist; any compatible display arrangement counts |
| Trigger the finale purely via depth or void boundary volumes | Finale requires intentional physical insertion of components |
| Block the main thread during save serialization or freeze the game on save | Nonblocking saves preserve input and dig rhythm; frame-impact and total background-save duration are different measurements |

## The one-line version

> Keep the hole, keep the loot, keep the tool, keep the wonder. Never take anything away that the
> player earned, and never surprise them with a different game.
