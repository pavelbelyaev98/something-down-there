# 015 — Silent Visual Detector

> **Complete:** A separate silent HUD detector shows three aim-alignment signal levels for nearby buried finds and disappears when looking away or once any part is uncovered. Independent prototype computers share approved art while retaining physical recovery, separate receiving space and individual saved exhibits.

## Objective
Guide ordinary digging toward noteworthy buried finds with a passive, silent, readable hunch. Reuse the approved unique computer model for independent prototype finds while new slice objects and connected scenes move later in the queue.

## Concept reference
- Concept 05: eligibility is authored, never inferred from value, metal or size. Routine commons and intentionally silent finds give no signal. Three levels communicate alignment to the current view without direction labels, identity, rarity, prices or distance.
- A nearby buried find signals only within the aiming cone, strongest at direct aim. Small angular/range margins prevent flicker; no timed lock blocks turning toward another find. Any partial exposure ends that object's hint, with no revealed fallback; collected, recovering, stored and displayed objects are silent.
- The baseline detector is useful from the start with no action, fuel cost or required upgrade. Follow existing excavation, exposure, ownership and recovery rules.
- Concepts 08/14: a subtle visual cue remains readable without audio or colour alone. Reused computer art is temporary prototype content; each exhibit identity still exists once per save. Tool reaction is integrated when the visible tool rig is authored.

## Live code analysis
- `BuriedFind.DetectorEligible` already combines authored eligibility with the silent-common flag; no detector owner or cue exists. `DiscoveryField` owns the saved population and exposure, so the detector must derive its candidates from that owner rather than search scene objects each frame.
- `FpsPlayer` owns gameplay/menu/focus gates; `FpsHud` and `GameHudView` own the single UI Toolkit HUD. No rig exists yet. Reuse this HUD, keeping detector guidance independent of the crosshair element and its animation.
- The retro-computer importer currently accepts one source identity. The catalog and saves already support multiple unique identities, but the winch validates one display socket and routes every arrival to one pad.
- Current saves validate the complete unique population. New prototype identities therefore require New Game under the current-only save policy; do not add migration or silently append finds to an existing excavation.

## Architecture and changes
- `DetectorTargeting` measures aim alignment for the supplied buried candidates and returns the strongest matching source's signal level. Range and angular hysteresis suppress boundary flicker; equal bearings use distance then stable identity. Looking away clears the reading immediately.
- `FindDetector`, owned by `FpsPlayer`, caches authored signaling finds by a population revision in `DiscoveryField`. Each frame evaluates current pose/exposure only for those sources. Any exposure or recorded visible discovery excludes the find; no extra density queries or common-population scans occur. Pause selection with gameplay and reset derived state on population replacement/load.
- Put baseline range and lock tuning in `EquipmentProgression`, leaving purchases and tier redesign to their queued task. Detector state is transient and adds no save fields.
- Use the existing UI Toolkit HUD for a separate compact panel with three signal bars driven by aim alignment. Hide it with no aimed buried source and during menus, loading and focus loss. Keep the aiming reticle independent; no direction/height text, arrows, target markers, flashing or audio.
- Extend the existing computer source catalog/importer to author independent unique entries sharing meshes/materials. Source JSON owns placement and identity; preserve strict unique validation and authored clearance reservations.
- Extend existing winch setup to provide receiving pads and unique display sockets for the prototype population. Select a free receiving pad for each haul, keep saved routes/poses authoritative, and validate all authored sockets on reload. Do not create another recovery system or auto-place exhibits.
- Move 012 and 013 after the early mechanics and larger-load work, ahead of the slice acceptance gate. Their object-recognition and connected-world goals remain intact.

## Edge cases
- Equal bearings/distances, tiny movement near angular/range thresholds, looking behind or vertically, no candidates, intentional silent content, partial exposure and moving/released finds.
- Full bag and empty battery do not disable guidance. Partial reveal, marking/extraction and collection remove candidates. Pausing stops updates; loading cannot retain stale references or restore a partially revealed object's signal.
- Different screen aspect ratios, crosshair hidden, existing prompts/resource warnings and placement overlays remain usable. No material/lighting changes.
- Every prototype computer retains its identity, physical recovery, unsellable state and individual display/socket across saves; multiple stored finds cannot occupy the same arrival space.

