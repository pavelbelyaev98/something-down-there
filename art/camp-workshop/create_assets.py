"""Camp workshop: builds the geometry (workshop_geometry.py), lays out UVs, bakes procedural wear into five atlases
(outside sheets, inside sheets, timber, concrete, metal/trim/fittings) and exports Workshop.fbx with its maps.
Run in Blender 5.2 through MCP (Cycles CPU bakes); preserves other scenes. RES may be set before running for quick
previews (default 2048). Corrugation is not baked: Unity adds it as a tiling detail normal (Workshop_Ribs_Normal),
so ribs stay crisp up close and never shimmer far away. SHEET_ATLAS_M in Workshop.json is the outside/inside
sheet atlas side in metres, from which the Unity setup derives that detail tiling.

Albedo carries colour, wear and dirt only; occlusion goes to the mask's G channel (long-range inside, so the
interior's ambient falls off away from the door and skylights). Mask: metallic R, occlusion G, detail mask B,
smoothness A.
"""
import importlib
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
ART = ROOT / 'art/camp-workshop'
OUT = ROOT / 'unity/Assets/Content/Camp/Workshop'
MODELS, TEXTURES = OUT / 'Models', OUT / 'Textures'
MODELS.mkdir(parents=True, exist_ok=True)
TEXTURES.mkdir(parents=True, exist_ok=True)
BLEND = ART / 'camp-workshop.blend'
SCENE = 'SDT_Workshop'
RES = int(globals().get('RES', 2048))
SAMPLES_AO = int(globals().get('SAMPLES_AO', 96))
if str(ART) not in sys.path:
    sys.path.insert(0, str(ART))
import workshop_geometry as wg
import bakekit
importlib.reload(wg)
importlib.reload(bakekit)
from bakekit import srgb, activate, pack_sheets, smart_uv, atlas_metres, Graph, finish, common, bake_atlas, final_material




PAINT = srgb(64, 104, 66)
PAINT_FADED = srgb(122, 142, 110)
ZINC = srgb(150, 153, 151)
ZINC_DULL = srgb(118, 121, 119)
ZINC_IN = srgb(158, 160, 156)
ZINC_IN_DULL = srgb(128, 130, 126)
WHITE_RUST = srgb(186, 186, 178)
RUST = srgb(130, 66, 32)
RUST_DARK = srgb(74, 42, 27)
MUD = srgb(112, 86, 60)
DUST = srgb(176, 160, 136)
PINE = srgb(138, 114, 86)
PINE_DARK = srgb(96, 78, 58)
OILED = srgb(86, 56, 32)
OILED_DARK = srgb(54, 34, 20)
CHIP_LIGHT = srgb(170, 136, 96)
CHIP_DARK = srgb(118, 88, 56)
CONCRETE = srgb(152, 149, 142)
CONCRETE_DARK = srgb(110, 108, 103)
OIL = srgb(46, 42, 38)
CREAM = srgb(206, 199, 176)
GREY_WOOD = srgb(120, 108, 92)
RACK_PAINT = srgb(70, 96, 120)
ENAMEL = srgb(214, 210, 196)
RUBBER = srgb(24, 24, 24)
STEEL = srgb(120, 122, 124)

