# 007 — Material IDs & Tool Auto-Adaptation

**Complete:** Soil, clay and rock identities persist with the voxel field, and the existing tool automatically changes cut shape, penetration and cadence while retaining material differences through upgrades. Stable shaving footprints and committed material feedback extend the existing dig/save pipeline without changing winch removal or lighting.

## Objective

Ground changes the feel and shape of the cut without a player-selected tool mode. Loose soil yields broad rounded cuts, clay produces narrower smooth shavings, and rock yields smaller faceted chips with greater resistance. The starter tool can excavate every family; upgrades improve output without erasing their differences.

## Concept reference

- [03 — World & Site](../../concept/03_WORLD_AND_SITE.md): density remains the ground authority; materials differ in behavior as well as presentation. Local cleanup must remove stranded remnants without collapsing useful tunnels/ledges. Finds survive and visible terrain matches collision. Material generation is stable within a save; permanent boundaries remain separate.
- [04 — Tool & Movement](../../concept/04_TOOL_AND_MOVEMENT.md): one powered tool automatically adapts to contact material. Hold/toggle shaving remains the default; all tiers work on all diggable materials, with power improving familiar-ground output. Preserve the developer-only scoop comparison.
- This task supplies the first three response groups and sound hooks. Multi-material textures/vertex weights follow in `008`; gravel/concrete in `074`, zones in `006`, audio in `028`, and the four-tier ladder in `087`.

## Live code analysis

- `ExcavationGrid` owns paged signed density and local cleanup. Brushes already share one bounded sample loop; sweep removal serves contact-gated winch recovery. It currently has no material identity.
- `TerrainVolume.TryCut` validates the live mesh hit, refines shallow contact against density, edits the grid and publishes matching meshes/colliders synchronously. Its reusable meshing/exposure workspaces stay intact.
- `FpsPlayer.TryDig` performs target resolution and fuel transactions; scheduled held cuts preserve pickup cadence. Material cadence must be selected from this actual hit, including soil covering an aimed find.
- `EquipmentProgression` owns shared tuning. `ShovelProfile` still owns the current tool ladder; changing its tier count is outside this task.
- `GridSnapshot` and `WorldSaveCodec` persist density in one whole-world checkpoint. Copy-on-write snapshots avoid a full main-thread density copy. Current-format validation and corruption recovery must continue.

## Architecture and changes

### Material data

- Add a compact `TerrainMaterialId` enum and immutable `TerrainMaterialSnapshot` alongside the density implementation. One byte per sample shares the density indexing scheme; occupancy still comes solely from density. Excavation never rewrites identity, including in removed air.
- Generate shallow, undulating soil/clay/rock deposits deterministically from the excavation seed when the live terrain starts. Use simple precomputed column variation and bounded arithmetic per sample, without Unity's global random state or additional GameObjects. This is material distribution, not the later authored zone system.
- Grid fixtures can create homogeneous material fields. Captures and restores share the immutable field safely with save workers; changing a fixture field uses a new validated snapshot, never mutation of a captured array.
- Expose clamped grid/world material sampling for tool response and later rendering. Resolve contact slightly inside the solid side of the refined surface.

### Cutting and player response

- Put material footprint, penetration and cadence tuning in `EquipmentProgression`, separate from the tier ladder. Select the actual sample's response inside the existing brush loop, so aiming at soil cannot cut neighboring rock at soil strength.
- Add adaptive options to existing shave/scoop methods. Retain geometric removal for recovery, fixture authoring and future blasts; do not change the accepted winch contact/pull behavior.
- Loose soil retains the broad rounded baseline; clay uses a smooth elongated footprint; rock uses clipped planar faces. Keep adaptive shaving orientation stable across strokes so repeated contact does not average every footprint into a circular bore. Preserve sub-cell shaving and existing cleanup. No extra terrain meshes or per-stroke debris objects.
- `TerrainVolume` provides the contact material and emits a typed cut notification only after a successful committed tool edit. Include contact point/normal, material and removed volume for later sound/feedback consumers. No new audio assets or fake sound implementation.
- `FpsPlayer` resolves material before fuel validation, applies its cadence to the next scheduled stroke, and charges proportionally to that cadence. Failed or stale cuts consume no fuel or feedback. One held-input frame still performs at most one cut; collection adds no delay. Default UI tool comparisons remain a soil reference.

