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
def box(name, p, size, bevel=.01):
    bpy.ops.mesh.primitive_cube_add(size=1, location=point(p))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new('Soft manufactured edges', 'BEVEL')
    mod.width = bevel; mod.segments = 2
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

parts = [box('Rubber foot', (0,.025,0), (.28,.05,.28)),
    box('Yellow battery', (0,.08,0), (.24,.08,.24)),
    box('Yellow lantern cap', (0,.365,0), (.25,.035,.25)),
    box('Lens', (0,.235,0), (.20,.23,.20), .018),
    box('Steel handle left', (-.09,.42,0), (.024,.10,.032)),
    box('Steel handle right', (.09,.42,0), (.024,.10,.032)),
    box('Rubber handle', (0,.475,0), (.20,.027,.036))]
for x in [-.115,.115]:
    for z in [-.115,.115]:
        parts.append(box('Steel corner guard', (x,.24,z), (.018,.245,.018), .004))

def export(name, objects):
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