# ---------------------------------------------------------------- scene
for old in [s for s in bpy.data.scenes if s.name in (SCENE, 'SDT_WorkshopPreview')]:
    for obj in list(old.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(old)
for mesh in [m for m in bpy.data.meshes if m.users == 0]:
    bpy.data.meshes.remove(mesh)
scene = bpy.data.scenes.new(SCENE)
previous = bpy.context.window.scene
bpy.context.window.scene = scene
wg.rng.seed(7)
objs = wg.build(scene.collection)


# The metal-like finishes share one atlas: join them, one material slot per finish (slot order below).
METAL_PARTS = ('Galv', 'Trim', 'Rack', 'Fittings')
activate([objs[p] for p in METAL_PARTS])
for slot, part in enumerate(METAL_PARTS):
    mesh = objs[part].data
    mesh.materials.clear()
    mesh.materials.append(bpy.data.materials.new('tmp_' + part))
    for poly in mesh.polygons:
        poly.material_index = 0
bpy.context.view_layer.objects.active = objs['Galv']
bpy.ops.object.join()
metal = objs['Galv']
metal.name = metal.data.name = 'Workshop_Metal'
objs = {k: v for k, v in objs.items() if k not in METAL_PARTS}
objs['Metal'] = metal
# After the join the slots are the four tmp materials in METAL_PARTS order.
slot_of = {m.name.split('.')[0][4:]: i for i, m in enumerate(metal.data.materials)}


# ---------------------------------------------------------------- UVs
for obj in objs.values():
    obj.data.uv_layers.active = obj.data.uv_layers['UVMap']
    obj.data.uv_layers['UVMap'].active_render = True
sheet_side = {}
for part in ('SheetOut', 'SheetIn', 'Skylight'):
    sheet_side[part] = pack_sheets(objs[part])


# Thin members waste most of an atlas inside wide margins; keep them a couple of texels at 2048.
for part, margin in (('Timber', .0007), ('Concrete', .003), ('Metal', .0012), ('Glass', .004), ('TubeLamp', .004)):
    smart_uv(objs[part], margin)


atlas_m = {part: atlas_metres(objs[part]) for part in ('Timber', 'Concrete', 'Metal')}

# Shading: smooth with sharp edges over 30 degrees, so boxes stay crisp and tubes and bowed sheets stay soft.
for obj in objs.values():
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.set_sharp_from_angle(angle=math.radians(30))


def nail_lines(g, value, lines, width=.7):
    """Sum of falloffs below each line (value grows downward from a line)."""
    total = None
    for h in lines:
        d = g.math('SUBTRACT', h, value)
        m = g.mul(g.ramp(d, -.005, .04), g.math('SUBTRACT', 1.0, g.ramp(d, .04, width)))
        total = m if total is None else g.math('MAXIMUM', total, m)
    return total


WALL_NAILS = (wg.SLAB + .7 + .025, wg.SLAB + 2.3 + .025, wg.PLATE - .05)
ROOF_NAILS = tuple(.15 + k * (wg.HW + wg.OVER_EAVE * .6 - .2) / 4 for k in range(5))


def sheet_out_material():
    mat = bpy.data.materials.new('WorkshopSheetOutBake')
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    local = g.xyz(g.uv('Local'))
    lu, lv = local[0], local[1]
    roof = g.ramp(nz, .5, .8)
    wall = g.math('SUBTRACT', 1.0, roof)
    broad = g.noise(p, 1.2, 3, .5, 1.7)
    blotch = g.noise(p, 5.5, 5, .55, 4.1)
    grain = g.noise(p, 60, 6, .6, 2.2)
    # Sun fade: the roof most, the walls more near the top; each sheet its own age.
    fade = g.add(.18, g.mul(roof, .5), g.mul(wall, g.mul(g.ramp(pz, .5, 3.2), .18)), g.mul(g.math('SUBTRACT', piece, .5), .3), g.mul(broad, .25))
    paint = g.mix(g.ramp(fade, .1, 1.0), PAINT, PAINT_FADED)
    # A replaced sheet or two never got painted.
    bare_sheet = g.mul(g.ramp(piece, .935, .94), g.ramp(ny, -.6, -.3))  # never on the front gable
    # Paint lost at the bottom, the lap edges, the eaves and in random flakes.
    edge = g.math('MAXIMUM', g.ramp(lu, .045, .0), g.ramp(lu, .955, 1.0))
    low = g.mul(wall, g.ramp(pz, .55, .05))
    wear = g.add(.05, g.mul(low, .55), g.mul(edge, .35), g.mul(roof, .12), g.mul(g.ramp(piece, .6, 1.0), .18))
    flakes = g.add(blotch, g.mul(grain, .25), g.mul(wear, .62))
    peel = g.ramp(flakes, .86, .88)
    # Rust: streaks down from every nail line, a band at the foot, blooms where paint is gone.
    streak_wall = g.noise(g.vec('MULTIPLY', p, (38, 38, 1.1)), 1.0, 3, .55, 6.4)
    streak_roof = g.noise(g.vec('MULTIPLY', p, (1.1, 38, 38)), 1.0, 3, .55, 8.8)
    ax = g.math('ABSOLUTE', px)
    wall_lines = nail_lines(g, pz, WALL_NAILS, .9)
    roof_lines = nail_lines(g, g.math('MULTIPLY', ax, -1.0), tuple(-x for x in ROOF_NAILS), .8)
    streaks = g.math('MAXIMUM', g.mul(g.mul(wall, wall_lines), g.ramp(streak_wall, .52, .7)), g.mul(g.mul(roof, roof_lines), g.ramp(streak_roof, .5, .68)))
    foot = g.mul(low, g.ramp(g.add(blotch, g.mul(grain, .3)), .35, .6))
    rust = g.math('MAXIMUM', g.mul(streaks, .85), foot)
    rust = g.math('MAXIMUM', rust, g.mul(peel, g.ramp(g.add(grain, g.mul(low, .6)), .55, .75)))
    rust = g.math('MAXIMUM', rust, g.mul(bare_sheet, g.ramp(g.add(blotch, g.mul(low, .5)), .62, .8)))
    # Nail heads on every second crest along the nail lines.
    along = g.add(px, py)
    crest = g.ramp(g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('DIVIDE', along, .152)), .5)), .06, .02)
    line_hit = None
    for h in WALL_NAILS:
        m = g.ramp(g.math('ABSOLUTE', g.math('SUBTRACT', pz, h)), .012, .006)
        line_hit = m if line_hit is None else g.math('MAXIMUM', line_hit, m)
    nails = g.mul(g.mul(crest, line_hit), wall)
    zinc = g.mix(g.ramp(blotch, .3, .8), ZINC, ZINC_DULL)
    zinc = g.mix(g.mul(g.ramp(grain, .7, .85), .4), zinc, WHITE_RUST)
    surface = g.mix(g.math('MAXIMUM', peel, bare_sheet), paint, zinc)
    rust_col = g.mix(g.ramp(grain, .3, .7), RUST, RUST_DARK)
    surface = g.mix(rust, surface, rust_col)
    surface = g.mix(nails, surface, ZINC_DULL)
    # Mud splashed up the foot of the walls and dust settled on the roof.
    splash = g.mul(g.mul(wall, g.ramp(pz, .42, .08)), g.ramp(g.add(blotch, grain), .7, 1.2))
    surface = g.mix(splash, surface, MUD)
    surface = g.mix(g.mul(roof, g.mul(g.ramp(broad, .3, .8), .3)), surface, DUST)
    metal_on = g.mul(g.math('MAXIMUM', peel, bare_sheet), g.math('SUBTRACT', 1.0, rust))
    metallic = g.mul(metal_on, .3)
    rough = g.mixf(g.math('MAXIMUM', peel, bare_sheet), g.add(.6, g.mul(fade, .2)), .5)
    rough = g.mixf(rust, rough, .88)
    rough = g.mixf(splash, rough, .95)
    dents = g.noise(p, 2.5, 2, .5, 5.5)
    height = g.add(g.mul(g.math('SUBTRACT', 1.0, g.math('MAXIMUM', peel, bare_sheet)), .25), g.mul(rust, g.mul(grain, .4)), g.mul(dents, .5), g.mul(nails, .6))
    return mat, finish(g, surface, metallic, rough, height, .3, .003)


