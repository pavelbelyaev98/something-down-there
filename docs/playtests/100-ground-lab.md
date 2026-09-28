# Playtest 100 — Ground Lab and every ground

**Build & start:** current build, title menu → **Ground Lab** (development build only). Unlimited
battery is on; Ctrl+Shift+1…9 picks tool levels 1-9, 0 the last level (12); aim at a bay to read it.
Nothing is saved; pause → Leave Ground Lab returns to the title. The normal game currently has one
plain ground (soil) everywhere; the lab keeps every ground for redesigning them one by one.

## Every ground and its effect
Exact numbers live in Developer admin → **Ground tuning** (table plus sliders; Print writes
`ground-tuning.txt`). Resistance = how small the bite gets (width, length, depth) plus a slightly
slower stroke; each stroke throws the ground's own chips and dust.

| Ground | Resistance | Effect |
|---|---|---|
| Soil | none (the reference) | Plain ground; never collapses |
| Gravel | about soil | Undercut a gravel ceiling and its section (up to 3 m) pours down with its finds |
| Backfill | softest, bigger bites than soil | Undercut it and it slumps (up to 2 m); the "someone dug here" tell |
| Clay | narrow, shallower bites | Never collapses |
| Pond clay | a little easier than clay | The basin / odd-spot tell; never collapses |
| Rock | small, shallow chips | Holds ore; cracks and veins are its tells |
| Shattered rock (crack band) | easier than rock | A cut into it breaks the band loose along the crack (further with a bigger tool) |
| Concrete | smallest chips, slowest stroke | Structures and sealed rooms |
| Shattered concrete | easier than concrete | Breaks loose along the crack |

## Try
- Dig every bay at level 1, level 7 (the drill) and level 12; retune any ground in Ground tuning.
- Back row: dig under the gravel layer (pour), undercut the backfill pit (slump), dig into a crack.

## Tell the agent
- Printed ground tuning you like, which grounds still feel alike, and slump/break sizes.
