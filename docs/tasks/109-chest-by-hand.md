# 109 — The Chest by Hand

**Status:** planned after `108`. Spec written ahead (2026-10-05); re-check against the code when it
starts. Plan and research: [107](completed/107-asset-only-grounds.md).

## Objective

The old chest opens by **holding Interact (E)**: the rusted lock gives, the lock drops and the lid
swings up on what it holds (its `ChestAnim`). Once the player has taken everything, the chest
**despawns quietly**, the next time it is out of sight and untouched, so emptied chests do not litter
the pits. This first version is meant to change after a playtest (user: "maybe, for now").

## Concept reference

- **`05` §3 "Finds inside finds":** the chest opens where it lies, with no keys or lockpicking, and
  the contents give a second reveal.
- **Rules that stay:**
  - contents are seated from the finite population and never reroll or duplicate;
  - they become visible before collection;
  - the container never hides or loses its contents.
- **This ticket changes** "strike its rusted lock with the tool" to "hold Interact to force its rusted
  lock". It also changes "stays there, solid, after it is emptied" to "goes once emptied and out of
  sight".
- **The same hold verb** already marks uniques for the crane (`05` §3, whole-object recovery): hold
  Interact on the visible object, with a fill that shows progress, and release to cancel.
- **Solid world** (user preference): the chest stays solid while it matters, never pops away in
  front of the player, and never vanishes from under them.

## Live codebase analysis

- **`BuriedChest`** implements `IDigTarget` and `IInteractionTarget`:
  - `CanDig` requires `!Opened && LidHasRoom()`;
  - `TryDig` sets `Opened`, plays `ChestAnim` and calls `field.NotifyMotion()`;
  - `TryInteract` returns false;
  - prompts are built in `GetPrompt`;
  - `ChestSnapshot { Position, Rotation, Released, Opened }`;
  - support and release happen in `FixedUpdate`.
- **The hold:**
  - `FindExtractionInteraction` is the only hold, hard-wired to `BuriedFind` and the crane.
  - `Progress = seconds / Crane.Settings.MarkSeconds` (1.1 s).
  - A new hold needs a release first (`waitForRelease`), and a binding change resets it.
  - Completion calls `Crane.TryMark`.
  - Exposed as `FpsPlayer.ExtractionMarkProgress`, which feeds the prompt % (`RefreshTargetPrompt`
    611), the lowered tool (`ToolRigPresenter` 87) and the crane's mark preview
    (`SalvageCrane.RefreshMark` via `TryGetRecoveryMark`).
  - Used by `UniqueRecoveryIntegrationTests` (452).
  - `FpsPlayer.Tick` runs it before `TryInteract`, and a valid hold consumes the frame.
- **Contents:**
  - They are ordinary finds with no link to the chest.
  - "Inside" is `chest.Hollow.Contains(chest.transform.InverseTransformPoint(pos))`.
  - `pos` is the find's physics pose when released (`BuriedFind.Record`).
  - Collected finds have `State` Collected or Stored.
- **Chest lifecycle:**
  - `DiscoveryField.Restore` rebuilds exactly the captured chest records; with none it spawns none.
  - New Game spawns one per stash.
  - Chests have no stable ids: a list index only.
- **Missing helpers:**
  - No camera-visibility helper exists. `FindProximityCollection.CanCollect` has a
    forward-hemisphere and line-of-sight pattern.
  - "Touching" exists only in `LoadRide`, for uniques, as a capsule overlap.

## Design

### 1. One hold for everything

- **New contract `IHoldTarget`** in `WorldActionContracts`:
  - `bool CanHold(FpsPlayer player)`
  - `float HoldSeconds(FpsPlayer player)`
  - `bool CompleteHold(FpsPlayer player, RaycastHit hit)`
- **`FindExtractionInteraction` becomes `HoldInteraction`:**
  - It raycasts `InteractReach` and takes `Contract<IHoldTarget>` with `CanHold`.
  - It keeps the release-first and binding-revision rules.
  - A changed target resets the hold.
- **`BuriedFind` implements it with the crane mark:**
  - `CanHold` uses today's conditions (crane configured and not busy, not placing, `CanMark`);
  - `HoldSeconds` is `MarkSeconds`;
  - `CompleteHold` is `Crane.TryMark`.
  - Behaviour, timing and the mark preview stay unchanged.