def sheet_in_material():
    mat = bpy.data.materials.new('WorkshopSheetInBake')
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    roof = g.ramp(nz, -.5, -.8)
    blotch = g.noise(p, 4.5, 5, .55, 3.3)
    grain = g.noise(p, 70, 5, .6, 1.4)
    spangle = g.voronoi(p, 45, 'F1', 'Color')
    spangle = g.xyz(spangle)[0]
    zinc = g.mix(g.add(g.mul(spangle, .35), g.mul(blotch, .65)), ZINC_IN, ZINC_IN_DULL)
    white = g.mul(g.ramp(g.add(g.noise(p, 200, 3, .5, 2.7), g.mul(blotch, .4)), .72, .82), .3)
    surface = g.mix(white, zinc, WHITE_RUST)
    # Condensation runs down the walls and leaves dark tide lines; rust at the foot and round the nails.
    runs = g.noise(g.vec('MULTIPLY', p, (30, 30, .9)), 1.0, 3, .5, 7.1)
    low = g.mul(g.math('SUBTRACT', 1.0, roof), g.ramp(pz, .7, .1))
    rust = g.math('MAXIMUM', g.mul(low, g.ramp(g.add(blotch, grain), .7, 1.0)), g.mul(nail_lines(g, pz, WALL_NAILS, .35), g.mul(g.ramp(runs, .55, .7), .7)))
    surface = g.mix(g.mul(g.ramp(runs, .5, .66), .25), surface, ZINC_IN_DULL)
    surface = g.mix(rust, surface, g.mix(g.ramp(grain, .3, .7), RUST, RUST_DARK))
    surface = g.mix(g.mul(roof, .25), surface, DUST)
    metallic = g.mul(g.math('SUBTRACT', 1.0, rust), .2)
    rough = g.mixf(rust, g.mixf(white, .55, .75), .9)
    height = g.add(g.mul(rust, g.mul(grain, .5)), g.mul(g.noise(p, 2.5, 2, .5, 5.5), .4))
    return mat, finish(g, surface, metallic, rough, height, .25, .003)


