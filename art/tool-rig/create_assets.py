"""Original evolving tool rig (shovel -> drill -> nozzle); execute through Blender MCP.

Every part is one object named `L<from>[-<to>]_<Part>__<Material>`: it shows from tool level
<from> (through <to> when given). Parts whose name contains `Spin` rotate about their local forward
axis in Unity; their origin sits on that axis. Coordinates below are Unity metres (x right, y up,
z forward along the tool); the runtime rig scales the whole model down to sit inside the player.
"""
import bpy, math
from pathlib import Path
from mathutils import Vector

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
OUT = ROOT / 'unity/Assets/Content/ToolRig/Models'
OUT.mkdir(parents=True, exist_ok=True)
NAME = 'SDT_ToolRig'
previous = bpy.context.window.scene
if NAME in bpy.data.scenes:
    old = bpy.data.scenes[NAME]
    for obj in list(old.objects): bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(old)
scene = bpy.data.scenes.new(NAME)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'

COLOURS = {'Wood': (.42, .27, .15), 'Steel': (.55, .56, .57), 'DarkSteel': (.18, .19, .2), 'Paint': (.72, .43, .07),
           'Rubber': (.05, .05, .05), 'Battery': (.12, .16, .2), 'Cable': (.45, .08, .05)}
materials = {}
for key, colour in COLOURS.items():
    mat = bpy.data.materials.get('SDT_Rig_' + key) or bpy.data.materials.new('SDT_Rig_' + key)
    mat.diffuse_color = (*colour, 1)
    materials[key] = mat
parts = []

# Unity (x, y, z) -> Blender; with the baked FBX axis conversion below this lands unmirrored in Unity.
def point(v): return Vector((v[0], v[2], v[1]))

def finish(obj, name, smooth=False):
    base, material = name.split('__')
    count, unique = 1, name
    while unique in bpy.data.objects and bpy.data.objects[unique] is not obj:
        count += 1; unique = base + str(count) + '__' + material
    obj.name = unique
    obj.data.name = unique
    obj.data.materials.clear(); obj.data.materials.append(materials[material])
    if smooth:
        for poly in obj.data.polygons: poly.use_smooth = True
    parts.append(obj)
    return obj

def select(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True); bpy.context.view_layer.objects.active = obj

def box(name, centre, size, bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=point(centre))
    obj = bpy.context.object
    obj.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new('Edges', 'BEVEL'); mod.width = bevel; mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, name)

def cylinder(name, a, b, radius, radius_end=None, vertices=16, smooth=True):
    a, b = point(a), point(b)
    axis = b - a
    if radius_end is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=axis.length, location=(a + b) / 2)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius_end, depth=axis.length, location=(a + b) / 2)
    obj = bpy.context.object
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(axis.normalized())
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return finish(obj, name, smooth)

def tube(name, points, radius):
    curve = bpy.data.curves.new(name, 'CURVE'); curve.dimensions = '3D'
    curve.bevel_depth = radius; curve.bevel_resolution = 2; curve.use_fill_caps = True
    spline = curve.splines.new('POLY'); spline.points.add(len(points) - 1)
    for p, source in zip(spline.points, points): p.co = (*point(source), 1)
    obj = bpy.data.objects.new(name, curve); scene.collection.objects.link(obj)
    select(obj); bpy.ops.object.convert(target='MESH')
    return finish(bpy.context.object, name, True)

def blade(name, z0, z1, width, cup, thickness=.006):
    # A shovel blade: flat back, sides cupped up, tapering to a rounded point.
    rows, cols = 14, 9
    verts, faces = [], []
    for i in range(rows + 1):
        t = i / rows
        # Straight sides, then a round spade point; the cup flattens toward the tip.
        taper = 1 if t < .55 else max(.06, math.sqrt(max(0, 1 - ((t - .55) / .45) ** 2)))
        for j in range(cols + 1):
            s = j / cols * 2 - 1
            x = s * width * .5 * taper
            y = cup * s * s * (1 - .7 * t)
            verts.append(point((x, y, z0 + (z1 - z0) * t)))
    for i in range(rows):
        for j in range(cols):
            k = i * (cols + 1) + j
            faces.append((k, k + 1, k + cols + 2, k + cols + 1))
    mesh = bpy.data.meshes.new(name); mesh.from_pydata(verts, [], faces); mesh.update()
    obj = bpy.data.objects.new(name, mesh); scene.collection.objects.link(obj)
    select(obj)
    mod = obj.modifiers.new('Plate', 'SOLIDIFY'); mod.thickness = thickness; mod.offset = -1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, name, True)

def spin_bit(name, base, length, radius):
    # Fluted drill bit spinning about its own axis; origin on the base centre.
    cone = cylinder(name, base, (base[0], base[1], base[2] + length), radius, .003, 14)
    flutes = []
    for k in range(2):
        pts = []
        for i in range(25):
            t = i / 24
            angle = k * math.pi + t * 5.5 * math.pi
            r = radius * (1 - t) + .002
            pts.append((base[0] + math.cos(angle) * r, base[1] + math.sin(angle) * r, base[2] + t * length))
        flutes.append(tube(name.replace('__', 'Flute' + str(k) + '__'), pts, radius * .16))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in flutes: obj.select_set(True); parts.remove(obj)
    cone.select_set(True); bpy.context.view_layer.objects.active = cone
    bpy.ops.object.join()
    scene.cursor.location = point(base)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return cone

