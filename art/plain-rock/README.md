# Asset: Plain Rock (4 Variants)

- **Item / Purpose:** the commonest shallow find, `common_rock` with looks `b`-`d` (`rock.json`, `RockSetup`).
- **Source / License:** shapes are Pure Nature 2: Mountains' mossless stones `Stone1b`-`4b` (user-purchased BK pack, Unity Asset Store EULA; card `art/pure-nature-mountains`); the colour is the retired original Blender rock's own colour map (`rock_colour.png`, graded warm grey-brown, original work under `LICENSE.txt`).
- **Unity Path:** `unity/Assets/Content/BuriedProps/Stones/` (mesh copies, `StoneRock.mat`, `Rock_Stone*.prefab`, maps), finds in `Content/Minerals`.
- **Tooling:** `make_stone_rock.py` writes the maps (a seamless tile of the colour, 3 x 3; the stones' own normal and occlusion); **Configure Buried Props** builds the prefabs (`BuriedPropsSetup.Stones`), **Sync Discovery Models** the finds.
- **Status:** in the game (user, 2026-10-08: new shapes, "not the poop like form", colours kept).