def timber_material():
    mat = bpy.data.materials.new('WorkshopTimberBake')
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    axis = g.attr('axis', 'Vector')
    kind = g.attr('kind')
    along = g.vec('DOT_PRODUCT', p, axis)
    perp = g.vec('SUBTRACT', p, g.vec('MULTIPLY', axis, g.node('ShaderNodeCombineXYZ', X=along, Y=along, Z=along).outputs[0]))
    offset = g.node('ShaderNodeCombineXYZ', X=g.mul(piece, 7.3), Y=g.mul(piece, 3.1), Z=g.mul(piece, 5.9)).outputs[0]
    grain_space = g.vec('ADD', g.vec('MULTIPLY', perp, (14, 14, 14)), g.vec('MULTIPLY', axis, g.node('ShaderNodeCombineXYZ', X=g.mul(along, .5), Y=g.mul(along, .5), Z=g.mul(along, .5)).outputs[0]))
    grain_space = g.vec('ADD', grain_space, offset)
    figure = g.noise(grain_space, 1.0, 5, .6, 2.0)
    rings = g.math('SINE', g.mul(g.add(g.mul(g.vec('DOT_PRODUCT', perp, (1.0, .8, .9)), 26), g.mul(figure, 6)), 6.2832))
    rings = g.ramp(rings, .3, 1.0)
    fine = g.noise(g.vec('ADD', g.vec('MULTIPLY', perp, (120, 120, 120)), g.vec('MULTIPLY', axis, g.node('ShaderNodeCombineXYZ', X=g.mul(along, 2), Y=g.mul(along, 2), Z=g.mul(along, 2)).outputs[0])), 1.0, 3, .5, 4.0)
    end_grain = g.ramp(g.math('ABSOLUTE', g.vec('DOT_PRODUCT', n, axis)), .8, .95)
    up = g.ramp(nz, .6, .9)
    pine = g.mix(g.add(g.mul(rings, .7), g.mul(fine, .3)), PINE, PINE_DARK)
    pine = g.mix(g.mul(g.math('SUBTRACT', piece, .5), .5), pine, PINE_DARK)
    pine = g.mix(g.mul(end_grain, .6), pine, PINE_DARK)
    pine = g.mix(g.mul(up, g.mul(g.ramp(g.noise(p, 6, 3, .5, 9.0), .3, .8), .45)), pine, DUST)
    chips = g.voronoi(p, 220, 'F1', 'Color')
    chip = g.mix(g.ramp(g.xyz(chips)[0], .3, .7), CHIP_LIGHT, CHIP_DARK)
    oiled = g.mix(g.add(g.mul(rings, .6), g.mul(fine, .4)), OILED, OILED_DARK)
    scratch = g.ramp(g.noise(g.vec('MULTIPLY', p, (90, 4, 90)), 1.0, 2, .5, 3.3), .7, .78)
    oiled = g.mix(g.mul(g.mul(up, scratch), .5), oiled, PINE)
    is_chip = g.eq(kind, 1)
    is_bench = g.eq(kind, 2)
    albedo = g.mix(is_chip, pine, chip)
    albedo = g.mix(is_bench, albedo, oiled)
    rough = g.mixf(is_bench, g.add(.72, g.mul(fine, .12)), g.add(.5, g.mul(scratch, .2)))
    height = g.add(g.mul(rings, .25), g.mul(fine, .35), g.mul(end_grain, g.mul(fine, .3)))
    return mat, finish(g, albedo, 0.0, rough, height, .25, .0015)


