"""Original swivel lifting eye with baked worn-paint, dirt and steel maps. Run in Blender (Cycles bakes)
through MCP; preserves other scenes.

Blender axes: Z is the surface normal. Both objects have their origin at the centre of the base on the
load's surface. "Base" stays bolted flat on the load; "Swivel" (ball-ended body and forged bow) turns
about that origin toward the hook, so the hook's seat stays exactly RecoveryMarkView.HookReach from it.
The bow lies in the XZ plane, so its hole runs along Y (Unity's local Z), and its inner top is 0.21 m up.
"""
import bmesh
import bpy
import math
import numpy as np
from mathutils import Vector
from pathlib import Path

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
MODELS = ROOT / 'unity/Assets/Content/Salvage/Models'
TEXTURES = ROOT / 'unity/Assets/Content/Salvage/Textures'
MODELS.mkdir(parents=True, exist_ok=True)
TEXTURES.mkdir(parents=True, exist_ok=True)
BLEND = ROOT / 'art/lifting-eye/lifting-eye.blend'
SCENE = 'SDT_LiftingEye'
RES = 1024

# Bow: tube radius and the centre of its round top; inner top = TOP_Z + BOW_R - TUBE = 0.21 m.
TUBE = .013
BOW_R = .056
TOP_Z = .21 + TUBE - BOW_R

for old in [s for s in bpy.data.scenes if s.name == SCENE]:
    for obj in list(old.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(old)
for name in ('LiftingEyePaintBake', 'LiftingEyeSteelBake', 'LiftingEye'):
    if name in bpy.data.materials:
        bpy.data.materials.remove(bpy.data.materials[name])
for name in ('LiftingEye_Albedo', 'LiftingEye_Normal', 'LiftingEye_Metal', 'LiftingEye_Rough', 'LiftingEye_AO', 'LiftingEye_Mask'):
    if name in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[name])
scene = bpy.data.scenes.new(SCENE)
previous = bpy.context.window.scene
bpy.context.window.scene = scene


# ---------------------------------------------------------------- geometry
def lathe(bm, profile, segments=48, closed=False):
    """Spins an (r, z) profile about Z. Points with r == 0 become poles."""
    rings = []
    for r, z in profile:
        if r <= 0:
            rings.append([bm.verts.new((0, 0, z))] * segments)
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segments), r * math.sin(2 * math.pi * i / segments), z))
                          for i in range(segments)])
    count = len(profile) if closed else len(profile) - 1
    for j in range(count):
        a, b = rings[j], rings[(j + 1) % len(profile)]
        for i in range(segments):
            k = (i + 1) % segments
            quad = [a[i], a[k], b[k], b[i]]
            unique = []
            for v in quad:
                if v not in unique:
                    unique.append(v)
            if len(unique) >= 3:
                bm.faces.new(unique)


