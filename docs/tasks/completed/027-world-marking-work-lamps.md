# 027 — World Marking & Placeable Work Lamps

Reusable diffuse lanterns illuminate in every direction with local occlusion, attach to solid scenery or fall freely, and save with the world. Free arrow, home and return-here markings conform to worked surfaces; placement, rotation, retrieval and erasing use rebindable controls.

## Objective
Make covered excavation readable with reusable work lights and recognizable route marks, available from the beginning of the playtest. Complete the lamp/marking task without coupling it to material response work; task 007 remains independent and pending.

## Concept reference
- Concept 03: no personal lamp or map. Sky fades along excavated routes; placed lamps provide underground visibility without battery drain or expiry. Digging removes support, never owned lamps. Free arrow, home and return-here marks communicate by shape.
- Concept 04: auxiliary placement uses the existing tool/input owner, with no digging-mode changes. All actions are rebindable. Finds and equipment survive terrain removal; visual debris never becomes a hazard.

## Live code analysis
- `TerrainVolume.Changed` already reports edited bounds after matching density/render/collision commits. Use it for support changes, including winch cuts and future explosions, without scanning the density field every frame.
- `ExcavationDaylight` already preserves URP additional lights underground. Add bounded shadowed work lights, leaving daylight ownership intact; essential local occlusion must survive the sun-shadow quality setting.
- `FpsPlayer` owns target rays, input gating, menus and interaction priority. `FpsInput` and `InputPreferences` own rebindable actions; `GameHudView` can display equipment availability and placement instructions without another menu.
- `WorldSaveController` captures one atomic world snapshot. Equipment poses, attachment state and marks belong to that checkpoint; no separate equipment save or old-format reader.
- No existing lamp equipment system or production marking system exists. Original portable-light and symbol geometry will be authored through Blender MCP, with source recipe and local asset card.

## Architecture and implementation
- Add `WorksiteTools` under Interaction, connected to the existing player and terrain. It owns the reusable kit, bounded world lists, placement preview/validation, rotations, support notifications and snapshot restore/capture. Numerical tuning stays in code, not this document.
- Add `WorkLamp` for a placed lamp's interaction and physical body. Anchored placement is stable on floor/wall/ceiling; removing support releases a dynamic, recoverable body. Physics pauses with gameplay. Pickups return the same kit entitlement without bag slots, sale value or charges.
- Add a small `WorldMark` representation with surface-conforming geometry. Aim and interact to erase; excavation of its supporting surface removes the mark. It has no solid collider to interfere with cutting or movement.
- Placement uses explicit valid/invalid feedback, a non-lighting ghost and bounded clearance checks. Mark geometry projects onto real world surfaces and never renders through soil. Cancellation, menus, focus loss, rescue and load leave no armed placement or held-action leakage.
- Add lamp, marking and rotation actions to the current input schema. Reuse primary/secondary actions for commit/cancel only during placement. Ordinary digging, unique tagging and find carrying retain priority outside placement.
- Add bounded, validated equipment records to the current save codec and revision tracking. Bump the format, with New Game for older development saves under repository policy. Validate poses, enum ranges, counts, identities and attachment data before restore.
- Add Editor setup for original model imports, materials/prefab and MainGame references. Reuse existing URP and lighting shaders, with a finite local-light budget and no new rendering package.

## Edge cases
- Invalid/overlapping placement never consumes equipment; a full kit leaves existing lamps recoverable. Held/toggle digging cannot accidentally cut behind a preview.
- Terrain support is checked only for affected bounds; static lamps sleep. Fallen lamps keep their light and remain reusable. Resetting terrain returns embedded lamps to the kit and clears invalid marks.
- Save/load while a lamp falls preserves its pose and velocity. Paused physics and placement do not advance. A copied checkpoint cannot duplicate kit ownership or exceed the world-mark bound.
- Marks on chunk boundaries project against the world, not an obsolete chunk mesh. Deleted support removes marks without touching discoveries.
- Lighting must reveal both ground and finds, respect occluding ground, and remain bounded when several lamps overlap. No shadow setting may make sealed ground transparent to lamp light.

## Acceptance criteria
- In a covered, dark playable excavation, a placed lamp illuminates actual terrain and finds; moving/retrieving it changes the light. A ghost provides no usable light.
- Floor/wall placement, invalid placement, rotate, cancel, retrieve and repeated reuse work with visible prompts and rebound inputs. Unsupported lamps fall and can be picked up after further digging.
- Arrow, home and return-here marks are distinct by shape, rotatable, removable and persisted. No map is introduced.
- Current-format save round-trip restores kit ownership, marks, physical motion and terrain together; corruption/duplicate validation remains strict. New Game produces the starter kit.
- Relevant core/input/save and integration tests pass, scripts compile cleanly, live visual checks cover dark ground and the HUD, and a fresh Windows player is built.

## Verification
- Edit-mode checks, corrected input-binding checks, save integration, UI/input integration, equipment physics/placement and daylight shader checks pass. Equipment checkpoint coverage includes motion, ownership, all marking shapes and props covering saved paint.
- Live covered-room checks confirm illumination, readable stencils, a non-lighting preview, stable lamp poses and solid-wall occlusion with sun shadows disabled; removing the separating wall admits light.
- MainGame validation and Windows build succeed with no script warnings or errors. The existing optional Pipeline runtime-configuration warning remains; in-player developer automation is disabled.
- The save format is current-only; older development saves require New Game. Material IDs and tool auto-adaptation remain independent pending work.

## Lighting and placement playtest iteration
- Replace the harsh, directional work beam and front-facing art with a compact neutral lantern, soft point-light shadows and bounded near-field irradiance shared by ground and find shaders. The shadow atlas accommodates every kit light without shrinking its faces.
- Accept fixed scenery, walls and ceilings; lift the small base clear of rough patches. Open-air placement and moving props release a physical lamp instead of rejecting the placement or leaving it floating.
- Verification: placement, support loss, motion persistence and retrieval checks pass. Live close-up and room views preserve soil texture; the same point light is blocked by a separating wall and illuminates the next chamber when that wall is removed, even with sun shadows Off.
