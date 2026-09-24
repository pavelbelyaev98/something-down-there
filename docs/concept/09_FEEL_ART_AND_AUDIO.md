# 09 — Game Feel, Art and Audio

## 1. Visual style

Stylized painted low-poly: strong silhouettes, restrained texture detail, painted gradients,
cohesive art direction across custom and licensed assets. Bright and readable, never realistic mud, never asset-store clutter.
Surface daylight reads as solar noon: a nearly overhead sun, short shadows, vivid grass,
warm soil and a blue sky with soft clouds. Increase color richness without washing out texture detail
or lifting the black level in deep tunnels.
Preserve the established vivid color grade across the game. Improve underground readability
through the excavation daylight and ground response, without flattening surface colors.
Lanterns light a broad work area with gradual falloff, without bleaching nearby textures or changing the surface grade.
The reservoir crust and freshly dug soil read as dry, rough ground: no mirror-like white
glare or crystal sparkles. Preserve texture relief and contact shadows without a wet sheen.
The whole round diggable surface is a meadow using approved pack plants: low grass,
white/blue daisies, taller white/yellow/pink flowers and occasional ferns, with small clearings.
Use pack turf instead of custom turf, continuous to cut edges with a soft, natural transition
into soil. Avoid sharp sawtooth colouring and dark polygonal outlines around holes.
Keep plants rooted beside holes; clip only foliage actually above the opening, including wind
movement. Remove whole plants when their roots lose support, without a padded clearing margin.
Use approved pack ground throughout the top layer beneath the meadow. The comparison is over;
retain the custom soil art as an inactive option for later, without applying it to the site.

- **Zones read instantly:** strong palettes per zone with gradual transitions.
- **Materials read by shape as well as color**, so colorblind players can still tell ground apart.
- Soil, clay and rock use distinct grain and relief patterns matching their saved deposits; texture scale stays consistent on floors, walls and ceilings.
- **Objects read by silhouette**, because recognition is the core reward.
- Pure Nature 2 supplies the meadow plants, ground textures and sky. Keep the surrounding yard
  plain gravel with stores only; the valley composition and all scenery are retired.
- Custom grass clump, cloud and sun art and the rejected first-person tool experiments are removed.
  Preserve the full licensed pack for future use; tune project-owned copies.
- No AI-generated images or textures; custom Blender models and approved licensed assets share one style guide.
 AI-assisted modeling with real references is allowed.

## 2. Zone palettes and mood

| Zone | Palette | Lighting mood |
|---|---|---|
| Recent fill | Warm browns, greens, rusty metal, bright sky | Warm daylight, open, hopeful |
| Old sediment | Grey-blue, clay orange, dull steel | Cool daylight fading, nostalgic |
| Deep clay/stone | Saturated clay reds, dark rock, wet gleam | True darkness; only placed lamps light the ground |
| Ancient constructed | Cold tones, unnatural smoothness, faint glow accents | Total darkness; only placed lamps show the surfaces |

Darkness escalates from shade to true black; lit ground stays readable, and lamps are the only light underground.
Give early digging a generous daylight reach, with a gradual transition before deep darkness.
Long lateral tunnels also reach darkness; terrain and finds lose their ambient colour
and sky reflections together instead of retaining an artificial visibility floor.

## 3. The absurdity, visually

Absurdity is controlled and deadpan:

- The machine escalates into a welded, bolted, over-batteried monster.
- Objects are placed straight-faced; the jokes are in what they are and what they are worth.
- Physical comedy is allowed: pile wobble, a car yanked out of the ground, the salvage winch
 straining a taut cable before a dirt rupture sends the load swinging into the next obstruction.
 Recovery has a brisk strain–release rhythm, with momentum-driven chains of breakage rather than constant-speed careful chipping.
- No random wackiness, no jokes baked into every texture, no cartoon eyes on the drill.

## 4. Dig feel

Continuous and controlled; rapid contact should not excavate the ground too quickly:

- Hold to rapidly shave shallow layers from the contacted surface, with an immediate response and
  measured progress as the aim moves. Keep each layer thin enough to control the excavation and
  notice emerging objects. Avoid a stop-start sequence of large scoops. Dust and crumbs
  follow the cut when the feedback pass is implemented.