def mesh_object(name, bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


def hex_bolt(bm, centre, radius, height, base_z):
    washer = [(radius * 1.25, base_z), (radius * 1.25, base_z + .0015), (0, base_z + .0015)]
    head = []
    verts_bottom, verts_top = [], []
    for i in range(6):
        a = math.pi / 6 + i * math.pi / 3
        x, y = centre[0] + radius * math.cos(a), centre[1] + radius * math.sin(a)
        verts_bottom.append(bm.verts.new((x, y, base_z + .0015)))
        verts_top.append(bm.verts.new((x, y, base_z + .0015 + height)))
    bm.faces.new(list(reversed(verts_bottom)))
    bm.faces.new(verts_top)
    for i in range(6):
        k = (i + 1) % 6
        bm.faces.new([verts_bottom[i], verts_bottom[k], verts_top[k], verts_top[i]])
    # Washer under the head.
    ring = []
    for i in range(24):
        a = 2 * math.pi * i / 24
        ring.append((centre[0] + radius * 1.3 * math.cos(a), centre[1] + radius * 1.3 * math.sin(a)))
    low = [bm.verts.new((x, y, base_z - .001)) for x, y in ring]
    high = [bm.verts.new((x, y, base_z + .0015)) for x, y in ring]
    bm.faces.new(list(reversed(low)))
    bm.faces.new(high)
    for i in range(24):
        k = (i + 1) % 24
        bm.faces.new([low[i], low[k], high[k], high[i]])


# Base: a flange plate with a low cup that seats the swivel's ball, held down by three bolts. The cup
# stays low so the ball and bow can turn to any side without cutting into it.
BALL = .036
bm = bmesh.new()
lathe(bm, [(BALL + .0012, -.006), (.071, -.006), (.073, -.004), (.073, .006), (.071, .009), (.050, .009),
           (.048, .011), (.046, .015), (.042, .017), (BALL + .002, .0165), (BALL + .0012, .014)], closed=True)
for angle in (30, 150, 270):
    a = math.radians(angle)
    hex_bolt(bm, (.0605 * math.cos(a), .0605 * math.sin(a)), .0072, .0055, .009)
base = mesh_object('Base', bm)

# Swivel ball: centred on the origin, so turning it never changes what the cup sees.
bm = bmesh.new()
lathe(bm, [(BALL * math.cos(math.radians(a)), BALL * math.sin(math.radians(a))) for a in range(-90, 91, 10)], segments=40)
body = mesh_object('Body', bm)

# Bow: one forged round bar, legs rising out of the ball and flaring into a round top.
def bezier(p0, p1, p2, p3, n):
    pts = []
    for i in range(n):
        t = i / (n - 1)
        u = 1 - t
        pts.append(tuple(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d for a, b, c, d in zip(p0, p1, p2, p3)))
    return pts

left = bezier((-.024, .02), (-.03, .058), (-BOW_R, .072), (-BOW_R, TOP_Z), 20)
arc = [(-BOW_R * math.cos(math.radians(a)), TOP_Z + BOW_R * math.sin(math.radians(a))) for a in range(6, 180, 6)]
right = [(-x, z) for x, z in reversed(left)]
path = left + arc + right
curve = bpy.data.curves.new('BowPath', 'CURVE')
curve.dimensions = '3D'
curve.bevel_depth = TUBE
curve.bevel_resolution = 4
curve.use_fill_caps = True
spline = curve.splines.new('POLY')
spline.points.add(len(path) - 1)
for point, (x, z) in zip(spline.points, path):
    point.co = (x, 0, z, 1)
bow = bpy.data.objects.new('Bow', curve)
scene.collection.objects.link(bow)
bpy.ops.object.select_all(action='DESELECT')
bow.select_set(True)
bpy.context.view_layer.objects.active = bow
bpy.ops.object.convert(target='MESH')
bow = bpy.context.object

# Join body and bow into the swivel.
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bow.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
swivel = bpy.context.object
swivel.name = 'Swivel'

for obj in (base, swivel):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    mod = obj.modifiers.new('Triangulate', 'TRIANGULATE')
    mod.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=mod.name)

# ---------------------------------------------------------------- UVs
bpy.ops.object.select_all(action='DESELECT')
for obj in (base, swivel):
    obj.select_set(True)
bpy.context.view_layer.objects.active = swivel
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.01, scale_to_bounds=False)
bpy.ops.uv.pack_islands(margin=.008, rotate=True)
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- bake shaders
def lin(r, g, b):
    def c(v):
        return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4
    return (c(r), c(g), c(b), 1)

# Amber powder coat, bare steel, zinc plating, dry dust, caked mud and rust (sRGB picks).
PAINT = lin(.76, .58, .16)
PAINT_FADED = lin(.72, .60, .34)
STEEL = lin(.70, .69, .67)
ZINC = lin(.68, .69, .68)
ZINC_DARK = lin(.55, .56, .56)
DUST = lin(.60, .50, .38)
MUD = lin(.46, .35, .25)
RUST = lin(.45, .24, .11)