# Level 1: an ordinary shovel.
cylinder('L01_Shaft__Wood', (0, 0, .08), (0, 0, .86), .017)
box('L01_GripBar__Wood', (0, 0, .0), (.11, .026, .026))
box('L01_GripSideL__Wood', (-.05, 0, .045), (.018, .022, .09))
box('L01_GripSideR__Wood', (.05, 0, .045), (.018, .022, .09))
cylinder('L01_Socket__Steel', (0, 0, .8), (0, 0, .9), .021, .03)
blade('L01-02_Blade__Steel', .88, 1.16, .22, .035)
# Everything bolts on near the head, the part of the tool the first-person view shows.
# Level 2: taped shaft, reinforced edge.
for i, z in enumerate((.2, .74)):
    cylinder('L02_Tape' + str(i) + '__Rubber', (0, 0, z), (0, 0, z + .05), .0205)
box('L02-02_Edge__Steel', (0, .006, 1.125), (.12, .01, .014), .002)
# Level 3: wider blade with bolted side plates.
blade('L03-06_BladeWide__Steel', .88, 1.17, .29, .045)
for side in (-1, 1):
    box('L03_SidePlate' + ('L' if side < 0 else 'R') + '__Paint', (side * .148, .035, .99), (.008, .04, .16))
    for z in (.94, 1.04):
        cylinder('L03_Bolt__Steel', (side * .153, .035, z), (side * .158, .035, z), .006, None, 8)
# Level 4: a small motor and belt under the shaft, just behind the head.
cylinder('L04_Motor__Paint', (0, -.045, .64), (0, -.045, .77), .031)
for z in (.66, .69, .72):
    cylinder('L04_Fin__Steel', (0, -.045, z), (0, -.045, z + .008), .035)
tube('L04_Belt__Rubber', [(0, -.035, .77), (0, -.03, .81), (0, -.02, .86)], .005)
# Level 5: a strapped-on battery pack and its cable.
box('L05_Battery__Battery', (0, .045, .66), (.07, .05, .13))
for z in (.62, .7):
    box('L05_Strap__Rubber', (0, .03, z), (.078, .085, .014), .002)
tube('L05_Cable__Cable', [(0, .05, .725), (.035, .03, .74), (.035, -.03, .72), (0, -.045, .70)], .0045)
# Level 6: second battery, pipe frame, head guard.
box('L06_Battery2__Battery', (.062, .03, .66), (.045, .05, .12))
for side in (-1, 1):
    cylinder('L06_Pipe__Steel', (side * .045, .005, .5), (side * .06, .005, .86), .0065)
box('L06_PipeBrace__Steel', (0, .005, .8), (.13, .01, .012), .002)
box('L06_HeadGuard__Paint', (0, .045, .885), (.3, .05, .02))
# Level 7: the drill. A gearbox at the socket, a shorter scoop head and a spinning bit.
box('L07_Gearbox__Paint', (0, .005, .84), (.08, .08, .08), .008)
blade('L07_HeadScoop__Steel', .88, 1.05, .3, .05)
spin_bit('L07-08_SpinBit__Steel', (0, .03, .96), .26, .03)
# Level 8: twin motors and welded plates.
cylinder('L08_Motor2__Paint', (.05, -.04, .63), (.05, -.04, .77), .03)
for side in (-1, 1):
    box('L08_WeldPlate__DarkSteel', (side * .032, 0, .78), (.008, .05, .12), .002)
    for z in (.73, .76, .79, .82):
        cylinder('L08_Weld__DarkSteel', (side * .036, .026, z), (side * .036, .026, z + .012), .004, None, 6)
# Level 9: a bigger bit, a third battery and a cable bundle.
spin_bit('L09_SpinBitBig__Steel', (0, .035, .95), .34, .045)
box('L09_Battery3__Battery', (-.062, .03, .66), (.045, .05, .12))
for k, x in enumerate((-.012, 0, .012)):
    tube('L09_Cables' + str(k) + '__Cable', [(x, .075, .6), (x, .06, .7), (x, .05, .78), (x, .045, .83)], .004)
# Level 10: the nozzle. The shovel has become a cannon.
cylinder('L10_Nozzle__Paint', (0, .085, .55), (0, .085, 1.0), .036)
cylinder('L10_Muzzle__Steel', (0, .085, 1.0), (0, .085, 1.08), .036, .052)
for z in (.65, .8):
    box('L10_Clamp__DarkSteel', (0, .06, z), (.05, .07, .02), .002)
tube('L10_Hose__Rubber', [(-.04, .06, .58), (-.06, .09, .56), (-.03, .11, .54), (0, .09, .55)], .009)

bpy.ops.object.select_all(action='DESELECT')
for obj in parts: obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.export_scene.fbx(filepath=str(OUT / 'ToolRig.fbx'), use_selection=True, object_types={'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
    use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
bpy.context.view_layer.update()
bpy.data.libraries.write(str(ROOT / 'art/tool-rig/tool-rig.blend'), {scene}, fake_user=True)
bpy.context.window.scene = previous
result = {'parts': len(parts), 'names': sorted({p.name.split('__')[0] for p in parts})}