def concrete_material():
    mat = bpy.data.materials.new('WorkshopConcreteBake')
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    top = g.ramp(nz, .6, .9)
    broad = g.noise(p, .9, 3, .5, 2.4)
    blotch = g.noise(p, 4, 5, .55, 6.1)
    agg = g.ramp(g.voronoi(p, 90, 'F1', 'Distance'), .25, .05)
    base = g.mix(g.add(g.mul(broad, .6), g.mul(blotch, .4)), CONCRETE, CONCRETE_DARK)
    base = g.mix(g.mul(agg, .25), base, CONCRETE_DARK)
    # Mud tracked in through the door, worn into two wheel-and-boot lanes.
    inside = g.ramp(py, -3.0, -3.2)
    track = g.mul(g.ramp(py, -.8, -3.9), g.ramp(g.noise(g.vec('MULTIPLY', p, (3, .35, 3)), 1.0, 3, .5, 1.2), .45, .7))
    lanes = g.math('MAXIMUM', g.ramp(g.math('ABSOLUTE', g.math('SUBTRACT', px, -1.1)), .5, .15), g.ramp(g.math('ABSOLUTE', g.math('SUBTRACT', px, 1.2)), .5, .15))
    mud = g.mul(top, g.mul(track, g.add(.4, g.mul(lanes, .6))))
    mud = g.math('MAXIMUM', mud, g.mul(g.ramp(py, -3.2, -3.9), g.ramp(blotch, .4, .7)))
    # Oil stains by the bench and where a machine stood; rust rings under the rack feet.
    stain = None
    for cx, cy, r in ((-3.2, -1.1, .55), (.6, .4, .8), (2.4, -.8, .35), (-1.9, 2.3, .4)):
        d = g.vec('DISTANCE', p, (cx, cy, wg.SLAB))
        m = g.mul(g.mul(g.ramp(d, r, r * .1), g.ramp(g.add(blotch, g.mul(broad, .5)), .3, .75)), .6)
        stain = m if stain is None else g.math('MAXIMUM', stain, m)
    stain = g.mul(stain, top)
    # Hairline shrinkage cracks.
    crack = g.ramp(g.voronoi(g.vec('ADD', p, g.vec('MULTIPLY', g.node('ShaderNodeCombineXYZ', X=blotch, Y=broad, Z=0.0).outputs[0], (.25, .25, 0))), .45, 'DISTANCE_TO_EDGE', 'Distance'), .012, .003)
    crack = g.mul(g.mul(g.mul(crack, top), g.ramp(broad, .56, .66)), .55)
    edge = g.mul(g.math('SUBTRACT', 1.0, top), g.ramp(blotch, .45, .75))
    albedo = g.mix(stain, base, OIL)
    albedo = g.mix(g.mul(mud, .85), albedo, MUD)
    albedo = g.mix(g.add(g.mul(crack, .8), g.mul(edge, .35)), albedo, CONCRETE_DARK)
    rough = g.mixf(stain, g.add(.86, g.mul(agg, .08)), .45)
    height = g.add(g.mul(agg, .3), g.mul(blotch, .2), g.mul(crack, -.8), g.mul(mud, .3))
    return mat, finish(g, albedo, 0.0, rough, height, .3, .002)