## Acceptance and verification
- A new game stays quiet across the surface. Within close range, sweeping toward a buried computer raises the bars through three levels; direct aim is strongest, looking away switches off. A small actual cut silences it before marking is possible.
- Tests cover aim selection in both axes, stable ties, angular/range hysteresis, immediate reveal cutoff, authored eligibility, ownership transitions, population replacement and save/load behavior.
- Exercise actual HUD rendering across all three levels, looking away, partial reveal and menus. Verify the separate panel remains silent and gives no direction/height instructions or find identity.
- Validate new authored placements and all pad/display references, test repeated recoveries and reload, and record bounded detector update cost against the dense population.
- Compile cleanly, run relevant checks, publish a fresh Windows build, then archive the spec and update baseline/architecture/queue.

## Initial verification
- Passed the EditMode assembly (271), detector integration (2), existing physical recovery integration (7), save integration (14) and startup integration (8). One save test interrupted by a diagnostic CLI timeout passed when rerun without inspections.
- Live HUD review covered facing/behind/below guidance, a rendered above-direction fixture, stronger underground cues with the reticle hidden, menu suppression and free-aspect/16:10 layouts. No new audio or lighting changes.
- Steady detector sampling averaged 0.0007 ms/update in the full authored population. Population scans occur only when the cache is rebuilt; ordinary samples visit signaling sources only.
- Windows build succeeded with no errors and no C# warnings; the existing Pipeline notice reports that its development runtime connection is disabled in players. New Game is required for the current prototype population; no save migration was introduced.

## Playtest iteration: separate instrument and nearby activation
- Rejected the reticle rings, floating edge hints and long-range surface guidance. The detector now occupies its own compact HUD panel and stays completely hidden until close to an eligible discovery; tuning remains in `EquipmentProgression`.
- Existing target stability, exposure priority and authored eligibility remain. Regression coverage checks quiet surface exploration, close activation, stronger proximity, retreat shutoff and population replacement; this presentation/range update needs no new save or population reset.
- Verified all six targeting tests and both detector integration tests. Live review confirms quiet surface exploration, the separate panel near a find, retreat shutoff, menu suppression and free-aspect/16:10 layouts. A fresh Windows build succeeds with no errors or C# warnings; only the existing development Pipeline notice remains.

## Playtest iteration: aim-sensitive signal and reveal cutoff
- Replace the arrow, direction/height labels and distance strength with three HUD bars driven by alignment to the current view. Keep the close-range gate and independent reticle; no new input, sound, item information or save fields.
- Refactor `DetectorTargeting` to choose the best aligned eligible source on each view update, resolving equal bearings by distance then stable identity. Remove timed locks and exposed-target fallback. Small angular/range hysteresis prevents flicker without delaying a deliberate change of aim; all tuning stays in `EquipmentProgression`.
- `FindDetector` continues caching authored sources by population revision. Evaluate only that small cache each frame, never scan commons or perform extra density/raycast work. Current exposure and recorded visible discovery exclude partially revealed finds immediately; terrain and existing discovery state preserve that exclusion after reload.
- `GameHudView` drives the retained panel and its three bars directly. Remove the superseded custom arrow element, direction text and associated styles.
- Acceptance: all three levels at fixed distance while sweeping horizontally/vertically; strongest at direct aim regardless of distance within range; no signal behind the player or beyond range; no off-axis target lock; no revealed fallback after a small real terrain cut or reload. Full bag, empty fuel, pause, authored silence, population replacement and bounded update cost remain covered.
- Verified 15 targeting checks and both detector integration tests, including a small real terrain cut and subsequent reload. Live HUD review confirms all three levels, looking-away/menu suppression and immediate disappearance with only a small part exposed. The fresh Windows build succeeds without errors or C# warnings; the existing development Pipeline notice remains. Existing current-format saves need no reset.