- Collecting objects never pauses shaving or scoop digging, resets the cutting cadence, or waits
  for the pickup animation. Held digging continues through collection.
- The camera never shakes or jerks from digging. No motion effects are added just to have toggles
 for them.
- Audio is per material: sand hisses, clay thumps, rock cracks, concrete grinds.
- **Seam cleaving feedback:** cutting broadly along a seam produces a sharp stress crack, a subtle
 visual settling shift, and a heavy fracturing break as the worked slab gives way.
- The machine's behavior and sound improve with upgrades, so power is felt in the hands, not read
 from a stat screen.
- Downward digging feels good with the starting shovel; upgrades make it feel ridiculous. Power
 outpaces tougher ground, while automatic material responses stay distinct through the final tier.

## 5. Material behavior

Each material family has a distinct response profile. Start with the same five working groups
as [World and Site](03_WORLD_AND_SITE.md); the exact material list remains open:

| Material | Bite | Residue | Sound |
|---|---|---|---|
| Loose earth (sand / soil) | Fast spilling / even cuts | Pours / crumbs | Soft hiss / dull thud |
| Clay / sediment | Sticky or resistant, steady | Clumps / flat chips | Wet thump / muffled crunch |
| Gravel | Trickles | Loose stones | Rattle |
| Rock | Slow, chipping | Shards | Sharp crack |
| Diggable concrete | Tough; early tools still make visible progress | Sparks, dust | Grinding screech |

**Impossibility signature:** ancient fabricated materials add one small, consistent response on top
of the working groups — an unusually clean cut, a glass-like resonance, dust that settles too
neatly. The signature appears only where the tool contacts the material, never as a direction,
proximity or value cue. It is a property of the material, never an answer: nothing in it reacts to
the player's presence, position or attention. Every part has a visual counterpart so the game stays
readable muted, and it never signals a threat.

## 6. Audio design

Ambience and feedback only. **No music. No voice acting**.

- **Zone ambience layers:** wind and distant water near the surface; drips and settling rock deeper;
 a low, almost-silent hum in the ancient zone. Layers crossfade with depth.
- **Action feedback:** dig loops per material, seam fractures, footsteps, jetpack thrust, salvage
  bladder inflation and mud-release pop, C4 blast, machine interactions, pickup chimes, the computer's sale feedback.
- **No audio-only clues.** Every sound that carries information has a visual counterpart. The
  detector is silent by design and readable while muted.
- **No threat-adjacent audio anywhere.** The deep zone hums; it never breathes, whispers, follows or
  stalks. Nothing in the mix implies a presence, and the impossible-material resonance never sounds
  like a response ([Ending and Mystery](11_ENDING_AND_MYSTERY.md)).
- **Mix:** ambience stays under the dig loop; picking, digging and the computer are the
 loudest, most satisfying elements.
- Licensed audio may be used where needed, but custom is preferred; every sound is reviewed for
 long-session fatigue.

## 7. FX policy

Comfort-safe effects:

- Dust, crumbs, sparkles, smoke from C4, splash from water-adjacent areas.
- Winch soil breaks eject a brief local burst of dirt crumbs and soft dust at the retaining contact, only when ground is actually removed. Keep the load and nearby excavation readable through the effect.
- No screen shake, no blood or gore, no full-screen flashes, no chromatic aberration,
 no forced bloom.
- Particles never collide and never deal damage.
- Intensity is adjustable; nothing visually discomforting is mandatory.

## 8. UI art

Clear, readable, industrial-worksite in flavor (final visual treatment later): stenciled
labels, simple type, high contrast, scale-friendly. UI never competes with the world; it stays out of
the way.

## 9. Photo-ready moments (by design, not by marketing)

The game should naturally produce absurd, striking screenshots: a ridiculous machine silhouetted in
a deep hole, a gramophone half-buried in pale sediment, a car mid-yank on a cable, a warm lamp pool
in a dim ancient zone. Photo mode (pause-only, HUD hidden) exists for exactly these moments.
