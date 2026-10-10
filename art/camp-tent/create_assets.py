"""Camp tent: builds the geometry (tent_geometry.py), lays out UVs, bakes procedural wear into four atlases (outer
canvas, inner canvas, wood, metal) and exports Tent.fbx with its maps. Run in Blender 5.2 through MCP (Cycles CPU
bakes) after art/camp-workshop (it shares that folder's bakekit.py); preserves other scenes. RES and SAMPLES_AO may be
set before running for quick previews. The canvas weave is not baked: Unity adds Tent_Weave_Normal as a tiling
detail normal; CANVAS_ATLAS_M in Tent.json is the canvas atlas side in metres, from which the setup derives its tiling.
"""
import importlib
import json
import math
import sys
from pathlib import Path

import bpy

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
ART = ROOT / 'art/camp-tent'
KIT = ROOT / 'art/camp-workshop'
OUT = ROOT / 'unity/Assets/Content/Camp/Tent'
MODELS, TEXTURES = OUT / 'Models', OUT / 'Textures'
MODELS.mkdir(parents=True, exist_ok=True)
TEXTURES.mkdir(parents=True, exist_ok=True)
BLEND = ART / 'camp-tent.blend'
SCENE = 'SDT_Tent'
RES = int(globals().get('RES', 2048))
SAMPLES_AO = int(globals().get('SAMPLES_AO', 96))
for path in (ART, KIT):
    if str(path) not in sys.path:
        sys.path.insert(0, str(path))
import bakekit
import tent_geometry as tg
importlib.reload(bakekit)
importlib.reload(tg)
from bakekit import srgb, activate, pack_sheets, smart_uv, atlas_metres, Graph, finish, common, bake_atlas, final_material

OLIVE = srgb(86, 90, 60)
OLIVE_DARK = srgb(64, 68, 46)
OLIVE_FADED = srgb(122, 120, 92)
OLIVE_PATCH = srgb(76, 84, 54)
WEBBING = srgb(92, 88, 62)
MUD = srgb(92, 80, 66)
DUST = srgb(170, 156, 132)
MILDEW = srgb(40, 42, 32)
SPRUCE = srgb(166, 146, 116)
SPRUCE_DARK = srgb(124, 104, 80)
GREY_WOOD = srgb(134, 128, 116)
CHIP_LIGHT = srgb(168, 140, 102)
CHIP_DARK = srgb(118, 94, 64)
PLY = srgb(196, 168, 126)
PLY_EDGE = srgb(150, 120, 84)
ZINC = srgb(150, 153, 151)
ZINC_DULL = srgb(118, 121, 119)
WHITE_RUST = srgb(186, 186, 178)
RUST = srgb(130, 66, 32)
RUST_DARK = srgb(74, 42, 27)
MANILA = srgb(162, 140, 100)
MANILA_DARK = srgb(118, 98, 66)
ENAMEL_GREEN = srgb(48, 78, 58)
ENAMEL_WHITE = srgb(222, 220, 208)
RUBBER = srgb(24, 24, 24)