def metal_material(part):
    mat = bpy.data.materials.new('WorkshopMetalBake_' + part)
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    blotch = g.noise(p, 6, 5, .55, 2.9)
    grain = g.noise(p, 80, 5, .6, 4.4)
    convex = g.ramp(geo.outputs['Pointiness'], .5, .56)
    low = g.ramp(pz, .6, .05)
    kind = g.attr('kind')
    if part == 'Galv':
        zinc = g.mix(g.ramp(blotch, .3, .8), ZINC, ZINC_DULL)
        white = g.mul(g.ramp(g.add(blotch, g.mul(grain, .35)), .72, .84), .35)
        rust = g.ramp(g.add(g.mul(low, .9), g.mul(grain, .4), g.mul(blotch, .3)), .75, .9)
        gutter = g.mul(g.ramp(nz, .3, .7), g.ramp(pz, wg.PLATE, wg.PLATE + .3))
        albedo = g.mix(white, zinc, WHITE_RUST)
        albedo = g.mix(rust, albedo, g.mix(g.ramp(grain, .3, .7), RUST, RUST_DARK))
        albedo = g.mix(g.mul(gutter, g.ramp(blotch, .3, .6)), albedo, MUD)
        metallic = g.mul(g.math('SUBTRACT', 1.0, rust), .3)
        rough = g.mixf(rust, g.mixf(white, .48, .75), .88)
        height = g.add(g.mul(rust, grain), g.mul(blotch, .2))
    elif part == 'Trim':
        peel = g.ramp(g.add(blotch, g.mul(grain, .3), g.mul(convex, .5), g.mul(low, .4)), .9, .93)
        paint = g.mix(g.ramp(blotch, .2, .9), CREAM, srgb(188, 182, 160))
        albedo = g.mix(peel, paint, GREY_WOOD)
        albedo = g.mix(g.mul(g.ramp(pz, .9, .2), .5), albedo, MUD)
        metallic = 0.0
        rough = g.mixf(peel, .62, .82)
        height = g.add(g.mul(g.math('SUBTRACT', 1.0, peel), .3), g.mul(grain, .2))
    elif part == 'Rack':
        chips = g.ramp(g.add(g.mul(convex, .7), g.mul(grain, .6), g.mul(blotch, .2)), .93, .96)
        paint = g.mix(g.ramp(blotch, .2, .9), RACK_PAINT, srgb(84, 108, 128))
        albedo = g.mix(chips, paint, g.mix(g.ramp(grain, .4, .8), STEEL, RUST))
        albedo = g.mix(g.mul(g.ramp(nz, .6, .9), .3), albedo, DUST)
        metallic = g.mul(chips, .6)
        rough = g.mixf(chips, .48, .55)
        height = g.add(g.mul(g.math('SUBTRACT', 1.0, chips), .2), g.mul(grain, .15))
    else:  # Fittings: enamel housings (0), rubber cords (1), bright steel (2)
        is_cord = g.eq(kind, 1)
        is_steel = g.eq(kind, 2)
        enamel = g.mix(g.mul(g.ramp(blotch, .3, .9), .5), ENAMEL, srgb(196, 186, 156))
        albedo = g.mix(is_cord, enamel, RUBBER)
        albedo = g.mix(is_steel, albedo, STEEL)
        metallic = g.mul(is_steel, .9)
        rough = g.mixf(is_cord, g.mixf(is_steel, .35, .35), .7)
        height = g.mul(grain, .1)
    return mat, finish(g, albedo, metallic, rough, height, .25, .0015)


