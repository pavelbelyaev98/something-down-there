"""Export small surface versions of the approved B/C rocks through Blender MCP."""
import bpy, os
root=os.path.dirname(os.path.abspath(__file__))
previous=bpy.context.window.scene
scene=bpy.data.scenes.get('SDT_SurfaceStones_134') or bpy.data.scenes.new('SDT_SurfaceStones_134')
try:
    with bpy.data.libraries.load(os.path.join(root,'PhotoRock.blend'),link=False) as (source,target):
        target.objects=['SDT_PhotoRock_B_Rock','SDT_PhotoRock_C_Rock']
    bpy.context.window.scene=scene
    objects=[]
    for label,source in zip(['B','C'],target.objects):
        name='SurfaceStone_'+label
        existing=scene.objects.get(name)
        if existing:bpy.data.objects.remove(existing,do_unlink=True)
        obj=source.copy();obj.data=source.data.copy();obj.name=name
        scene.collection.objects.link(obj);obj.location=(0,0,0)
        obj.hide_set(False);obj.hide_viewport=False;obj.hide_render=False
        for m in list(obj.modifiers):obj.modifiers.remove(m)
        modifier=obj.modifiers.new('Small stone geometry budget','DECIMATE');modifier.ratio=.02
        modifier.use_collapse_triangulate=True
        objects.append(obj)
        bpy.data.objects.remove(source,do_unlink=True)
    for o in bpy.context.selected_objects:o.select_set(False)
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(root,'SurfaceStones.fbx'),use_selection=True,
        object_types={'MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',
        apply_unit_scale=True,bake_space_transform=True)
    recipe=bpy.data.texts.get('Surface134_export_stones.py') or bpy.data.texts.new('Surface134_export_stones.py')
    recipe.clear();recipe.write(open(__file__,encoding='utf-8').read())
    bpy.data.libraries.write(os.path.join(root,'SurfaceStones.blend'),{scene,recipe},fake_user=True)
    result={'meshes':[{'name':o.name,'size':list(o.dimensions),'polygons':len(o.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.polygons)} for o in objects]}
finally:bpy.context.window.scene=previous
