"""Original swivel lifting eye. Run in Blender through MCP; preserves other scenes.

Blender axes: Z is the surface normal. The origin is the centre of the base plate on the surface; the
crane's hook seats at the inner top of the ring (about 20 cm up), sized for the crane's hook.
"""
import bpy
import math
from pathlib import Path

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
OUT = ROOT / 'unity/Assets/Content/Salvage/Models'
OUT.mkdir(parents=True, exist_ok=True)
BLEND = ROOT / 'art/lifting-eye/lifting-eye.blend'
SCENE = 'SDT_LiftingEye'
SIZE = 2.4

for old in [s for s in bpy.data.scenes if s.name == SCENE]:
    for obj in list(old.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(old)
scene = bpy.data.scenes.new(SCENE)
previous = bpy.context.window.scene
bpy.context.window.scene = scene


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    return mat


# Unity remaps these by name to project-owned URP materials (SalvageCraneSetup).
PAINT = material('LiftingEyePaint', (.85, .6, .08), 0, .5)
STEEL = material('LiftingEyeSteel', (.5, .51, .53), 1, .45)


def finish(obj, name, mat):
    obj.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


parts = []
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.035 * SIZE, depth=.012 * SIZE, location=(0, 0, .006 * SIZE))
parts.append(finish(bpy.context.object, 'e base', STEEL))
bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=.022 * SIZE, depth=.022 * SIZE, location=(0, 0, .023 * SIZE))
parts.append(finish(bpy.context.object, 'e collar', STEEL))
bpy.ops.mesh.primitive_torus_add(major_radius=.03 * SIZE, minor_radius=.008 * SIZE, major_segments=28, minor_segments=10,
                                 location=(0, 0, .065 * SIZE), rotation=(math.pi / 2, 0, 0))
ring = bpy.context.object
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
parts.append(finish(ring, 'e ring', PAINT))
bpy.ops.object.select_all(action='DESELECT')
for part in parts:
    part.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
eye = bpy.context.object
eye.name = 'Lifting Eye'
scene.cursor.location = (0, 0, 0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.export_scene.fbx(filepath=str(OUT / 'LiftingEye.fbx'), use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
bpy.data.libraries.write(str(BLEND), {scene}, fake_user=True)
bpy.context.window.scene = previous
print({'eye': eye.name, 'blend': str(BLEND)})
