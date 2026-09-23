"""Original recovery fixtures. Run in Blender through MCP; preserves other scenes."""
import bpy
import math
from pathlib import Path

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
OUT = ROOT / 'unity/Assets/Content/Salvage/Models'
OUT.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('SDT_RecoveryFixtures')
previous = bpy.context.window.scene
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
groups = {}

def box(name, location, scale):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new('Rounded steel edges', 'BEVEL')
    bevel.width = .025
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    return obj

def cylinder(name, location, radius, depth, rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return obj

groups['WinchFixture'] = [box('Winch base', (0,0,.13), (1.5,1.05,.26)),
    box('Left bearing', (-.55,0,.65), (.16,.7,1)), box('Right bearing', (.55,0,.65), (.16,.7,1)),
    cylinder('Cable drum', (0,0,.76), .34,.95,(0,math.pi/2,0)),
    cylinder('Left flange', (-.46,0,.76), .44,.06,(0,math.pi/2,0)),
    cylinder('Right flange', (.46,0,.76), .44,.06,(0,math.pi/2,0)),
    box('Guide upright', (0,.33,1.45), (.16,.16,1.5)),
    box('Guide arm', (0,.08,2.18), (.2,.7,.2)),
    cylinder('Guide pulley', (0,-.2,2.06), .18,.14,(0,math.pi/2,0))]
for i in range(15):
    groups['WinchFixture'].append(cylinder('Drum wrap', (-.42+i*.06,0,.76), .355,.025,(0,math.pi/2,0)))
groups['RecoveryPad'] = [box('Recovery deck',(0,0,.12),(2.4,1.7,.24)),
    box('Left skid',(-.92,0,.27),(.08,1.7,.1)), box('Right skid',(.92,0,.27),(.08,1.7,.1))]
groups['ExhibitStand'] = [box('Exhibit base',(0,0,.06),(1.55,1.1,.12)),
    box('Pedestal',(0,0,.5),(.72,.55,.85)), box('Exhibit top',(0,0,.98),(1.55,1.1,.14)),
    box('Card plate',(0,-.56,.82),(.65,.05,.25))]
# Open, readable hook made from a curved tubular mesh; no rope physics dependency.
curve=bpy.data.curves.new('Hook curve','CURVE')
curve.dimensions='3D'; curve.bevel_depth=.026; curve.bevel_resolution=3
spline=curve.splines.new('POLY'); spline.points.add(20)
for i,point in enumerate(spline.points):
    angle=math.radians(-55+i*285/20)
    point.co=(.095*math.cos(angle),0,.095*math.sin(angle),1)
hook=bpy.data.objects.new('Recovery hook',curve); scene.collection.objects.link(hook)
bpy.ops.object.select_all(action='DESELECT'); hook.select_set(True); bpy.context.view_layer.objects.active=hook
bpy.ops.object.convert(target='MESH')
groups['Hook']=[bpy.context.object,box('Hook shank',(0,0,.16),(.055,.055,.16))]

for name,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.object; obj.name=name
    scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,
        object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',
        use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False)
    obj.location.x=list(groups).index(name)*3

bpy.context.view_layer.update()
bpy.data.libraries.write(str(ROOT/'art/salvage-winch/recovery-fixtures.blend'), {scene}, fake_user=True)
bpy.context.window.scene=previous
result={'models':list(groups), 'blend':str(ROOT/'art/salvage-winch/recovery-fixtures.blend')}
