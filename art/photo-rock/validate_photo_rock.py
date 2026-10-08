"""Run through Blender MCP; checks the actual source and FBX deliveries."""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).resolve().parent
P = 'SDT_PhotoRock'
SIZES = [(.70,.55,.45),(.72,.51,.39),(.61,.54,.46)]


def inspect_mesh(obj, triangles, size, convex=False, weld=False):
    mesh = obj.data
    mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles) == triangles, obj.name + ': triangle count'
    points = [obj.matrix_world @ v.co for v in mesh.vertices]
    assert all(math.isfinite(c) for v in points for c in v), 'Non-finite geometry'
    bounds = [(min(v[i] for v in points), max(v[i] for v in points)) for i in range(3)]
    dims = [b-a for a, b in bounds]
    assert all(abs(a-b) < 1e-5 for a, b in zip(dims, size)), str(dims)
    assert abs(bounds[2][0]) < 1e-5, 'Base pivot'
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        if weld:
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
        bm.normal_update()
        assert all(e.is_manifold and e.is_contiguous for e in bm.edges), 'Open or inconsistent mesh'
        assert all(f.calc_area() > 1e-12 for f in bm.faces), 'Degenerate face'
        volume = bm.calc_volume(signed=True)
        assert volume > 0, 'Inverted volume'
        if convex:
            assert all((v.co-f.verts[0].co).dot(f.normal) < 1e-5
                       for f in bm.faces for v in bm.verts), 'Non-convex collider'
    finally:
        bm.free()
    if not convex:
        assert len(mesh.materials) == 1, 'Expected one material'
        assert mesh.uv_layers.active is not None, 'Missing UVs'
        uv = mesh.uv_layers.active.data
        assert all(math.isfinite(c) and -.0001 <= c <= 1.0001 for p in uv for c in p.uv), 'UV range'
        assert all(abs((uv[t.loops[1]].uv-uv[t.loops[0]].uv).cross(
            uv[t.loops[2]].uv-uv[t.loops[0]].uv)) > 1e-14 for t in mesh.loop_triangles), 'Collapsed UV triangle'
    return {'triangles': triangles, 'vertices': len(mesh.vertices), 'dimensions_m': dims,
            'closed': True, 'outward_normals': True, 'volume_m3': volume, 'convex': convex}


def validate():
    original_scene = bpy.context.window.scene
    collections = ('scenes', 'objects', 'collections', 'meshes', 'materials', 'images', 'lights', 'cameras', 'worlds')
    before = {kind: set(getattr(bpy.data, kind)) for kind in collections}
    report = {'blender': bpy.app.version_string, 'asset': 'common_rock', 'appearances': {}}
    try:
        with bpy.data.libraries.load(str(OUT/'PhotoRock.blend'), link=False) as (source, target):
            assert set(source.scenes) == {P+'_'+v+'_Studio' for v in 'ABC'}, 'Unexpected scenes in source'
            target.scenes = list(source.scenes)
            target.materials = [P+'_'+v+'_EditableStone' for v in 'ABC']
        for index, variant in enumerate('abc'):
            prefix = P+'_'+variant.upper()
            saved = next(scene for scene in target.scenes if scene.name.startswith(prefix))
            bpy.context.window.scene = saved
            bpy.context.view_layer.update()
            visual = next(o for o in saved.objects if o.name.startswith(prefix+'_Rock'))
            collider = next(o for o in saved.objects if o.name.startswith(prefix+'_Collision'))
            entry = {'saved_visual': inspect_mesh(visual, 12500, SIZES[index]),
                     'saved_collision': inspect_mesh(collider, 82, SIZES[index], convex=True)}
            assert all(o.name.startswith(prefix) for o in saved.objects), 'Unrelated source objects'
            assert any(o.name.startswith(prefix+'_EditableControlCage') for o in saved.objects)
            assert len(target.materials[index].node_tree.nodes) > 20, 'Missing editable material'
            entry['textures'] = {}
            for channel in ('BaseColor', 'Normal', 'Masks'):
                img = visual.data.materials[0].node_tree.nodes[channel].image
                assert tuple(img.size) == (2048, 2048) and img.packed_file, 'Missing packed map'
                assert not img.is_float, 'Expected 8-bit maps'
                expected = 'sRGB' if channel == 'BaseColor' else 'Non-Color'
                assert img.colorspace_settings.name == expected, 'Wrong color space'
                path = OUT/'variants'/variant/'textures'/('Rock_'+channel+'.png')
                assert path.is_file() and path.stat().st_size > 1024, 'Missing PNG'
                entry['textures'][channel] = {'size': [2048,2048], 'color_space': expected, 'packed': True}
            temporary = bpy.data.scenes.new(P+'_Validation_'+variant)
            temporary.unit_settings.system = 'METRIC'
            bpy.context.window.scene = temporary
            entry['fbx_roundtrip'] = {}
            for kind, triangles in [('models',12500), ('collisions',82)]:
                previous = set(bpy.data.objects)
                bpy.ops.import_scene.fbx(filepath=str(OUT/'variants'/variant/kind/'ordinary_weathered_rock.fbx'))
                added = set(bpy.data.objects)-previous
                assert len(added) == 1 and next(iter(added)).type == 'MESH', 'Unexpected FBX contents'
                bpy.context.view_layer.update()
                imported = next(iter(added))
                entry['fbx_roundtrip'][kind] = inspect_mesh(imported,triangles,SIZES[index],convex=kind=='collisions',weld=True)
            report['appearances'][variant] = entry
        report['result'] = 'PASS'
    finally:
        bpy.context.window.scene = original_scene
        added = set().union(*(set(getattr(bpy.data, kind))-before[kind] for kind in collections))
        bpy.data.batch_remove(ids=added)
    (OUT/'validation.json').write_text(json.dumps(report, indent=2)+'\n',encoding='utf-8')
    return report