for old in [s for s in bpy.data.scenes if s.name in (SCENE, 'SDT_TentPreview')]:
    for obj in list(old.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(old)
for mesh in [m for m in bpy.data.meshes if m.users == 0]:
    bpy.data.meshes.remove(mesh)
scene = bpy.data.scenes.new(SCENE)
previous = bpy.context.window.scene
bpy.context.window.scene = scene
tg.rng.seed(11)
objs = tg.build(scene.collection)

for obj in objs.values():
    obj.data.uv_layers.active = obj.data.uv_layers['UVMap']
    obj.data.uv_layers['UVMap'].active_render = True
canvas_side = {part: pack_sheets(objs[part], margin=.04) for part in ('CanvasOut', 'CanvasIn')}
for part, margin in (('Wood', .0008), ('Metal', .0015), ('Bulb', .004)):
    smart_uv(objs[part], margin)
atlas_m = {part: atlas_metres(objs[part]) for part in ('Wood', 'Metal')}
for obj in objs.values():
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.set_sharp_from_angle(angle=math.radians(35))


def canvas_material(inner):
    mat = bpy.data.materials.new('TentCanvasInBake' if inner else 'TentCanvasOutBake')
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    kind = g.attr('kind')
    strap, rolled = g.eq(kind, 1), g.eq(kind, 2)
    roof = g.ramp(nz if not inner else g.math('MULTIPLY', nz, -1.0), .3, .6)
    broad = g.noise(p, 1.1, 3, .5, 2.2)
    blotch = g.noise(p, 4.5, 5, .55, 5.3)
    grain = g.noise(p, 90, 4, .6, 1.9)
    # Per-panel dye lots, sun fade on the roof, chalky dust where rain does not reach.
    base = g.mix(g.add(g.mul(g.math('SUBTRACT', piece, .5), .6), g.mul(broad, .5)), OLIVE, OLIVE_DARK)
    if not inner:
        base = g.mix(g.mul(roof, g.add(.42, g.mul(broad, .16))), base, OLIVE_FADED)
    else:
        base = g.mix(.35, base, OLIVE_DARK)
    # Seams: a 6 cm reinforcement over each rafter and the cloth widths every 1.53 m, both double-stitched.
    seam = None
    for rx in tg.LEGS:
        d = g.math('ABSOLUTE', g.math('SUBTRACT', px, rx))
        m = g.ramp(d, .035, .028)
        seam = m if seam is None else g.math('MAXIMUM', seam, m)
    width = g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('DIVIDE', g.add(px, 4.6), 1.533)), .5))
    seam = g.math('MAXIMUM', seam, g.ramp(width, .485, .49))
    stitch = g.mul(seam, g.ramp(g.math('ABSOLUTE', g.math('SINE', g.mul(px, 600.0))), .7, .9))
    # Square repair patches sewn over a few tears.
    patch_cells = g.node('ShaderNodeTexVoronoi', Vector=p, Scale=1.4)
    patch_cells.voronoi_dimensions = '3D'
    patch_cells.distance = 'CHEBYCHEV'
    pick = g.ramp(g.xyz(patch_cells.outputs['Color'])[0], .9, .91)
    d = patch_cells.outputs['Distance']
    patch = g.mul(pick, g.ramp(d, .2, .19))
    patch_edge = g.mul(pick, g.mul(g.ramp(d, .2, .195), g.ramp(d, .18, .19)))
    # Water tide lines on the roof, mud and mildew low on the back wall, grime on hems and in folds.
    tide = g.mul(roof, g.mul(g.ramp(g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.mul(broad, 5.0)), .5)), .485, .5), .12 if not inner else 0.0))
    low = g.ramp(pz, .45, .03)
    mud = g.mul(low, g.ramp(g.add(blotch, g.mul(grain, .3)), .45, .75))
    mildew = g.mul(g.ramp(pz, .6, .1), g.ramp(g.noise(p, 30, 2, .5, 7.7), .7, .74))
    hem = g.mul(g.ramp(pz, tg.EAVE - .26, tg.EAVE - .31), g.ramp(pz, tg.EAVE - .4, tg.EAVE - .33))
    albedo = g.mix(g.mul(seam, .5), base, OLIVE_DARK)
    albedo = g.mix(g.mul(patch, .8), albedo, OLIVE_PATCH)
    albedo = g.mix(g.add(g.mul(stitch, .6), g.mul(patch_edge, .7)), albedo, OLIVE_DARK)
    albedo = g.mix(tide, albedo, OLIVE_DARK)
    albedo = g.mix(g.mul(rolled, .35), albedo, OLIVE_DARK)
    albedo = g.mix(g.mul(hem, .3), albedo, OLIVE_DARK)
    albedo = g.mix(strap, albedo, g.mix(g.ramp(g.math('SINE', g.mul(g.add(px, g.add(py, pz)), 900.0)), -.3, .3), WEBBING, OLIVE_DARK))
    albedo = g.mix(g.mul(mud, .85), albedo, MUD)
    albedo = g.mix(g.mul(mildew, .5), albedo, MILDEW)
    if not inner:
        albedo = g.mix(g.mul(roof, g.mul(g.ramp(broad, .4, .8), .12)), albedo, DUST)
    rough = g.add(.86, g.mul(grain, .1))
    height = g.add(g.mul(seam, .5), g.mul(stitch, .25), g.mul(patch, .35), g.mul(grain, .15), g.mul(strap, .3))
    return mat, finish(g, albedo, 0.0, rough, height, .25, .0015)


