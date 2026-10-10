# 119 — Unique Stash in the Camp Workshop

**Status:** on trial. Bag-sized uniques are taken with E (no slot, HUD trophy x/y) and set down with E at their own
glowing spot in the workshop (racking shelves, workbench); crane loads land in the yard beside it.

## Objective
Give every unique a reason to come home and a place it belongs. A unique small enough for the bag is taken by hand
(Interact) once exposed: it uses no slot, is never sold and is carried until the player is back at camp. There its own
spot in the workshop glows; Interact at the spot sets it down there for good. Bigger uniques keep the crane, which now
sets them down in a yard beside the workshop, so the whole collection gathers in one place. The HUD shows how many of
the save's uniques the player has secured (x/y). Testable at once in the Ground Lab.

## Concept reference
- 05 §1: uniques exist once per save, are unsellable, use zero bag slots, have a story line, no perk. §3: deliberate
  exposure before any collection (60%); hand-taken finds wait for Interact (the chest's ingots); uniques solid to the player.
- 07 §1/§5: the workshop is the camp's building and the computer stands inside; recovered uniques are readable where
  they stand (name, depth, story); no shelves-as-placement chores beyond one deliberate set-down.
- 08 §1: HUD is icons and numbers; the user asked for a secured-uniques count (2026-10-10), recorded there.
- 13: the stash direction (user, 2026-10-10): no sell-or-keep choice, one of each unique, a satisfaction game; bag-sized
  uniques go in the bag and are placed by hand, the crane keeps loads too big for the bag.
- 15: nothing shrinks away; never lose what the player earned (carried uniques survive recovery and saves).

## Live codebase (what exists)
- `BuriedFind`: `RecoveryMethod { Bag, Rope }`, `FindState { World, Extracting, Collected, Stored }`; hand-picked
  commons (`handPicked`) take Interact; rope uniques mark for the crane and become `Stored` at a set-down spot
  (`SalvageCrane.Deliver`: `MoveRecovered`, `Transition`, `FindPhysics.Restore(false)`); `Stored` shows the lore card.
- Uniques come from authored source catalogs (`RetroComputerSetup`, `CrystalTrophySetup`); prop finds bake prefabs with
  `PropBake` (`PropFindSetup`). `DiscoveryCatalog.Validate` requires uniques to be rope-recovered.
- Saves store each find's state as a byte; `WorldSnapshot.Validate` checks unique ownership and depth records.
- HUD (`GameHud.uxml`, `GameHudView`): bag and lamp counters with icons; pickup notes via `FpsPlayer.Collected`.
- Ground Lab (`GroundLab`, `DiscoveryField.UseGroundLab`): crane scenes with catalog computers; set-down uniques clear
  after 5 s.
- Crane set-down spots: `SalvageCraneSetup.SetDownSpots`, world positions east of the camp; mast at (15, -14).

## Changes
- **Runtime:** `RecoveryMethod.Carry` and `FindState.Carried`. A carry unique: prompt "Name | E to take" when exposure
  is ready; Interact (tap) takes it: depth recorded, state `Carried`, hidden, pickup note. `Collected` covers `Carried`.
  `PlaceAt(pose)`: `Carried` → `Stored`, standing on the spot, kinematic, solid, lore readable.
- **`StashSpot`** (Interaction): one per carry unique under `CampWorkshop/Stash`, its content id and pose; a chalk outline
  of the unique's footprint that glows (pulsing) only while that unique is carried, with a thin aim collider enabled
  only then; prompt "Name | E to place"; Interact places it. `DiscoveryField` raises `StashChanged` on take, place and
  restore; spots follow it.
- **HUD:** a trophy icon with "secured/total" uniques (any state but World), shown when the save has uniques.
- **Data:** `art/stash-uniques/catalog.json`: each carry unique's prop prefab, authored burial pose, find policy and its
  stash spot (workshop-local pose). First uniques: the wood-cased TV (its junk copies leave the junk roster, the CRT
  stays junk at six), a field terminal and an operator console (Cosmic Retro Computer pack). `StashUniqueSetup` adds them
  in Sync Discovery Models; `Validate` accepts carry uniques; Configure Camp Workshop builds the spots and the outline
  material (Excavation Lit, emission) from that file.
- **Crane yard:** the set-down spots move to a yard beside the workshop, mostly on its side toward the mast, clear of the
  walk from the camp to the door (`CampWorkshopSetup.YardSpots`, used by Configure Salvage Crane).
- **Ground Lab:** a shallow trench south-west of the bays holds one of each carry unique ready to take; the aim prompt
  names it; placed lab uniques stay (only craned ones clear).

## Edge cases
Full bag (no effect on carry uniques); taking with the menu open or unfocused (refused); zero-battery rescue and saves
mid-carry (state persists, spot still glows after load); placing while a spot is blocked by the player (allowed, the
unique is solid and pushes them); a carried unique whose spot is missing (stays carried, feedback); Ground Lab restart
(lab copies only, nothing saved); craned yard spots all taken (crane keeps its existing full-yard behaviour).

## Acceptance criteria
- Exposed carry unique: "E to take" takes it with no bag slot; HUD counter rises; it is never offered for sale.
- Back at camp its own spot in the workshop glows; aiming at it offers "E to place"; placing stands it there, solid and
  readable (name, depth, story); the glow goes out; each unique has its own spot.
- Save/load while carrying and after placing restores both; validation accepts `Carried`.
- Craned uniques land in the yard beside the workshop.
- Ground Lab: the trench's uniques can be taken, counted and placed without digging first.
- Warning-free compile; build; playtest note `119`.

## Build notes
- The racking's single solid collider hid the shelf spots and placed finds from the aim ray; the racking now collides
  as its boards and uprights.
- Rejected: the console on a bottom shelf. From standing eye height the middle shelf hides it, so it stands on the
  second bay's middle shelf beside the TV.
- Taking a unique that had come loose from the soil clears its physics release, so a save while carrying validates.
