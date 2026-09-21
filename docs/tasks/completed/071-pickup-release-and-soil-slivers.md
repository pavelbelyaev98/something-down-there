# 071 — Nearby pickup, natural release and soil slivers

**Status:** complete. Automatic collection reaches visible nearby finds at any height during walking/held digging, nearly freed rocks release naturally, full bags use the resource-warning banner, and paper-thin soil ribbons crumble within the stroke.

## Objective
Make automatic collection work comfortably around the player and on falling finds, release nearly excavated rocks naturally, show full inventory as a resource warning, and remove unusable paper-thin soil strips during excavation.

## Concept reference
`05_DISCOVERIES.md` exposure/collection and capacity rules; `03_WORLD_AND_SITE.md` editable ground; `09_FEEL_ART_AND_AUDIO.md` readable physical excavation.

## Current implementation
- `FindWalkCollection` only searches a tiny feet capsule during grounded movement. Exact aimed collection has separate longer reach, which does not help automatic collection.
- `BuriedFind.HasSoilAttachment` keeps `FindPhysics` anchored while any surface sample has soil contact, even after nearly complete exposure.
- `GameHudView` and `GameHud.uxml` already own the low-fuel banner; full inventory only appears in target/feedback text.
- `ExcavationGrid.RemoveTinyRemnants` preserves long sheets and multi-support connections regardless of how thin they become. Density and mesh/collision must be corrected together.

## Changes
- Refactor the existing collector into nearby collection with a three-dimensional range, direct visibility and full-exposure/free-item requirements. Run during grounded movement or held digging, independently of shovel cooldown. Keep bag, menu, focus, throw/drop exclusion and terrain-restore guards; no through-wall collection.
- Release mostly exposed finds when only shallow surface contacts remain and their inner volume is clear. Substantial burial continues to anchor them; physics owns the fall and contact response.
- Add an inventory-full banner using the existing resource-warning style; allow simultaneous fuel/full-bag warnings without overlap. Full bags leave overflow in place.
- Extend local density cleanup to collapse paper-thin connected strips, regardless of their length. Preserve thicker ledges, supported crowns, boundaries and untouched ground; include cleanup in the stroke's bounds, volume, revision and collider rebuild.

## Edge cases
Falling/elevated finds, off-centre pickup while digging, full bag then freed capacity, obstructed sightlines, airborne travel without digging, deliberate throws/drops, nearly released versus substantially buried rocks, diagonal thin strips, long strips attached at both ends, repeat strokes and checkpoint restore.

## Acceptance
- Automatic collection works beyond foot contact and on nearby elevated/falling clear finds; occluded and partially buried finds remain.
- A nearly exposed rock becomes dynamic before every surface contact is removed; substantially embedded rocks stay anchored.
- Inventory-full and low-fuel warnings share presentation and remain independently readable.
- Thin-strip regression fixtures clear in the same stroke without damaging useful thicker supports; relevant physics/collection/terrain checks pass.
- Compile and asset validation are clean, gameplay/UI reviewed, and a fresh Windows player builds and launches.

## Verification
- 101 targeted checks passed across grid excavation, live terrain, find physics and collection, including targeted reruns after updating fixtures that assumed the retired foot-only/hover-only pickup behavior. Coverage includes diagonal and multiply attached ribbons, thicker supports, elevated/off-centre finds, occlusion, full bags, deliberate drops, held/toggled input and shallow-contact release.
- Reviewed connected excavation cuts and both warning banners in MainGame. The banners do not overlap; freeing capacity clears the inventory warning and menus hide the HUD. Review changes were discarded.
- C# compiled cleanly and Odin returned 14,490 valid checks with no issues. Windows build succeeded and launched without startup errors; its only build warning is the intentionally disabled Pipeline player bridge.
- Test preflight now replaces even an untitled scene directly with an empty scene, avoiding Unity's save prompt under the disposable-scene policy. Recovery and vendor assets remain untouched.
