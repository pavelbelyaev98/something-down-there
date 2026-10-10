"""Builds the workshop in its own scene with flat stand-in colours and renders preview shots (EEVEE) for form checks.
Run through Blender MCP; writes PNGs to PREVIEW_DIR. Leaves other scenes alone."""
import importlib
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ART = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there/art/camp-tent')
PREVIEW_DIR = Path(globals().get('PREVIEW_DIR', r'C:/Users/pavel/AppData/Local/Temp/claude/c--Users-pavel-Desktop-Dev-CompanyProjects-something-down-there/4d658b7b-8eaa-439d-a232-2fbd446a8f7a/scratchpad/blender'))
PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
SCENE = 'SDT_TentPreview'
if str(ART) not in sys.path:
    sys.path.insert(0, str(ART))
import tent_geometry as wg
importlib.reload(wg)

for old in [s for s in bpy.data.scenes if s.name == SCENE]:
    for obj in list(old.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(old)
scene = bpy.data.scenes.new(SCENE)
bpy.context.window.scene = scene
objs = wg.build(scene.collection)

COLOURS = {'CanvasOut': (.10, .11, .055, 1), 'CanvasIn': (.08, .085, .05, 1), 'Metal': (.55, .56, .55, 1),
           'Wood': (.45, .36, .24, 1), 'Bulb': (1, .9, .7, 1)}
for name, obj in objs.items():
    key = name
    mat = bpy.data.materials.get('Preview_' + key) or bpy.data.materials.new('Preview_' + key)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = COLOURS[key]
    bsdf.inputs['Roughness'].default_value = .6
    bsdf.inputs['Metallic'].default_value = .3 if key == 'Metal' else 0
    if COLOURS[key][3] < 1:
        bsdf.inputs['Alpha'].default_value = COLOURS[key][3]
        mat.surface_render_method = 'BLENDED'
    if key == 'Bulb':
        bsdf.inputs['Emission Color'].default_value = (1, 1, .95, 1)
        bsdf.inputs['Emission Strength'].default_value = 8
    obj.data.materials.clear()
    obj.data.materials.append(mat)

# Ground, sun, sky.
bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, 0))
ground = bpy.context.active_object
ground.name = 'PreviewGround'
gm = bpy.data.materials.new('PreviewGround')
gm.use_nodes = True
gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.36, .25, .15, 1)
ground.data.materials.append(gm)
sun_data = bpy.data.lights.new('PreviewSun', 'SUN')
sun_data.energy = 4
sun_data.angle = math.radians(1.5)
sun = bpy.data.objects.new('PreviewSun', sun_data)
scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(globals().get('SUN_TILT', 10)), 0, math.radians(30))
world = bpy.data.worlds.get('PreviewWorld') or bpy.data.worlds.new('PreviewWorld')
world.use_nodes = True
bg = world.node_tree.nodes['Background']
bg.inputs['Color'].default_value = (.45, .6, .9, 1)
bg.inputs['Strength'].default_value = .8
scene.world = world
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.eevee.taa_render_samples = 32
try:
    scene.eevee.use_shadows = True
    scene.eevee.use_raytracing = True
except AttributeError:
    pass
scene.view_settings.view_transform = 'AgX'

cam_data = bpy.data.cameras.new('PreviewCam')
cam = bpy.data.objects.new('PreviewCam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
shots = globals().get('SHOTS', {
    'front': ((1.0, -12, 1.7), (0, 0, 1.6), 55),
    'corner': ((-10, -8, 2.0), (0, 0, 1.5), 55),
    'back': ((8, 10, 3.0), (0, 0, 1.4), 55),
    'inside': ((1.5, -2.0, 1.7), (-2.0, 2.4, 1.2), 75),
    'roof': ((-7, -9, 8), (0, 0, 2.0), 50),
    'close': ((3.6, -3.4, 1.5), (4.5, -2.4, 1.9), 60),
})
rendered = []
for name, (pos, look, fov) in shots.items():
    cam.location = Vector(pos)
    direction = Vector(look) - Vector(pos)
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    cam_data.angle = math.radians(fov)
    scene.render.filepath = str(PREVIEW_DIR / f'tent-{name}.png')
    bpy.ops.render.render(write_still=True, scene=scene.name)
    rendered.append(scene.render.filepath)
result = {'rendered': rendered, 'tris': {n: sum(len(p.vertices) - 2 for p in o.data.polygons) for n, o in objs.items()}}
