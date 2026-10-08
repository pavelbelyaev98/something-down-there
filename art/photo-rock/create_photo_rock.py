"""Original photo-referenced rock. Run through Blender MCP. Owns SDT_PhotoRock only."""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector, noise as mnoise

ROOT = Path(__file__).resolve().parent
OUT = ROOT / 'variants' / 'a'
VARIANT = 0
P = 'SDT_PhotoRock_A'
SIZE = (0.70, 0.55, 0.45)  # width / depth / height in Blender metres
MAP_SIZE = 2048


def activate(objects):
    if bpy.context.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def scene():
    sc = bpy.data.scenes[P + '_Studio']
    bpy.context.window.scene = sc
    return sc


def rock():
    return bpy.data.objects[P + '_Rock']


def color(hex_color):
    c = [int(hex_color[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in c) + (1,)


def node(mat, kind, name):
    n = mat.node_tree.nodes.new(kind)
    n.name = n.label = name
    return n


def wire(mat, value, socket):
    if hasattr(value, 'node'):
        mat.node_tree.links.new(value, socket)
    else:
        socket.default_value = value


def calc(mat, operation, a, b=0):
    n = node(mat, 'ShaderNodeMath', operation)
    n.operation = operation
    wire(mat, a, n.inputs[0]); wire(mat, b, n.inputs[1])
    return n.outputs[0]


def mix(mat, fac, a, b, name):
    n = node(mat, 'ShaderNodeMixRGB', name)
    wire(mat, fac, n.inputs[0]); wire(mat, a, n.inputs[1]); wire(mat, b, n.inputs[2])
    return n.outputs[0]


def ramp(mat, value, stops, name):
    n = node(mat, 'ShaderNodeValToRGB', name)
    n.color_ramp.interpolation = 'EASE'
    for i, (at, col) in enumerate(stops):
        e = n.color_ramp.elements[i] if i < 2 else n.color_ramp.elements.new(at)
        e.position = at; e.color = color(col)
    wire(mat, value, n.inputs[0])
    return n.outputs[0]


def noise(mat, coords, scale, detail, name):
    n = node(mat, 'ShaderNodeTexNoise', name)
    n.inputs['Scale'].default_value = scale
    n.inputs['Detail'].default_value = detail
    n.inputs['Roughness'].default_value = .72
    wire(mat, coords, n.inputs['Vector'])
    return n.outputs['Fac']


def material():
    m = bpy.data.materials.get(P + '_EditableStone') or bpy.data.materials.new(P + '_EditableStone')
    m.use_nodes = True; m.use_fake_user = True; m.node_tree.nodes.clear()
    tex = node(m, 'ShaderNodeTexCoord', 'Local stone coordinates')
    coords = tex.outputs['Object']
    broad = noise(m, coords, 5.0, 3.0, 'Broad grey-green weathering')
    mid = noise(m, coords, 28, 4.0, 'Broken mineral patches')
    grain = noise(m, coords, 235, 2.3, 'Fine stone granules')
    pores = noise(m, coords, 78, 3.0, 'Small weathered pits')
    stone = ramp(m, broad, [(.25, '464d42'), (.45, '727867'), (.60, '969784'), (.77, 'b9b6a4')], 'Stone palette')
    mineral = ramp(m, mid, [(.19, '586052'), (.48, '888d80'), (.80, 'c2c0ae')], 'Subtle mineral variation')
    stone = mix(m, .43, stone, mineral, 'Minerals within broad faces')
    flecks = ramp(m, grain, [(.22, '5c6459'), (.50, '929889'), (.77, 'c5c5b3')], 'Grain palette')
    stone = mix(m, .21, stone, flecks, 'Restrained grain color')

    xyz = node(m, 'ShaderNodeSeparateXYZ', 'Stone side weathering'); wire(m, coords, xyz.inputs[0])
    side = calc(m, 'MULTIPLY', calc(m, 'MULTIPLY', xyz.outputs['Y'], -2.0), .45)
    # Warm olive alteration stays broad and restrained, as on the photographed fracture face.
    weathering = calc(m, 'MULTIPLY', calc(m, 'ADD', side, .22), noise(m, coords, 11, 2, 'Weathering breakup'))
    stone = mix(m, weathering, stone, color('9a9677'), 'Warmer olive face')

    large_lichen = noise(m, coords, 17, 4.3, 'Sparse pale patch shapes')
    small_lichen = noise(m, coords, 140, 2.5, 'Broken edges of pale patches')
    patch = calc(m, 'GREATER_THAN', calc(m, 'ADD', large_lichen, calc(m, 'MULTIPLY', small_lichen, .20)), .715)
    stone = mix(m, calc(m, 'MULTIPLY', patch, .73), stone, color('c5c3b4'), 'Sparse weathering flecks')
    rust = calc(m, 'GREATER_THAN', noise(m, coords, 35, 2.2, 'Tiny warm inclusions'), .69)
    stone = mix(m, calc(m, 'MULTIPLY', rust, .42), stone, color('a3826b'), 'Rare warmer chips')

    stretch = node(m, 'ShaderNodeVectorMath', 'Directional fracture coordinates'); stretch.operation = 'MULTIPLY'
    wire(m, coords, stretch.inputs[0]); stretch.inputs[1].default_value = (1.2, .6, 2.6)
    vein = noise(m, stretch.outputs[0], 15, 3.2, 'Interrupted fractures')
    line = calc(m, 'LESS_THAN', calc(m, 'ABSOLUTE', calc(m, 'SUBTRACT', vein, .5)), .011)
    fracture = calc(m, 'MULTIPLY', line, calc(m, 'GREATER_THAN', mid, .57))
    stone = mix(m, calc(m, 'MULTIPLY', fracture, .62), stone, color('3c463a'), 'Fine intermittent cracks')

    rough = calc(m, 'ADD', .78, calc(m, 'MULTIPLY', grain, .18))
    rough = calc(m, 'ADD', rough, calc(m, 'MULTIPLY', patch, .025))
    cavity_height = calc(m, 'SUBTRACT', pores, calc(m, 'MULTIPLY', fracture, .55))
    bump = node(m, 'ShaderNodeBump', 'Pits and fractured surface')
    bump.inputs['Strength'].default_value = .7; bump.inputs['Distance'].default_value = .0045
    wire(m, cavity_height, bump.inputs['Height'])
    micro = node(m, 'ShaderNodeBump', 'Fine rock grain')
    micro.inputs['Strength'].default_value = .62; micro.inputs['Distance'].default_value = .0022
    wire(m, grain, micro.inputs['Height']); wire(m, bump.outputs[0], micro.inputs['Normal'])
    bsdf = node(m, 'ShaderNodeBsdfPrincipled', 'Original Surface')
    wire(m, stone, bsdf.inputs['Base Color']); wire(m, rough, bsdf.inputs['Roughness'])
    wire(m, micro.outputs[0], bsdf.inputs['Normal'])
    bsdf.inputs['Metallic'].default_value = 0
    bsdf.inputs['Specular IOR Level'].default_value = .25
    out = node(m, 'ShaderNodeOutputMaterial', 'Output'); wire(m, bsdf.outputs[0], out.inputs[0])
    for name, value in [('Color Bake', stone)]:
        emission = node(m, 'ShaderNodeEmission', name); wire(m, value, emission.inputs[0])
    channels = node(m, 'ShaderNodeCombineColor', 'Metal AO Roughness')
    channels.inputs[0].default_value = 0; channels.inputs[1].default_value = 1
    wire(m, rough, channels.inputs[2])
    emission = node(m, 'ShaderNodeEmission', 'Masks Bake'); wire(m, channels.outputs[0], emission.inputs[0])
    # Lay out an editable graph rather than leaving every node stacked together.
    for i, n in enumerate(m.node_tree.nodes):
        n.location = ((i // 7) * 240, -(i % 7) * 170)
    return m


def create_geometry(sc):
    # Small uneven contact footprint, broad broken shoulders, sloping nonparallel faces.
    profiles = [
        [
            [(-.17,-.10,.052),(.07,-.16,.015),(.19,-.08,.055),(.15,.065,.035),(.09,.145,.075),(-.12,.12,.02),(-.22,.04,.085),(-.19,-.045,.025)],
            [(-.29,-.17,.15),(.12,-.255,.115),(.34,-.12,.19),(.275,.15,.265),(.15,.25,.255),(-.20,.20,.245),(-.335,.055,.215),(-.31,-.11,.19)],
            [(-.245,-.15,.345),(.105,-.18,.325),(.27,-.085,.39),(.205,.12,.435),(.105,.205,.455),(-.175,.17,.43),(-.27,.045,.435),(-.275,-.095,.375)]
        ],
        [
            [(-.19,-.08,.06),(.11,-.125,.015),(.215,-.045,.075),(.18,.095,.04),(.065,.14,.07),(-.145,.125,.025),(-.22,.025,.09),(-.195,-.04,.04)],
            [(-.335,-.15,.18),(.12,-.235,.135),(.355,-.10,.18),(.30,.13,.225),(.13,.22,.29),(-.20,.22,.25),(-.36,.04,.255),(-.33,-.08,.195)],
            [(-.275,-.105,.36),(.14,-.17,.30),(.30,-.06,.335),(.26,.12,.385),(.085,.16,.385),(-.185,.18,.405),(-.30,.02,.38),(-.32,-.07,.335)]
        ],
        [
            [(-.125,-.13,.055),(.10,-.145,.02),(.155,-.09,.08),(.135,.07,.035),(.075,.13,.075),(-.09,.145,.025),(-.175,.02,.095),(-.165,-.09,.055)],
            [(-.26,-.185,.18),(.095,-.25,.14),(.285,-.135,.215),(.27,.10,.28),(.145,.225,.31),(-.18,.23,.255),(-.29,.045,.255),(-.28,-.115,.24)],
            [(-.23,-.145,.38),(.08,-.185,.345),(.225,-.12,.385),(.21,.075,.435),(.10,.165,.455),(-.125,.195,.46),(-.23,.025,.425),(-.25,-.09,.41)]
        ]
    ]
    rings = profiles[VARIANT]
    centres = [(-.025,.035,.409),(.015,.03,.410),(-.005,.025,.463)]
    verts = [v for ring in rings for v in ring] + [centres[VARIANT],(-.012,.012,.024)]
    faces = [(25,(i+1)%8,i) for i in range(8)]
    for ring in range(2):
        for i in range(8):
            j=(i+1)%8; faces.append((ring*8+i,ring*8+j,(ring+1)*8+j,(ring+1)*8+i))
    faces += [(16+i,16+(i+1)%8,24) for i in range(8)]
    mesh=bpy.data.meshes.new(P+'_ControlCageMesh'); mesh.from_pydata(verts,[],faces); mesh.update()
    cage=bpy.data.objects.new(P+'_EditableControlCage',mesh); sc.collection.objects.link(cage)
    cage.hide_render=True; cage.hide_set(True)
    obj=bpy.data.objects.new(P+'_Rock',mesh.copy()); sc.collection.objects.link(obj); activate([obj])
    bevel=obj.modifiers.new('Small broken edge bevels','BEVEL'); bevel.width=.013; bevel.segments=2; bevel.angle_limit=.34
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    sub=obj.modifiers.new('Surface sculpture resolution','SUBSURF'); sub.subdivision_type='SIMPLE'; sub.levels=4
    bpy.ops.object.modifier_apply(modifier=sub.name)
    obj.data.update()
    positions=[v.co.copy() for v in obj.data.vertices]; normals=[v.normal.copy() for v in obj.data.vertices]
    for v,p,n in zip(obj.data.vertices,positions,normals):
        broad=mnoise.noise_vector(p*13+Vector((1.3,4.1,2.4)))[0]
        detail=mnoise.fractal(p*48+Vector((2.1,1.2,7.3)),.7,2.0,3)
        displacement=(broad*.0055+detail*.0028)
        v.co=p+n*displacement
        top=max(0,min(1,(p.z-.26)/.09))
        # A has one asymmetric basin. B/C have solid broken crowns, no cavity.
        cx,cy=centres[VARIANT][:2]
        dx=(p.x-cx)/(.125 if VARIANT!=2 else .115)
        dy=(p.y-cy)/(.102 if VARIANT!=1 else .087)
        radius=dx*dx+dy*dy
        bowl=(.072 if VARIANT==0 else 0)*math.exp(-radius*1.25)
        rim_variation=1+.10*math.sin(p.x*31+p.y*17+VARIANT)
        v.co.z-=top*bowl*rim_variation
    obj.data.update(); obj.data.calc_loop_triangles()
    dec=obj.modifiers.new('Game surface reduction','DECIMATE'); dec.ratio=min(1,12500/max(1,len(obj.data.loop_triangles)))
    bpy.ops.object.modifier_apply(modifier=dec.name)
    tri=obj.modifiers.new('Stable bake and export triangles','TRIANGULATE'); bpy.ops.object.modifier_apply(modifier=tri.name)
    bm=bmesh.new(); bm.from_mesh(obj.data); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(obj.data); bm.free()
    # Bake real metre dimensions into geometry, with base-centre pivot and unit transforms.
    lows=[min(v.co[k] for v in obj.data.vertices) for k in range(3)]
    highs=[max(v.co[k] for v in obj.data.vertices) for k in range(3)]
    for v in obj.data.vertices:
        for k in range(3):
            v.co[k]=(v.co[k]-lows[k])/(highs[k]-lows[k])*SIZE[k]-(SIZE[k]/2 if k<2 else 0)
    for polygon in obj.data.polygons: polygon.use_smooth=True
    obj.data.update(); activate([obj])
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(62),island_margin=.014,area_weight=.5,scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj['source_reference']='Four user supplied rock photographs; original recreation, not a scan.'
    obj['game_scale_metres']=SIZE; obj['source_asset_id']='common_rock'; obj['appearance_id']='abc'[VARIANT]
    obj['source_material']=P+'_EditableStone'
    obj.data.materials.append(material())
    bpy.context.view_layer.update()
    return obj


def create_collider(sc,obj):
    bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=2,radius=1)
    dirs=[v.co.normalized() for v in bm.verts]+[Vector(d) for d in [(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)]]
    bm.free(); points=[]
    for direction in dirs:
        p=max(obj.data.vertices,key=lambda v:v.co.dot(direction)).co.copy()
        if not any((p-q).length<.00001 for q in points):points.append(p)
    bm=bmesh.new(); vs=[bm.verts.new(p) for p in points]
    hull=bmesh.ops.convex_hull(bm,input=vs,use_existing_faces=False)
    discard=[g for g in hull.get('geom_interior',[])+hull.get('geom_unused',[]) if isinstance(g,bmesh.types.BMVert)]
    if discard:bmesh.ops.delete(bm,geom=list(set(discard)),context='VERTS')
    bmesh.ops.triangulate(bm,faces=list(bm.faces)); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    mesh=bpy.data.meshes.new(P+'_CollisionMesh'); bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new(P+'_Collision',mesh); sc.collection.objects.link(obj)
    obj.hide_render=True; obj.hide_set(True); return obj


def studio():
    if bpy.data.scenes.get(P+'_Studio'):
        raise RuntimeError('Existing rock scene: use the named edit/bake/export steps; do not rebuild over user edits.')
    for folder in ('models', 'collisions', 'textures', 'previews'):
        (OUT/folder).mkdir(parents=True, exist_ok=True)
    sc=bpy.data.scenes.new(P+'_Studio'); bpy.context.window.scene=sc
    sc.unit_settings.system='METRIC'; sc.unit_settings.scale_length=1
    sc.render.engine='BLENDER_EEVEE'; sc.render.resolution_x=1600; sc.render.resolution_y=1400; sc.render.resolution_percentage=100
    sc.render.image_settings.file_format='PNG'; sc.render.image_settings.color_mode='RGBA'
    sc.view_settings.view_transform='AgX'
    sc.view_settings.exposure=-1.0
    sc.world=bpy.data.worlds.new(P+'_World'); sc.world.use_nodes=True
    sc.world.node_tree.nodes['Background'].inputs[0].default_value=(.26,.27,.25,1)
    sc.world.node_tree.nodes['Background'].inputs[1].default_value=.45
    obj=create_geometry(sc); hull=create_collider(sc,obj)
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.006)); floor=bpy.context.object; floor.name=P+'_PreviewFloor'
    mat=bpy.data.materials.new(P+'_StudioFloor'); mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=color('62655e')
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.95; floor.data.materials.append(mat)
    for name,loc,power,size,col in [
        ('Key',(-1.5,-1.8,2.6),185,2.0,(1,.96,.90)),
        ('Fill',(1.7,-.4,1.2),70,1.7,(.88,.93,1)),
        ('Rim',(.1,1.6,2),135,1.5,(1,1,.95))]:
        data=bpy.data.lights.new(P+'_'+name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size; data.color=col
        light=bpy.data.objects.new(P+'_'+name,data); sc.collection.objects.link(light); light.location=loc
        light.rotation_euler=(Vector((0,0,.2))-light.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new(P+'_Camera'); data.lens=58; data.clip_start=.01; data.clip_end=250
    camera=bpy.data.objects.new(P+'_Camera',data); sc.collection.objects.link(camera); sc.camera=camera
    camera.location=(1.2,-1.55,1.05); camera.rotation_euler=(Vector((0,0,.22))-camera.location).to_track_quat('-Z','Y').to_euler()
    activate([obj]); obj.data.calc_loop_triangles(); hull.data.calc_loop_triangles()
    return {'scene':sc.name,'dimensions_m':list(obj.dimensions),'triangles':len(obj.data.loop_triangles),'collision_triangles':len(hull.data.loop_triangles)}


def preview(name='hero',baked=False):
    sc=scene(); obj=rock()
    obj.data.materials.clear(); obj.data.materials.append(delivery_material() if baked else bpy.data.materials[P+'_EditableStone'])
    views={'hero':(1.2,-1.55,1.05),'front':(-1.25,-1.6,.95),'rear':(.9,1.65,1.18),'top':(.5,-.65,1.8),'close':(.60,-.82,.67)}
    camera=sc.camera; camera.location=views[name]
    camera.rotation_euler=(Vector((0,0,.22))-camera.location).to_track_quat('-Z','Y').to_euler()
    sc.render.engine='BLENDER_EEVEE'; sc.render.filepath=str(OUT/'previews'/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    return {'preview':sc.render.filepath,'baked':baked}


def bake(channel):
    sc=scene(); obj=rock(); m=bpy.data.materials[P+'_EditableStone']
    obj.data.materials.clear(); obj.data.materials.append(m); activate([obj])
    sc.render.engine='CYCLES'; sc.cycles.samples=4; sc.cycles.device='CPU'
    img=bpy.data.images.get(P+'_'+channel)
    if img is not None and img.is_float:
        users=[n for material in bpy.data.materials if material.name.startswith(P) and material.use_nodes
               for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image==img]
        bpy.data.images.remove(img)
        img=bpy.data.images.new(P+'_'+channel,MAP_SIZE,MAP_SIZE,alpha=False,float_buffer=False)
        for n in users:n.image=img
    if img is None:img=bpy.data.images.new(P+'_'+channel,MAP_SIZE,MAP_SIZE,alpha=False,float_buffer=False)
    if img.packed_file:img.unpack(method='REMOVE')
    img.colorspace_settings.name='sRGB' if channel=='BaseColor' else 'Non-Color'
    img.filepath_raw=str(OUT/'textures'/('Rock_'+channel+'.png')); img.file_format='PNG'
    img.save()
    target=m.node_tree.nodes.get('Atlas Bake Target') or node(m,'ShaderNodeTexImage','Atlas Bake Target')
    target.image=img
    for n in m.node_tree.nodes:n.select=False
    target.select=True; m.node_tree.nodes.active=target
    source='Original Surface' if channel=='Normal' else ('Color Bake' if channel=='BaseColor' else 'Masks Bake')
    wire(m,m.node_tree.nodes[source].outputs[0],m.node_tree.nodes['Output'].inputs['Surface'])
    bpy.ops.object.bake(type='NORMAL' if channel=='Normal' else 'EMIT',normal_space='TANGENT',margin=16,use_clear=True)
    img.save()
    wire(m,m.node_tree.nodes['Original Surface'].outputs[0],m.node_tree.nodes['Output'].inputs['Surface'])
    sc.render.engine='BLENDER_EEVEE'
    return {'channel':channel,'path':img.filepath_raw,'size':list(img.size)}


def delivery_material():
    existing=bpy.data.materials.get(P+'_BakedStone')
    if existing:return existing
    m=bpy.data.materials.new(P+'_BakedStone'); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Specular IOR Level'].default_value=.25
    for channel in ['BaseColor','Normal','Masks']:
        n=node(m,'ShaderNodeTexImage',channel); n.image=bpy.data.images[P+'_'+channel]; n.extension='EXTEND'
        if channel=='BaseColor':wire(m,n.outputs['Color'],p.inputs['Base Color'])
        elif channel=='Normal':
            normal=node(m,'ShaderNodeNormalMap','Tangent normal'); wire(m,n.outputs['Color'],normal.inputs['Color']); wire(m,normal.outputs[0],p.inputs['Normal'])
        else:
            split=node(m,'ShaderNodeSeparateColor','Metal AO Roughness'); wire(m,n.outputs['Color'],split.inputs[0])
            wire(m,split.outputs[0],p.inputs['Metallic']); wire(m,split.outputs[2],p.inputs['Roughness'])
    return m


def export():
    sc=scene(); obj=rock(); obj.data.materials.clear(); obj.data.materials.append(delivery_material())
    files=[]
    for o,path in [(obj,OUT/'models/ordinary_weathered_rock.fbx'),(bpy.data.objects[P+'_Collision'],OUT/'collisions/ordinary_weathered_rock.fbx')]:
        activate([o]); bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',bake_space_transform=False,use_mesh_modifiers=True,use_triangles=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False)
        files.append(str(path))
    bpy.data.objects[P+'_Collision'].hide_set(True); activate([obj])
    return {'exports':files}


def save_source():
    sc=scene()
    for img in bpy.data.images:
        if img.name.startswith(P+'_') and img.has_data:img.pack()
    blocks={sc,bpy.data.materials[P+'_EditableStone']}
    bpy.data.libraries.write(str(OUT/'PhotoRock.blend'),blocks,path_remap='RELATIVE_ALL',fake_user=True,compress=True)
    return {'source':str(OUT/'PhotoRock.blend')}


def use_variant(index):
    global P, OUT, SIZE, VARIANT
    VARIANT=index
    P='SDT_PhotoRock_'+'ABC'[index]
    OUT=ROOT/'variants'/'abc'[index]
    SIZE=[(.70,.55,.45),(.72,.51,.39),(.61,.54,.46)][index]
    return {'variant':'abc'[index],'size':SIZE}


def save_batch():
    blocks=set()
    for index in range(3):
        use_variant(index)
        blocks.update((scene(),bpy.data.materials[P+'_EditableStone']))
    for img in bpy.data.images:
        if img.name.startswith('SDT_PhotoRock_') and img.has_data:img.pack()
    bpy.data.libraries.write(str(ROOT/'PhotoRock.blend'),blocks,path_remap='RELATIVE_ALL',fake_user=True,compress=True)
    use_variant(0);scene()
    return {'source':str(ROOT/'PhotoRock.blend'),'variants':3}


def rebuild_variant_geometry():
    sc=scene()
    if bpy.context.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
    for suffix in ('_Rock','_Collision','_EditableControlCage'):
        old=bpy.data.objects.get(P+suffix)
        if old:
            mesh=old.data;bpy.data.objects.remove(old,do_unlink=True)
            if mesh.users==0:bpy.data.meshes.remove(mesh)
    obj=create_geometry(sc);collider=create_collider(sc,obj)
    return {'variant':VARIANT,'triangles':len(obj.data.polygons),'collision_triangles':len(collider.data.polygons)}


def render_lineup(view='hero'):
    """Preview staging only; no extra objects enter FBX or the saved source scenes."""
    original=scene()
    previous=set(bpy.data.objects)
    review=bpy.data.scenes.new('SDT_PhotoRock_Overview')
    try:
        bpy.context.window.scene=review
        review.world=original.world
        review.render.engine='BLENDER_EEVEE'
        review.render.resolution_x=2200;review.render.resolution_y=1000;review.render.resolution_percentage=100
        review.view_settings.view_transform='AgX';review.view_settings.exposure=-1.0
        for obj in original.objects:
            if obj.type in ('LIGHT','CAMERA') or obj.name.endswith('_PreviewFloor'):
                copy=obj.copy();review.collection.objects.link(copy)
                if obj.type=='CAMERA':copy.data=obj.data.copy();review.camera=copy
        for index in range(3):
            source=bpy.data.objects['SDT_PhotoRock_'+'ABC'[index]+'_Rock']
            copy=source.copy();review.collection.objects.link(copy)
            copy.location=((index-1)*.82,0,0);copy.hide_set(False)
            copy.rotation_euler.z=math.radians([0,12,-14][index])
        camera=review.camera;camera.data.type='ORTHO';camera.data.ortho_scale=2.85
        camera.location={'hero':(.55,-3.6,2.35),'rear':(.3,3.6,1.7),'top':(.01,-.15,4),'front':(0,-4,1)}[view]
        camera.rotation_euler=(Vector((0,0,.2))-camera.location).to_track_quat('-Z','Y').to_euler()
        (ROOT/'previews').mkdir(exist_ok=True)
        review.render.filepath=str(ROOT/'previews'/('variations.png' if view=='hero' else 'variants_'+view+'.png'));review.render.image_settings.file_format='PNG'
        bpy.ops.render.render(write_still=True)
        return {'preview':review.render.filepath,'left_to_right':['A: hollow','B: solid broad','C: solid tall']}
    finally:
        camera_data=review.camera.data if review.camera else None
        bpy.context.window.scene=original
        for obj in set(bpy.data.objects)-previous:bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(review)
        if camera_data and camera_data.users==0:bpy.data.cameras.remove(camera_data)