sheet_out, sheet_out_nodes = sheet_out_material()
sheet_in, sheet_in_nodes = sheet_in_material()
timber, timber_nodes = timber_material()
concrete, concrete_nodes = concrete_material()
metal_mats = {part: metal_material(part) for part in METAL_PARTS}

for part, mat in (('SheetOut', sheet_out), ('SheetIn', sheet_in), ('Timber', timber), ('Concrete', concrete)):
    objs[part].data.materials.clear()
    objs[part].data.materials.append(mat)
for part, i in slot_of.items():
    metal.data.materials[i] = metal_mats[part][0]

# ---------------------------------------------------------------- bake
bakekit.configure(scene=scene, res=RES, samples_ao=SAMPLES_AO, textures=TEXTURES, occlusion_floor=.35)
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.use_denoising = False
scene.render.bake.margin = 8
if scene.world is None:
    scene.world = bpy.data.worlds.new('WorkshopBakeWorld')


baked = {}
baked['Workshop_SheetOut'] = bake_atlas('Workshop_SheetOut', objs['SheetOut'], [sheet_out_nodes], .45)
baked['Workshop_SheetIn'] = bake_atlas('Workshop_SheetIn', objs['SheetIn'], [sheet_in_nodes], 6.0)
baked['Workshop_Timber'] = bake_atlas('Workshop_Timber', objs['Timber'], [timber_nodes], 5.0)
baked['Workshop_Concrete'] = bake_atlas('Workshop_Concrete', objs['Concrete'], [concrete_nodes], 6.0)
baked['Workshop_Metal'] = bake_atlas('Workshop_Metal', metal, [metal_mats[p][1] for p in sorted(slot_of, key=slot_of.get)], 1.2)


# ---------------------------------------------------------------- export
for part, key in (('SheetOut', 'Workshop_SheetOut'), ('SheetIn', 'Workshop_SheetIn'), ('Timber', 'Workshop_Timber'), ('Concrete', 'Workshop_Concrete')):
    objs[part].data.materials.clear()
    objs[part].data.materials.append(final_material(key, baked[key]))
metal_final = final_material('Workshop_Metal', baked['Workshop_Metal'])
metal.data.materials.clear()
metal.data.materials.append(metal_final)
for poly in metal.data.polygons:
    poly.material_index = 0
for part, name in (('Skylight', 'Workshop_Skylight'), ('Glass', 'Workshop_Glass'), ('TubeLamp', 'Workshop_TubeLamp')):
    objs[part].data.materials.clear()
    objs[part].data.materials.append(final_material(name, None))

export = list(objs.values())
for obj in export:
    local = obj.data.uv_layers.get('Local')
    if local is not None:
        obj.data.uv_layers.remove(local)
activate(export)
bpy.ops.export_scene.fbx(filepath=str(MODELS / 'Workshop.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, use_triangles=True, mesh_smooth_type='OFF',
                         add_leaf_bones=False, bake_anim=False)
(OUT / 'Workshop.json').write_text(json.dumps({'sheet_atlas_m': sheet_side['SheetOut'], 'sheet_in_atlas_m': sheet_side['SheetIn'],
                                               'skylight_atlas_m': sheet_side['Skylight'], 'rib_pitch_m': wg.SHEET_LAP,
                                               'width_m': wg.W, 'depth_m': wg.D, 'slab_m': wg.SLAB, 'plate_m': wg.PLATE,
                                               'door_half_m': wg.DOOR_HALF, 'door_top_m': wg.DOOR_TOP, 'ridge_m': wg.roof_z(0),
                                               'pitch': wg.PITCH, 'eave_overhang_m': wg.OVER_EAVE, 'gable_overhang_m': wg.OVER_GABLE,
                                               'atlas_m': atlas_m},
                                              indent=1))
bpy.data.libraries.write(str(BLEND), {scene}, fake_user=True)
bpy.context.window.scene = previous
result = {'tris': {n: sum(len(p.vertices) - 2 for p in o.data.polygons) for n, o in objs.items()}, 'sheet_side': sheet_side, 'res': RES}
print(result)