class Graph:
    def __init__(self, mat):
        self.tree = mat.node_tree
        self.tree.nodes.clear()
        self.x = 0

    def node(self, kind, **inputs):
        n = self.tree.nodes.new(kind)
        n.location = (self.x, 0)
        self.x += 180
        for key, value in inputs.items():
            if isinstance(value, bpy.types.NodeSocket):
                self.tree.links.new(value, n.inputs[key])
            else:
                n.inputs[key].default_value = value
        return n

    def math(self, op, a, b=0.0, clamp=False):
        n = self.node('ShaderNodeMath')
        n.operation = op
        n.use_clamp = clamp
        for i, v in enumerate((a, b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.tree.links.new(v, n.inputs[i])
            else:
                n.inputs[i].default_value = v
        return n.outputs[0]

    def ramp(self, value, lo, hi):
        n = self.node('ShaderNodeMapRange')
        n.clamp = True
        self.tree.links.new(value, n.inputs['Value'])
        n.inputs['From Min'].default_value = lo
        n.inputs['From Max'].default_value = hi
        return n.outputs[0]

    def mix(self, factor, a, b, kind='RGBA'):
        n = self.node('ShaderNodeMix')
        n.data_type = kind
        suffix = 'Color' if kind == 'RGBA' else 'Float'
        def socket(sockets, identifier):
            return next(s for s in sockets if s.identifier == identifier)
        self.tree.links.new(factor, socket(n.inputs, 'Factor_Float'))
        for name, v in (('A_' + suffix, a), ('B_' + suffix, b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.tree.links.new(v, socket(n.inputs, name))
            else:
                socket(n.inputs, name).default_value = v
        return socket(n.outputs, 'Result_' + suffix)

    def mixf(self, factor, a, b):
        return self.mix(factor, a, b, 'FLOAT')

    def noise(self, vector, scale, detail=6, roughness=.6, w=0.0):
        n = self.node('ShaderNodeTexNoise')
        n.noise_dimensions = '4D'
        self.tree.links.new(vector, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = roughness
        n.inputs['W'].default_value = w
        return n.outputs['Fac']


def bake_material(name, painted):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    g = Graph(mat)
    coords = g.node('ShaderNodeTexCoord').outputs['Object']
    z = g.node('ShaderNodeSeparateXYZ', Vector=coords).outputs['Z']
    geometry = g.node('ShaderNodeNewGeometry')
    up = g.ramp(g.node('ShaderNodeSeparateXYZ', Vector=geometry.outputs['Normal']).outputs['Z'], .1, .9)
    ao = g.node('ShaderNodeAmbientOcclusion', Distance=.012)
    ao.samples = 16
    crevice = g.math('SUBTRACT', 1.0, ao.outputs['AO'])
    convex = g.ramp(geometry.outputs['Pointiness'], .5, .56)
    grain = g.noise(coords, 140, 8, .65, 1.3)
    blotch = g.noise(coords, 30, 5, .55, 4.1)
    broad = g.noise(coords, 9, 3, .5, 6.2)
    fine = g.noise(coords, 900, 2, .5, 7.7)
    wave = g.node('ShaderNodeTexWave', Vector=coords, Scale=55, Distortion=9, Detail=6)
    wave.wave_profile = 'SIN'
    scratches = g.math('MULTIPLY', g.ramp(wave.outputs['Fac'], .975, .995), g.ramp(g.noise(coords, 18, 2, .5, 3.3), .56, .66))
    # Dry dust settles on upward faces everywhere; mud cakes in crevices and low down.
    film = g.math('MULTIPLY', g.math('ADD', .55, g.math('MULTIPLY', up, .45)), g.ramp(broad, .15, .65))
    stretched = g.node('ShaderNodeVectorMath', Vector=coords)
    stretched.operation = 'MULTIPLY'
    stretched.inputs[1].default_value = (1, 1, .1)
    streaks = g.math('MULTIPLY', g.ramp(g.noise(stretched.outputs[0], 60, 4, .6, 9.2), .5, .75), .45)
    low = g.ramp(z, .075 if painted else .02, .0)
    mud_amount = g.math('ADD', g.math('MULTIPLY', crevice, 2.4), g.math('MULTIPLY', low, 1.2 if painted else .45))
    mud = g.ramp(g.math('MULTIPLY', mud_amount, g.math('ADD', blotch, .2)), .38, .6)
    if painted:
        # Powder coat knocked back to steel on the most exposed curves and in a few dings.
        dings = g.ramp(g.noise(coords, 48, 3, .5, 2.6), .735, .755)
        chips = g.ramp(g.math('ADD', g.math('MULTIPLY', convex, .5), g.math('MULTIPLY', grain, .75)), .85, .89)
        chips = g.math('MAXIMUM', chips, dings)
        paint = g.mix(g.ramp(blotch, .3, .8), PAINT, PAINT_FADED)
        surface = g.mix(chips, paint, STEEL)
        surface = g.mix(g.math('MULTIPLY', scratches, .8), surface, STEEL)
        bare = g.math('MAXIMUM', chips, g.math('MULTIPLY', scratches, .8))
        metallic = g.math('MULTIPLY', bare, .35)
        rough = g.mixf(bare, g.math('ADD', .56, g.math('MULTIPLY', grain, .12)), .45)
        relief = g.math('ADD', g.math('MULTIPLY', g.math('SUBTRACT', 1.0, bare), .45), g.math('MULTIPLY', fine, .05))
    else:
        # Zinc plating, mottled, with rust creeping out of the crevices around the bolts.
        rust = g.ramp(g.math('ADD', g.math('MULTIPLY', crevice, 1.8), g.math('MULTIPLY', grain, .55)), .6, .78)
        zinc = g.mix(g.ramp(blotch, .3, .8), ZINC, ZINC_DARK)
        surface = g.mix(rust, zinc, RUST)
        metallic = g.math('MULTIPLY', g.math('SUBTRACT', 1.0, rust), .3)
        rough = g.mixf(rust, g.math('ADD', .6, g.math('MULTIPLY', grain, .14)), .85)
        relief = g.math('ADD', g.math('MULTIPLY', rust, .25), g.math('MULTIPLY', fine, .08))
    albedo = g.mix(g.math('MULTIPLY', film, .75), surface, DUST)
    albedo = g.mix(streaks, albedo, MUD)
    albedo = g.mix(mud, albedo, g.mix(g.math('MULTIPLY', grain, .7), MUD, DUST))
    metallic = g.math('MULTIPLY', metallic, g.math('MULTIPLY', g.math('SUBTRACT', 1.0, mud), g.math('SUBTRACT', 1.0, g.math('MULTIPLY', film, .5))))
    rough = g.mixf(g.math('MULTIPLY', film, .6), rough, .9)
    rough = g.mixf(mud, rough, .95)
    dents = g.noise(coords, 14, 2, .5, 5.5)
    relief = g.math('ADD', relief, g.math('MULTIPLY', dents, .12))
    relief = g.math('ADD', relief, g.math('MULTIPLY', mud, g.math('ADD', .4, g.math('MULTIPLY', grain, .5))))
    bump = g.node('ShaderNodeBump', Strength=.4, Distance=.0012, Height=relief)
    shader = g.node('ShaderNodeBsdfPrincipled', **{'Base Color': albedo, 'Metallic': metallic, 'Roughness': rough,
                                                   'Normal': bump.outputs['Normal']})
    emission = g.node('ShaderNodeEmission', Strength=1.0)
    output = g.node('ShaderNodeOutputMaterial')
    image = g.node('ShaderNodeTexImage')
    g.tree.nodes.active = image
    return mat, {'albedo': albedo, 'metal': metallic, 'rough': rough}, shader, emission, output, image


paint_mat, paint_out, paint_bsdf, paint_emit, paint_output, paint_image = bake_material('LiftingEyePaintBake', True)
steel_mat, steel_out, steel_bsdf, steel_emit, steel_output, steel_image = bake_material('LiftingEyeSteelBake', False)
swivel.data.materials.clear()
swivel.data.materials.append(paint_mat)
base.data.materials.clear()
base.data.materials.append(steel_mat)

scene.render.engine = 'CYCLES'
scene.cycles.device = 'CPU'
scene.cycles.samples = 48
scene.cycles.use_denoising = False
scene.render.bake.margin = 8
if scene.world is None:
    scene.world = bpy.data.worlds.new('LiftingEyeWorld')
scene.world.light_settings.distance = .03

materials = [(paint_mat, paint_out, paint_bsdf, paint_emit, paint_output, paint_image),
             (steel_mat, steel_out, steel_bsdf, steel_emit, steel_output, steel_image)]


def new_image(name, color):
    image = bpy.data.images.new(name, RES, RES, alpha=False, float_buffer=True)
    image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
    return image


def bake(kind, image, channel=None):
    for mat, out, bsdf, emit, output, node in materials:
        links = mat.node_tree.links
        for link in list(output.inputs['Surface'].links):
            links.remove(link)
        if channel is None:
            links.new(bsdf.outputs[0], output.inputs['Surface'])
        else:
            links.new(out[channel], emit.inputs['Color'])
            links.new(emit.outputs[0], output.inputs['Surface'])
        node.image = image
        mat.node_tree.nodes.active = node
    bpy.ops.object.select_all(action='DESELECT')
    for obj in (base, swivel):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = swivel
    if kind == 'NORMAL':
        bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', margin=8, use_clear=True)
    else:
        bpy.ops.object.bake(type=kind, margin=8, use_clear=True)


albedo = new_image('LiftingEye_Albedo', True)
metal = new_image('LiftingEye_Metal', False)
rough = new_image('LiftingEye_Rough', False)
occlusion = new_image('LiftingEye_AO', False)
normal = new_image('LiftingEye_Normal', False)
bake('EMIT', albedo, 'albedo')
bake('EMIT', metal, 'metal')
bake('EMIT', rough, 'rough')
scene.cycles.samples = 128
bake('AO', occlusion)
scene.cycles.samples = 16
bake('NORMAL', normal)


def pixels(image):
    data = np.empty(RES * RES * 4, dtype=np.float32)
    image.pixels.foreach_get(data)
    return data.reshape(RES, RES, 4)


# Unity's Lit mask: metallic in R, occlusion in G (also the occlusion map), smoothness in A.
mask = np.zeros((RES, RES, 4), dtype=np.float32)
mask[..., 0] = pixels(metal)[..., 0]
mask[..., 1] = np.clip(.35 + .65 * pixels(occlusion)[..., 0], 0, 1)
mask[..., 3] = 1 - pixels(rough)[..., 0]
packed = bpy.data.images.new('LiftingEye_Mask', RES, RES, alpha=True, float_buffer=True)
packed.colorspace_settings.name = 'Non-Color'
packed.pixels.foreach_set(mask.ravel())


def save(image, name):
    image.filepath_raw = str(TEXTURES / name)
    image.file_format = 'PNG'
    image.save(filepath=str(TEXTURES / name))


save(albedo, 'LiftingEye_Albedo.png')
save(normal, 'LiftingEye_Normal.png')
save(packed, 'LiftingEye_Mask.png')

# ---------------------------------------------------------------- export
final = bpy.data.materials.new('LiftingEye')
final.use_nodes = True
tree = final.node_tree
bsdf = tree.nodes.get('Principled BSDF')
for image, socket, non_color in ((albedo, 'Base Color', False), (packed, None, True)):
    tex = tree.nodes.new('ShaderNodeTexImage')
    tex.image = image
    if socket:
        tree.links.new(tex.outputs['Color'], bsdf.inputs[socket])
    else:
        split = tree.nodes.new('ShaderNodeSeparateColor')
        tree.links.new(tex.outputs['Color'], split.inputs['Color'])
        tree.links.new(split.outputs['Red'], bsdf.inputs['Metallic'])
        invert = tree.nodes.new('ShaderNodeMath')
        invert.operation = 'SUBTRACT'
        invert.inputs[0].default_value = 1
        tree.links.new(tex.outputs['Alpha'], invert.inputs[1])
        tree.links.new(invert.outputs[0], bsdf.inputs['Roughness'])
ntex = tree.nodes.new('ShaderNodeTexImage')
ntex.image = normal
nmap = tree.nodes.new('ShaderNodeNormalMap')
tree.links.new(ntex.outputs['Color'], nmap.inputs['Color'])
tree.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
for obj in (base, swivel):
    obj.data.materials.clear()
    obj.data.materials.append(final)

bpy.ops.object.select_all(action='DESELECT')
for obj in (base, swivel):
    obj.select_set(True)
bpy.context.view_layer.objects.active = swivel
bpy.ops.export_scene.fbx(filepath=str(MODELS / 'LiftingEye.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, use_triangles=True, mesh_smooth_type='OFF',
                         add_leaf_bones=False, bake_anim=False)
bpy.data.libraries.write(str(BLEND), {scene}, fake_user=True)
bpy.context.window.scene = previous
result = {'base_tris': len(base.data.polygons), 'swivel_tris': len(swivel.data.polygons),
          'inner_top': TOP_Z + BOW_R - TUBE, 'textures': str(TEXTURES)}
print(result)
