# 008 — Multi-Material Chunk Meshing & Triplanar Shader

Saved soil, clay and rock deposits drive distinct world-projected textures with continuous chunk boundaries. Full bags preserve digging through common finds, and reusable lamps provide broader illumination with bounded close brightness and local shadows.

## Objective
Make saved soil, clay and rock deposits visibly distinct during ordinary excavation. Also address the requested full-bag digging interruption and widen the usable illumination from the existing reusable lamps.

## Concept reference
- Concept 03: deposits retain their material identity after cuts and reload; automatic tool response follows the same identity. Lamps illuminate excavated routes, while solid ground blocks light and deep unlit spaces remain dark.
- Concept 09: identify materials through texture relief as well as colour, keep dry ground rough, and retain the vivid surface grade and meadow transition. Collection must not interrupt cutting cadence. Reuse approved licensed art, without generated bitmap artwork.
- Concept 05: a full bag stops collection, never digging; excess finds remain physical, identifiable and recoverable.

## Live code analysis
- `ExcavationGrid` already owns immutable saved material samples alongside density. `TerrainChunkMesh` runs surface nets through a reusable Burst workspace but publishes no material attributes; its cache checks density only.
- `GroundTriplanar.shader` uses one shared soil texture set, world projections, a shallow turf collar, excavation daylight and X-ray opacity. Keep these systems and the existing mesh/collider ownership.
- `FpsPlayer.TryPrimaryAction` returns early on a collectible find with a full bag. `BuriedFind.TryGetCoveringSoil` handles partly buried finds only, so exposed colliders prevent reaching terrain behind them.
- Work lamps already use shadowed omnidirectional point lights and a bounded kit. Their short range and steep falloff leave too little usable space; simply raising intensity would restore the rejected close-range glare.

## Architecture and changes
- Copy material bytes only for each rebuilt chunk's existing sample halo. Reuse managed/native workspace buffers; never clone the full material field per cut.
- Derive clay/rock blend weights from the immutable lattice at each surface vertex; soil is the remaining weight. Publish through the third UV channel, whose absent/default zero means soil on existing rim/preview meshes. Adjacent chunks use identical world lattice samples.
- Include immutable material snapshot identity in the chunk cache so a restore with unchanged density still refreshes material attributes. Derived weights are rebuilt, not serialized; retain the current save format.
- Extend the existing ground shader with separate approved texture/normal sets, local tint/scale/relief and narrow blended boundaries. Skip absent layers with coherent branches, use continuous world-position gradients and share sampler states. Retain one material/submesh and existing shadow, normal and X-ray passes.
- Extend `GroundTextureSetup` to configure project-owned copies from the approved Pure Nature pack. Keep vendor files, global lighting and the surface turf unchanged.
- A full bag makes common find colliders transparent only to the digging query, within ordinary dig reach. Select the nearest remaining hit; walls, lamps, props and uniques still block it. Preserve fuel/cadence, keep every find, and resume normal pickup as soon as capacity returns. Reuse bounded raycast storage and reject overflow safely.
- Pair broader lamp range/intensity with a larger finite-source attenuation radius, limiting close brightness while raising useful distant illumination. Update the existing prefab through Editor APIs, retain soft shadows and kit limits, and extend distance culling to the wider lit area.

## Edge cases
- Chunk borders, surface/rim joins, three-way deposits, empty chunks, material-only restore and current-format reload must not leave seams or stale weights.
- Static unweighted terrain previews default to soil. The inactive original-soil comparison remains usable.
- Full bag while holding/toggling dig, shaving/scoops, insufficient charge, released finds, multiple overlapping common finds, intervening permanent geometry and regaining capacity retain their normal action rules.
- Lamps placed against walls or ceilings must not bleach nearby ground or leak through it; multiple lamps keep the existing bounded shadow budget. Surface grading must remain unchanged.

## Acceptance and verification
- Excavated soil, clay and rock show different texture patterns/relief and readable boundaries matching tool response, including after reload.
- Meaningful mesher tests cover uniform and blended weights, adjacent-chunk equality, material-only cache invalidation and deterministic restore. Record a bounded mesh/cut timing check.
- Play-mode regressions prove a full bag permits repeated charged, cadence-limited cuts through common finds, preserves their identities, blocks permanent obstacles and allows pickup when space returns.
- Inspect live material boundaries, meadow joins, X-ray and close/distant lamp illumination. Run relevant terrain, collection and worksite checks and shader compilation checks.
- Publish a fresh Windows player, update current-state documentation and archive this spec only after verification succeeds.

## Final decisions and verification
- Reuse the approved pack's soil set, fine gravel tinted as compacted clay, and tileable rock-detail colour/normal textures. Rock uses a neutral tint; no global lighting or grading assets change. Project copies retain vendor sources and use Git LFS.
- Use the third mesh UV channel because Unity's primitive preview already contains lightmap UVs in the second channel. Unweighted authored meshes default to soil without a special renderer flag.
- Core edit-mode checks and focused material checks pass, including normalised three-way weights, seam equality, current-format restore and material-only cache invalidation. An initial floating-point overshoot is clamped and normalised in the mesher.
- Full-bag, remapped-toggle, terrain, lamp lifecycle and shaving integration checks pass. The older toggle test now expects real cutting and its fuel cost while full; walls still stop the ray and freeing capacity permits immediate pickup.
- Live disposable MainGame review confirms distinct deposited layers, intact meadow colours/turf edges, transparent X-ray and readable ground next to a lamp. A sealed chamber has no daylight; its broad illumination vanishes behind a separating wall and returns when the wall is removed. Review holes and lamps are discarded.
- A bounded live cut sweep with finds disabled measured 95th-percentile terrain-edit CPU times of 1.57–1.68 ms across the three families, with a 2.88 ms maximum. This isolates excavation cost rather than claiming whole-game frame performance.
- MainGame validation and the fresh Windows build succeed with no compiler errors or warnings. The existing optional Pipeline runtime-configuration warning remains; it disables developer automation in the player, not gameplay.
