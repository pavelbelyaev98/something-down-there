"""Original portable work light and navigation stencils; execute through Blender MCP."""
import bpy
from pathlib import Path
from mathutils import Vector

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
OUT = ROOT / 'unity/Assets/Content/WorksiteTools/Models'
OUT.mkdir(parents=True, exist_ok=True)
previous = bpy.context.window.scene
scene = bpy.data.scenes.new('SDT_WorksiteTools')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1

def point(v): return (v[0], -v[2], v[1])
# Set ONLY = {'WorkLamp'} before running to re-export a subset without touching the others.
ONLY = globals().get('ONLY') or {'WorkLamp', 'Arrow', 'Home', 'ReturnHere'}

def finish(obj, name, smooth=True):
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if smooth:
        try: bpy.ops.object.shade_smooth_by_angle(angle=0.7)
        except Exception: bpy.ops.object.shade_smooth()
    return obj

def cylinder(name, y, height, radius, bevel=.003):
    bpy.ops.mesh.primitive_cylinder_add(vertices=28, radius=radius, depth=height, location=point((0, y, 0)))
    obj = finish(bpy.context.object, name)
    mod = obj.modifiers.new('Soft edges', 'BEVEL'); mod.width = bevel; mod.segments = 2
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

def ring(name, y, major, minor, rotation=(0, 0, 0), keep_above=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=36, minor_segments=8,
        location=point((0, y, 0)), rotation=rotation)
    obj = finish(bpy.context.object, name)
    if keep_above is not None:
        import bmesh
        mesh = bmesh.new(); mesh.from_mesh(obj.data)
        cut = keep_above - obj.location.z
        bmesh.ops.delete(mesh, geom=[v for v in mesh.verts if v.co.z < cut], context='VERTS')
        mesh.to_mesh(obj.data); mesh.free()
    return obj

# Hand-sized puck work light: rubber foot, yellow housing, frosted dome in a steel guard with a
# hanging loop, and a spike under the base that reads as jabbed into the ground. Base at y = 0.
parts = [cylinder('Rubber foot', .006, .012, .054),
    cylinder('Yellow housing', .029, .034, .05, .004)]
bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=14, radius=.041, location=point((0, .062, 0)))
lens = finish(bpy.context.object, 'Lens')
for v in lens.data.vertices: v.co.z = max(v.co.z, -.017)   # flat bottom seated in the housing
parts.append(lens)
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=.0055, location=point((0, .03, .049)))
parts.append(finish(bpy.context.object, 'Status lens'))
parts.append(ring('Steel guard ring', .064, .045, .0032))
parts.append(ring('Steel guard arc', .062, .046, .0028, rotation=(1.5708, 0, 0), keep_above=.06))
parts.append(ring('Steel guard arc', .062, .046, .0028, rotation=(1.5708, 0, 1.5708), keep_above=.06))
parts.append(ring('Steel loop', .117, .009, .0024, rotation=(1.5708, 0, 0)))
bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0, radius2=.011, depth=.055, location=point((0, -.0275, 0)))
parts.append(finish(bpy.context.object, 'Steel spike'))

def export(name, objects):
    if name not in ONLY: return
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,
        object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',
        use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False)

export('WorkLamp', parts)

def stencil(name, segments):
    verts=[]; faces=[]
    for a,b,width in segments:
        a=Vector(a); b=Vector(b); d=(b-a).normalized(); side=Vector((-d.y,d.x))*width*.5
        start=len(verts)
        for p in (a-side,b-side,b+side,a+side): verts.append(point((p.x,p.y,0)))
        faces.append((start,start+1,start+2,start+3))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); scene.collection.objects.link(obj)
    export(name,[obj])

stencil('Arrow', [((0,-.22),(0,.20),.065), ((0,.20),(-.15,.04),.065), ((0,.20),(.15,.04),.065)])
stencil('Home', [((-.21,.015),(0,.21),.055), ((0,.21),(.21,.015),.055),
    ((-.15,.035),(-.15,-.19),.05), ((.15,.035),(.15,-.19),.05), ((-.15,-.19),(.15,-.19),.05),
    ((-.035,-.19),(-.035,-.055),.045), ((-.035,-.055),(.05,-.055),.045), ((.05,-.055),(.05,-.19),.045)])
stencil('ReturnHere', [((-.18,.17),(.16,.17),.055), ((.16,.17),(.16,-.12),.055),
    ((.16,-.12),(-.16,-.12),.055), ((-.16,-.12),(-.025,.005),.055), ((-.16,-.12),(-.025,-.24),.055)])
bpy.context.view_layer.update()
bpy.data.libraries.write(str(ROOT/'art/work-lamps/worksite-tools.blend'), {scene}, fake_user=True)
bpy.context.window.scene = previous
result={'models':['WorkLamp','Arrow','Home','ReturnHere'],'scene':scene.name}