- **`FpsPlayer.ExtractionMarkProgress` becomes `HoldProgress`.**
  - The prompt % applies to any held target.
  - `TryGetRecoveryMark` returns the held target only when it is a `BuriedFind`.
  - `ToolRigPresenter` lowers the tool while any hold runs.

### 2. The chest's lock

- **`BuriedChest` drops `IDigTarget`** and implements `IHoldTarget`:
  - `CanHold` means `!Opened && LidHasRoom() && !terrain.IsRestoring`.
  - `HoldSeconds` is a chest constant, a little longer than the crane mark (a rusted lock: about 1.5 s).
  - `CompleteHold` sets `Opened`, plays `ChestAnim` and calls `NotifyMotion`.
- **A dig stroke on the chest** gets the ordinary "Cannot dig here" feedback.
- **Prompts** (`GetPrompt`):
  - closed, lid space blocked: `Old chest | Clear the soil above its lid`
  - closed, room to open: `Old chest | Hold E to force the rusted lock` (the binding's display name),
    with the % while held
  - opened: `Old chest`

### 3. The emptied chest goes

- **Empty:**
  - The chest is opened, and its opening animation has finished.
  - No uncollected find (`State` World or Extracting) has its current pose inside the hollow.
  - This is checked in the chest's own frame, so a fallen or tipped chest works too.
  - Finds that tumbled out do not count.
- **Unseen:** the chest's renderer bounds are outside the player camera's frustum (with a small
  margin), or no line of sight reaches the bounds' centre from the eye (soil in between).
- **Untouched:** the player's capsule, slightly inflated, does not overlap any chest collider (the
  `LoadRide` capsule pattern).
- **Checks:** run a few times per second, not every frame. The chest needs the player (camera and
  capsule), passed in `Initialize` with the terrain and the field.
- **Removal:** when all three hold, the field removes the chest (destroy, drop it from `chests`,
  `NotifyMotion`).
  - Nothing is saved for it: `CaptureChests` simply no longer lists it, and restore spawns only
    listed chests.
  - No codec change; `107` already moved the version.

## Edge cases

- **A closed or still-full chest never goes.** A unique never sits in a chest, so `Extracting` never
  applies; it is still checked, for safety.
- **Full bag:** the contents stay inside, so the chest stays.
- **The player stands on or leans against an empty chest:** it stays until they leave it and it is
  out of sight.
- **Released (fallen) chest:** the same rules apply, using its body's pose.
- **Save while an emptied chest still exists:** it is captured and restored, and goes later by the
  same rules.
- **Admin reset or Ground Lab:** `UseGroundLab` already clears chests, and the site restore brings
  back exactly the saved ones.
- **Interact reach:** a chest at the pit bottom must be reachable from the cleared lid space within
  `InteractReach` (3 m); check in play.

## Tests

- Update `StashChestIntegrationTests`:
  - opening by hold (not dig);
  - a hold released early does not open;
  - a dig stroke never opens;
  - the undercut-fall test stays.
- Add a despawn test: open, collect or remove the contents, turn the camera away, and the chest goes.
  - It stays while in view, while touched, and while anything remains inside.
  - After save and load the chest count stays reduced.
- `UniqueRecoveryIntegrationTests` uses `HoldInteraction`; the crane mark is unchanged.

## Acceptance criteria

1. The chest opens only by holding Interact once its lid has room. The prompt shows the binding and
   the %, and the animation plays.
2. An emptied chest despawns only when out of sight and untouched, never with contents inside, and
   stays gone after save and load.
3. Crane marking works exactly as before.
4. Compiles warning-free and tests pass. The build is delivered and the `106` playtest note is
   updated.

## Playtest (`106` note, updated)

- **Try:**
  1. Clear the first chest's lid space, hold E and watch it open.
  2. Take everything, turn around, and look back.
  3. Fill the bag first and check the chest stays with its contents.
  4. Save and quit, then Continue.
- **Tell the agent:**
  - whether the hold length feels right;
  - whether the quiet despawn feels right or should change (stay until the tool breaks it, or
    crumble in view).
