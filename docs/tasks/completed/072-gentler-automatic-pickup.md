# 072 — Gentler automatic pickup

## Objective and concept
Dial back the broad automatic collection added in 071 while retaining convenient pickup above foot height and between digging strokes. Update `05_DISCOVERIES.md` to favor a close collection area over sweeping nearby loose finds into the bag.

## Existing implementation and change
`FindProximityCollection` owns the camera-centred query, visibility, full-exposure check and deliberate-drop exclusion. Reduce its automatic radius by one quarter; the departure margin follows that radius. Keep the current aimed collection and falling-find timing. No new system, serialized data or scene authoring is needed.

## Edge cases and acceptance
- Elevated free finds within the closer range still collect without precise aim; off-centre finds beyond it stay in the world.
- Occlusion, grounded walking, full bags and deliberate drops retain their guards.
- Held/toggled digging and immediate falling-find collection remain responsive.
- Focused live collection checks pass, compile/build validation is clean, and a fresh Windows player launches.

## Verification
- Nine live checks passed: close versus distant elevated collection, obstruction, walking/airborne behavior, falling pickup during recovery, deliberate drops, capacity, and held/remapped-toggle wide scoops.
- Compilation and build validation passed. Fresh Windows player built and passed startup smoke testing; the only build warning is the intentionally disabled Pipeline player bridge.
