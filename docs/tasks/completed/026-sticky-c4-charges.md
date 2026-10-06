# 026 — Sticky C4 Charges & Material Reactions

**Status:** complete: C4 charges are bought one at a time at the computer and stuck on diggable ground through a
preview that shows the ball they take; one key sets them all off. A blast removes that ball through the dig pipeline
in every ground and cracks geode shell further, leaving finds, chests and lamps whole. A twelve-level C4 track blasts
wider, packs more and makes charges cheaper.

## Objective

Give the player C4: charges bought at the computer, stuck on the ground through a clear preview that shows the
volume they will carve, then set off together from anywhere. A blast takes a big, predictable ball of ground in every
ground, cracks a geode's shell wider than soil, and leaves finds, uniques, chests and lamps whole. A C4 track in the
shop makes blasts bigger, packs larger and charges cheaper.

## Concept rules (`04` §8, `06` §2 and §4, `03` §6 and §9, `08` §4, `10`)

- C4 is an optional accelerator, never the only way past anything. Charges cost money, not battery.
- Thrown or placed, then remotely detonated; several can be armed at once.
- Charges stick where they land: no bouncing, no clipping through the target.
- Placement is forgiving: a clear valid/invalid preview, no pixel hunting; an invalid attempt consumes nothing.
- The blast is properly powerful: a large, predictable volume disappears with matching cleanup; saving for charges
  must feel worth it. C4 is never weak against a common ground.
- Material reactions make where to stick a charge a decision: every ground gets a big blast; a geode's hard shell
  cracks open under a charge, its natural target, far faster than the drill.
- Its own track: blast size, pack size, efficiency (cheaper demolition); twelve levels on the shared price ladder;
  enters the shop with its mechanics, no placeholder purchases. Spare charges are a late money sink.
- Finds survive: distinctives and uniques stay for deliberate collection; commons keep normal pickup; lamps survive.
  Terrain removal and C4 never delete finds. Debris is visual only.
- C4 removes only the ground it was set to remove: it never harms, buries or traps the player. No hazards.
- Controls: a rebindable pair, throw/place and detonate. No camera shake (`10`). Snap-to-surface placement.
- Standing decision (2026-09-28): queue tasks reuse existing art; new 3D models and audio are separate chats.

## Codebase analysis (before)

- **Placement:** `WorksiteTools` owns lamp and mark placement: `HandleInput` toggles a mode from `FpsInputFrame`
  edges (`LampPressed`, `MarkPressed`), `UpdatePreview` solves a pose and swaps ghost materials (`validPreview`,
  `invalidPreview`), Dig confirms, `CancelPlacement`/Back/menus/focus loss/restore cancel. `PlacementPrompt` feeds
  `FpsPlayer.RefreshTargetPrompt`. Placed lamps are `WorkLamp` (Rigidbody + BoxCollider, `IInteractionTarget` to
  pick up, `CheckSupport` on `TerrainVolume.Changed`). `LampKit` holds ownership; `StationTrade.LampOffer` sells one.
- **Shop:** `StationTrade.UpgradeOffer` serves four tracks by `EquipmentKind`; `ComputerStation` lists them, then
  Refill (4), Lamp (5) and sales (6+); `GameMenuView` splits UPGRADES and SERVICES by `RefillCommand` and writes
  headlines per kind. `EquipmentProgression` owns the ladder (`TierPrices`) and per-track tables.
- **Saves:** `WorldSaveCodec` v21 writes one fixed-order stream ending with chests; `WorldSnapshot` holds levels and
  `LampsOwned`; `WorksiteSnapshot` holds lamps and marks. No migration: a version change means New Game.
- **Ground removal:** `TerrainVolume.ClearSphere` → `ExcavationGrid.RemoveSphere` (a plain ball, blind to ground,
  respecting the permanent bank) → `CompleteRemoval` (detached soil, slivers, remnants) → `CommitEdit` rebuilds the
  chunks and raises `Changed`. Subscribers release finds (`DiscoveryField`), chests (`BuriedChest`, which also breaks
  into its pocket), lamps and marks (`WorksiteTools`), geodes open (`CheckGeodeBreaks`) and daylight updates. Finds
  are separate objects, so removal never deletes them.
- **Effects:** `SalvageCrane.EmitGroundBreak(point, normal, removed, reach, intensity)` throws clods, crumbs and
  dust coloured by the ground, pools capped (clods 96, crumbs 512, dust 128). No camera shake exists anywhere.
- **Input:** `InputPreferences` holds 15 rebindable actions (append-only ids); a stored map missing an id is
  rejected whole, so adding two resets custom keys to defaults once (acceptable before release). Free keys: G, B.
- **Art:** no owned pack has an explosive. The Mining pack's flat ingot (committed, used by the chests) is a
  brick the size of a demolition block.

## Design

**Kit and track** (`ChargeKit`, `EquipmentKind.C4`, `EquipmentProgression`):
- `ChargeKit` holds the C4 track level and the charges owned (carried plus armed, like lamps). Its level sets the
  blast radius, the pack size (how many charges it holds) and the price of one charge (`C4Profile`). New Game: level
  1, no charges.