def wood_material():
    mat = bpy.data.materials.new('TentWoodBake')
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    axis = g.attr('axis', 'Vector')
    kind = g.attr('kind')
    along = g.vec('DOT_PRODUCT', p, axis)
    alongv = g.node('ShaderNodeCombineXYZ', X=along, Y=along, Z=along).outputs[0]
    perp = g.vec('SUBTRACT', p, g.vec('MULTIPLY', axis, alongv))
    offset = g.node('ShaderNodeCombineXYZ', X=g.mul(piece, 7.3), Y=g.mul(piece, 3.1), Z=g.mul(piece, 5.9)).outputs[0]
    space = g.vec('ADD', g.vec('ADD', g.vec('MULTIPLY', perp, (16, 16, 16)), g.vec('MULTIPLY', axis, g.vec('MULTIPLY', alongv, (.6, .6, .6)))), offset)
    figure = g.noise(space, 1.0, 5, .6, 2.0)
    rings = g.ramp(g.math('SINE', g.mul(g.add(g.mul(g.vec('DOT_PRODUCT', perp, (1.0, .8, .9)), 30), g.mul(figure, 6)), 6.2832)), .3, 1.0)
    saw = g.ramp(g.noise(g.vec('MULTIPLY', p, (70, 70, 70)), 1.0, 3, .6, 3.0), .4, .7)
    end_grain = g.ramp(g.math('ABSOLUTE', g.vec('DOT_PRODUCT', n, axis)), .8, .95)
    up = g.ramp(nz, .6, .9)
    # Pallets: fresh rough-sawn spruce, the older ones gone grey, some boards dirtier than others.
    spruce = g.mix(g.add(g.mul(rings, .6), g.mul(saw, .4)), SPRUCE, SPRUCE_DARK)
    spruce = g.mix(g.mul(g.ramp(piece, .6, .9), g.add(.45, g.mul(rings, .25))), spruce, GREY_WOOD)
    spruce = g.mix(g.mul(end_grain, .5), spruce, SPRUCE_DARK)
    chips = g.voronoi(p, 260, 'F1', 'Color')
    block = g.mix(g.ramp(g.xyz(chips)[0], .3, .7), CHIP_LIGHT, CHIP_DARK)
    plies = g.ramp(g.math('ABSOLUTE', g.math('SINE', g.mul(pz, 1100.0))), .3, .9)
    ply = g.mix(g.math('SUBTRACT', 1.0, g.ramp(nz, .6, .9)), g.mix(g.mul(rings, .4), PLY, SPRUCE_DARK), g.mix(plies, PLY_EDGE, PLY))
    albedo = g.mix(g.eq(kind, 1), spruce, block)
    albedo = g.mix(g.eq(kind, 2), albedo, ply)
    albedo = g.mix(g.eq(kind, 3), albedo, g.mix(g.mul(rings, .7), SPRUCE, SPRUCE_DARK))
    # Boots bring mud in at the front; dust settles on the deck.
    tracked = g.mul(g.mul(up, g.ramp(py, -.5, -2.5)), g.ramp(g.add(g.noise(p, 3, 3, .5, 1.1), g.mul(saw, .3)), .5, .8))
    albedo = g.mix(g.mul(tracked, .7), albedo, MUD)
    albedo = g.mix(g.mul(up, g.mul(g.ramp(g.noise(p, 5, 3, .5, 8.0), .4, .8), .2)), albedo, DUST)
    rough = g.add(.78, g.mul(saw, .14))
    height = g.add(g.mul(rings, .25), g.mul(saw, .45), g.mul(end_grain, g.mul(saw, .3)))
    return mat, finish(g, albedo, 0.0, rough, height, .3, .0015)


def metal_material():
    mat = bpy.data.materials.new('TentMetalBake')
    g = Graph(mat)
    p, n, px, py, pz, nx, ny, nz, piece, geo = common(g)
    kind = g.attr('kind')
    axis = g.attr('axis', 'Vector')
    blotch = g.noise(p, 6, 5, .55, 2.9)
    grain = g.noise(p, 80, 5, .6, 4.4)
    low = g.ramp(pz, .35, .02)
    zinc = g.mix(g.ramp(blotch, .3, .8), ZINC, ZINC_DULL)
    white = g.mul(g.ramp(g.add(blotch, g.mul(grain, .35)), .72, .84), .35)
    frame = g.mix(white, zinc, WHITE_RUST)
    frame = g.mix(g.ramp(g.add(g.mul(low, .9), g.mul(grain, .4)), .7, .9), frame, g.mix(g.ramp(grain, .3, .7), RUST, RUST_DARK))
    along = g.vec('DOT_PRODUCT', p, axis)
    lay = g.ramp(g.math('SINE', g.add(g.mul(along, 260.0), g.mul(g.noise(p, 40, 2, .5, 1.0), 4))), -.4, .6)
    rope = g.mix(lay, MANILA_DARK, MANILA)
    rope = g.mix(g.mul(low, .5), rope, MUD)
    stake = g.mix(g.ramp(grain, .3, .7), RUST, RUST_DARK)
    stake = g.mix(g.mul(low, .6), stake, MUD)
    # Shades: green enamel outside, white inside (normals toward the lamp's axis), chipped on the rim.
    lamp_x = g.mul(g.math('SIGN', px), tg.LAMPS[1])
    radial = g.node('ShaderNodeCombineXYZ', X=g.math('SUBTRACT', px, lamp_x), Y=py, Z=0.0).outputs[0]
    inner = g.ramp(g.vec('DOT_PRODUCT', n, radial), .0, -.002)
    shade = g.mix(inner, ENAMEL_GREEN, ENAMEL_WHITE)
    albedo = g.mix(g.eq(kind, 1), frame, rope)
    albedo = g.mix(g.eq(kind, 2), albedo, SPRUCE_DARK)
    albedo = g.mix(g.eq(kind, 3), albedo, stake)
    albedo = g.mix(g.eq(kind, 4), albedo, shade)
    albedo = g.mix(g.eq(kind, 5), albedo, RUBBER)
    is_frame = g.eq(kind, 0)
    metallic = g.mul(is_frame, .3)
    rough = g.mixf(g.eq(kind, 4), g.mixf(is_frame, .85, .5), .3)
    height = g.add(g.mul(g.eq(kind, 1), g.mul(lay, .6)), g.mul(grain, .15))
    return mat, finish(g, albedo, metallic, rough, height, .25, .0015)


