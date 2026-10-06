# 108 — Rubble Backfill

**Status:** planned after `107`. Spec written ahead (2026-10-05); re-check against the code when it
starts. Plan and research: [107](completed/107-asset-only-grounds.md).

## Objective

Backfill becomes **rubble fill**: a pit refilled with broken stone and rubble is stonier than the soil
around it. The player sees the stones in the wall and the tool takes small bites. Its dig rate is
**about half soil's** (user, 2026-10-05), so following a pit is a toll the player chooses, felt at
once, never a wall.

## Concept reference

- **`03` §4, disturbed ground:**
  - only what was buried on purpose leaves a pit (stash or rubbish), and every pit holds something;
  - it reads by grain, not colour alone;
  - the first pit lies a few metres under the plot centre.
- **This ticket changes the backfill tell** from "the tool sinks in faster" to "stony and hard". The
  tell rule from `107` ("different ground means you are on to something") already covers it.
- **`03` §4 hardness:** hardness shows mostly as bite size and a little as rhythm; strokes throw no
  particles.
- **`02` §4:** each zone introduces its tell gently, near the main shaft, and pays off. The first
  stash keeps doing that.

## Live codebase analysis

- **The response:**
  - `EquipmentProgression.Backfill = MaterialToolResponse(1.167, 1.109, 1.313, .92)`: Width, Length,
    Penetration, Interval, relative to soil's `(1, 1, 1, 1)`.
  - Interval scales stroke time and energy (`FpsPlayer.PrepareDig` 767-770).
  - Width, Length and Penetration shape the shovel scoop (`ExcavationGrid.RemoveBrush` 488-521, soil
    and backfill share the superellipse side).
  - Penetration also sets the drill's advance (`TerrainVolume.TryCut` 286-290).
- **`ToolRigPresenter`:** `sink` (43, 113, 154) pushes the tool 1.5× deeper in backfill. This is the
  "sinks in" presentation.
- **Texture:**
  - `art/pure-nature-highlands/make_backfill.py` builds `Content/GroundTextures/Backfill_*` from
    Mountains Mud01 (tiled 2×2) and Highlands Mud_rubble.
  - Stones come from threshold masks on the rubble's grey (`>140` all stones, `>145` churned stones).
  - Other settings: `dirt_shade .84`, `rubble_mud_share .35`, `stone_dirt .12`.
  - `GroundTextureSetup` binds it at tile 7 m with normal strength 1.
- **Texts:** `GroundEffect`, the Ground Lab bay hints and the `106` playtest note still say "sinks in".

## Design

1. **Response:** smaller, shallower bites and a slightly slower stroke. Volume per second is about
   half soil's at every tool level, shovel and drill. Values live in `EquipmentProgression`; tune in
   the Ground Lab with the admin ground dials, then write them back.
2. **`HardnessOrder`** becomes `{ Soil, Backfill }`.
3. **Presentation:** `sink` goes. A backfill stroke is presented like any harder ground; the smaller
   bite carries it.
4. **Look:** more and clearer stones, so the eye expects the hardness.
   - Lower the stone thresholds so more of the rubble's stones survive.
   - Keep the stones' own grey against the soil-tinted mud, and raise `rubble_mud_share` if the mud
     still reads as plain soil.
   - The albedo carries no light direction, and form comes from the normal and occlusion maps.
   - Compare variants side by side in the lamp-lit Ground Lab (screenshots), then pick one with the
     user.
5. **Texts:**
   - `GroundEffect` becomes "Rubble fill: stony and hard; never collapses".
   - The Ground Lab backfill bays read "rubble fill: about half soil's speed".
6. **Test** (core system, worth it): for every tool level, a backfill cut removes clearly less volume
   per second than a soil cut, between 35% and 65% of soil's. Extend `TerrainMaterialTests`, whose
   tier and response checks already cover `HardnessOrder`.

## Edge cases

- **The chest's lid space is backfill**, so clearing it is slower too; the playtest asks whether it
  becomes a chore. If it does, the fix is in the chest's `LidSoilAllowance` (`109`), not a softer pit.
- **Players may dig around a pit instead of through it.** That still reaches its bottom, because the
  pit is about 2 m across, so it is accepted.
- **The drill's first moments in backfill** must not become a slow needle. `DrillPushes` and
  `DrillEngagedShare` already guard this; check by playing.
- **Mixed cuts across the pit edge** keep per-sample resistance (soil bites soil, backfill bites
  backfill).

## Acceptance criteria

1. Backfill removes 35–65% of soil's volume per second at every tool level (test).
2. In the lamp-lit Ground Lab and on the site, the pit's stones read at a glance, and the user picks
   the texture variant.
3. The tool no longer sinks deeper in backfill.
4. Compiles warning-free and tests pass. The build is delivered and the `106` playtest note is
   updated.

## Playtest (`106` note, updated)

- **Try:**
  1. With the first shovel, dig from soil into the first pit under the plot centre: the stroke
     should feel slower at once and the wall should look stony.
  2. Do the same with the drill (admin level).
  3. Clear the chest's lid space.
- **Good feels like:** "this ground is different; someone filled this in." It should cost seconds,
  not feel like a chore.