- The C4 track is a fifth upgrade row on the shared ladder; charges are a service row ("Buy C4 / $n") bought one at a
  time up to the pack size. Tables in `EquipmentProgression` (no prices or radii in Markdown).
- "Arrives late" comes from its price: one charge costs about two lamps at level 1, which an early haul does not
  spare, and the track makes it cheaper. No lock and no track dependency (`06`: tracks are independent).

**Placing** (`WorksiteTools.Charges.cs`, `PlacedCharge`):
- **G** opens the charge preview, like the lamp key; Dig sticks a charge, **RMB**/Back cancels. The preview stays
  open while charges remain, so several go out quickly.
- Aim reach is `ChargeReach` (well beyond the lamp's), so a charge is "thrown": it flies from the tool to the aimed
  spot in a short, straight arc and sticks flat to the surface there. No physics bounce.
- Valid: the aim meets diggable ground (a terrain chunk) within reach and a charge is free. Invalid otherwise, with
  the reason ("Aim at diggable ground within N m", "No charges left: buy C4 at the computer", "All charges armed:
  set them off or pick one up"). The ghost charge turns the invalid colour; nothing is consumed.
- The preview shows the carve volume: a translucent ball at the blast centre (`BlastPreview.shader`, seen through
  the ground), the exact radius the blast removes in common ground.
- Interact on an armed charge picks it back up into the kit. A charge whose ground is dug away falls and lies where
  it settles, still armed (like a lamp). A charge never collides with the player.

**Detonating:**
- **B** sets off every armed charge, in the order placed, a tenth of a second apart. Nothing armed: feedback only.
- Each blast removes a ball of the kit's radius centred `BlastSink` of a radius inside the face it was stuck to, so
  the crater opens into the wall or floor. A gentle low-frequency wobble keeps the edge from looking stamped; the
  preview ball covers the blast within that wobble.
- **Material reaction:** geode shell inside the blast goes `ShellReach` times as far as soil: a charge on a shell
  cracks it open wide. Soil and backfill take the full radius (never weak against common ground).
- Through the dig pipeline: a new `ExcavationGrid.RemoveBlast` (ball by ground, permanent bank respected) and the
  shared `CompleteRemoval` cleanup, `TerrainVolume.Blast` commits and notifies, so finds release, chests break in,
  geodes open, lamps fall and marks erase exactly as with digging. A crystal cavern cluster in the blast bursts too
  (`CavernScenery.BlastWithin`).
- Feedback: a brief white-orange flash light, clods, crumbs and dust from the crane's ground-break effects at points
  round the crater, coloured by its ground. No camera shake, no damage, no push; audio is a separate chat.

**Saving:** `WorldSnapshot.C4Level`, `ChargesOwned`; `WorksiteSnapshot.Charges` (position, rotation, stuck or loose);
codec v22 appends them after the chests. Validation: level in range, owned within the pack, armed within owned.

**Art (stand-in):** the charge is the Mining pack's flat ingot mesh in an olive-drab project material with a small
blinking red arming light. A proper model goes to the art chat.

**Admin:** Developer admin → **Fill C4 kit** fills the kit to its pack size (with Add $500 for the track).

## Edge cases

- Placing at a find, lamp, chest, unique, prop, crane or the permanent rim: invalid (not diggable ground).
- Charge on ground that is later dug away: it falls; on detonation it blasts where it lies.
- A charge inside another's blast: still detonates in its turn (from where it fell or floats briefly).
- Player inside the blast: nothing happens to them; the ground under them may go, and they fall harmlessly.
- Blast at the grid's edge or under the rim: the bank and grid bounds clip it like any dig.
- Menus, focus loss, recovery, restore and returning to the surface cancel the preview (existing paths); a
  detonation sequence in progress finishes (blasts are not saved half-way: each is its own terrain edit).
- Ground Lab: charges work; the lab never saves.
- Save while charges fly: a charge in flight is captured at its target (it sticks there).

## Acceptance

- G previews with the carve ball; invalid aims say why and consume nothing; Dig sticks a charge that stays put.
- B sets off all armed charges; each removes about the previewed ball in soil and backfill and more in geode shell;
  cleanup leaves no floating specks; finds, uniques, chests and lamps survive and fall or release as with digging.
- The shop shows the C4 track (12 levels, shared prices) and a charge row bounded by the pack size; charges cost
  money only (no battery).
- Charges, kit level and owned count survive save/load; invalid saved kits are rejected.
- Tests: kit/track trade rules, save round trip and validation, and a blast integration test (volume, geode reach,
  finds and lamps survive, nothing consumed on an invalid placement).
- Fresh Windows build; playtest note.

## Results

- Lab measurements (Editor): a level-1 charge on flat soil took 9.9 m³ (the previewed ball below the surface) in
  25 ms, rebuilding 12 chunks; a level-12 charge on the lab geode took 94 m³ in 70 ms (45 chunks) and opened it with
  its crystals intact. One frame per blast; chained charges spread the work.
- The stand-in block was first the ingot at its own thickness (2 cm), too thin for a demolition block; it is now
  twice as thick (about 4 cm) with a dimmer arming light.
