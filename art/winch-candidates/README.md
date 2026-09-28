# Rim winch candidates
- Item: two detailed rim winches with wire rope: A, a slewing tripod hoist on a cross base with a compact lifting magnet; B, a trailer crane with a slewing turret, luffing lattice jib and a single suction cup. Neither uses a hook: a small (about 0.6 m, real-size) attachment sticks onto the find.
- Purpose: A/B pick for the salvage winch's look, sized for oversized salvage (machine at 3x, person-scale controls, attachment well out over the hole); built to slew so a load can be swung from the hole onto a pad. The winner replaces `art/salvage-winch`'s winch fixture.
- Source/license: original project geometry created through Blender MCP; no external content.
- Recipe/source: `create_assets.py`, `winch-candidates.blend`. Fixed root `A_Base`/`B_Chassis`; slewing root `A_Slew`/`B_Turret` (turns about local y) parents `_Drum`, `_*Sheave`, `_Rope`, `_Attachment` (origin at the rope end) and, on B, `_Jib` (luffs about local x, parents `_TipSheave`) and `_Pendants`. All pivots sit on their axes with no rotation.
- Unity path: `Assets/Content/WinchCandidates` (materials; models in `Models/`), placed by Place Winch Candidates.
- Status: unlinked props in their home pose on the camp arc; the working winch still uses the old fixture.