out_mat, out_nodes = canvas_material(False)
in_mat, in_nodes = canvas_material(True)
wood_mat, wood_nodes = wood_material()
metal_mat, metal_nodes = metal_material()
for part, mat in (('CanvasOut', out_mat), ('CanvasIn', in_mat), ('Wood', wood_mat), ('Metal', metal_mat)):
    objs[part].data.materials.clear()
    objs[part].data.materials.append(mat)

bakekit.configure(scene=scene, res=RES, samples_ao=SAMPLES_AO, textures=TEXTURES, occlusion_floor=.4)
scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.use_denoising = False
scene.render.bake.margin = 8
if scene.world is None:
    scene.world = bpy.data.worlds.new('TentBakeWorld')
baked = {
    'Tent_CanvasOut': bake_atlas('Tent_CanvasOut', objs['CanvasOut'], [out_nodes], .5),
    'Tent_CanvasIn': bake_atlas('Tent_CanvasIn', objs['CanvasIn'], [in_nodes], 4.0),
    'Tent_Wood': bake_atlas('Tent_Wood', objs['Wood'], [wood_nodes], 3.0),
}
bakekit.configure(res=max(512, RES // 2))
baked['Tent_Metal'] = bake_atlas('Tent_Metal', objs['Metal'], [metal_nodes], 1.0)

for part, key in (('CanvasOut', 'Tent_CanvasOut'), ('CanvasIn', 'Tent_CanvasIn'), ('Wood', 'Tent_Wood'), ('Metal', 'Tent_Metal')):
    objs[part].data.materials.clear()
    objs[part].data.materials.append(final_material(key, baked[key]))
objs['Bulb'].data.materials.clear()
objs['Bulb'].data.materials.append(final_material('Tent_Bulb', None))

export = list(objs.values())
for obj in export:
    local = obj.data.uv_layers.get('Local')
    if local is not None:
        obj.data.uv_layers.remove(local)
activate(export)
bpy.ops.export_scene.fbx(filepath=str(MODELS / 'Tent.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, use_triangles=True, mesh_smooth_type='OFF',
                         add_leaf_bones=False, bake_anim=False)
(OUT / 'Tent.json').write_text(json.dumps({
    'canvas_atlas_m': canvas_side['CanvasOut'], 'canvas_in_atlas_m': canvas_side['CanvasIn'], 'weave_tile_m': .012,
    'legs_x': list(tg.LEGS), 'half_depth_m': tg.HD, 'eave_m': tg.EAVE, 'ridge_m': tg.RIDGE, 'overhang_m': tg.OVER,
    'pallet_top_m': tg.PALLET[2], 'table': list(tg.TABLE), 'table_top_m': tg.PALLET[2] + .758, 'lamps_x': list(tg.LAMPS),
    'lamp_z_m': tg.RIDGE - .6, 'atlas_m': atlas_m}, indent=1))
bpy.data.libraries.write(str(BLEND), {scene}, fake_user=True)
bpy.context.window.scene = previous
result = {'tris': {n: sum(len(p.vertices) - 2 for p in o.data.polygons) for n, o in objs.items()}, 'canvas_side': canvas_side, 'atlas_m': atlas_m, 'res': RES}
print(result)