### Persistence and performance

- Extend the current save format with exact material bytes alongside density; bump the current version and remove any old-version assumption. No migration or regeneration on load. A new game is required for older development checkpoints.
- Validate sample dimensions/count before large allocations and reject unknown IDs, truncation, checksum errors or count mismatches. Bound combined density/material allocation within the supported payload budget.
- Immutable material capture is constant-time and allocation-free. Per-cut material reads are constant-time within the existing brush bounds; no world scan, material cloning or extra collider rebuild. Replace allocation-based terrain configuration validation with shared dimension validation.

## Edge cases

- Oblique contacts and chunk boundaries use the same field/indexing; ghost samples clamp consistently. Empty space has no independent material authority.
- Mixed deposits preserve per-sample resistance. IDs stay stable after cleanup, reset, carving and reload; loading must not regenerate from a possibly changed generator.
- Starter and upgraded cuts all advance in rock. Radius/cadence upgrades cannot reduce output on the same homogeneous material.
- Small detached hard fragments may clear through the existing bounded cleanup; this is local support cleanup, not a material-mode bypass or structural collapse.
- Recovery sweep, find exposure/pickup, lamp support and X-ray keep using density. Lighting and graphics defaults stay under their existing owners.

## Acceptance criteria

1. Live MainGame contains reachable soil, clay and rock; the same held input automatically changes response. Families leave visibly different footprints without requiring new textures.
2. Deterministic generation, material capture immutability, exact save roundtrip and next-cut equivalence pass automated checks. Invalid IDs/counts and truncated current-format checkpoints fail safely.
3. Equal tool tiers exhibit ordered removal rates and distinct cut shapes; every tier excavates each material and higher tiers improve familiar-ground output. A mixed-boundary check proves harder samples keep their own response.
4. Player integration verifies actual contact selection, cadence/fuel accounting, successful-only notifications, stale-hit rejection and synchronous collider updates.
5. Existing terrain/grid, save and relevant recovery/equipment checks pass. Live review covers the three cuts and existing player interaction; profiling confirms bounded cutting and no material snapshot copy on the main thread.
6. C# compiles warning-free, current content validation passes, and a fresh Windows player is built. Update baseline/architecture, archive this spec and advance the queue to `008`.

## Validation and review decisions

- Full EditMode suite passed; final focused material and site-budget checks also passed. PlayMode terrain, shaving, whole-world saves, unique recovery, worksite equipment and find physics/collection checks passed. C# compilation has no warnings or errors.
- MainGame review exercised repeated player cuts in all three families with matching collision and proportional fuel. The initial randomized shaving orientation gradually rounded off every footprint; retain stable contact-frame orientation for adaptive shaving, with organic variation only in the scoop comparison. A sustained-cut contour regression covers that distinction.
- In the controlled Editor review (finds disabled to isolate terrain), full cut p95 was approximately 1.76 ms for soil, 1.92 ms for clay and 1.79 ms for rock; the largest observed cut was 5.60 ms. Material snapshots shared the same immutable field and full-site capture took about 0.02 ms. These are focused measurements, not the later full-frame or long-session acceptance gate.
- The fresh Windows development player built successfully with content validation and zero errors. Its only build warning is the existing optional Pipeline runtime configuration warning; runtime automation is disabled in the player.
- The shared ground texture remains intentional until `008`; material audio and debris presentation remain in their own queued tasks. Older development saves require New Game because material identities are part of the current checkpoint format.
