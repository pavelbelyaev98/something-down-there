"""Shared Blender helpers for the camp shelters (workshop and tent): selection, UV packing, procedural shader
graphs and atlas bakes (albedo, normal and a Unity mask: metallic R, occlusion G, detail B, smoothness A).
Call configure() before bake_atlas()."""
import math

import bpy
import numpy as np

CFG = {'scene': None, 'res': 2048, 'samples_ao': 96, 'textures': None, 'occlusion_floor': .35}


def configure(**values):
    CFG.update(values)


def srgb(r, g, b):
    return tuple(((c / 255 + .055) / 1.055) ** 2.4 for c in (r, g, b)) + (1.0,)


def activate(objects):
    bpy.ops.object.mode_set(mode='OBJECT') if bpy.context.object and bpy.context.object.mode != 'OBJECT' else None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def pack_sheets(obj, margin=.06):
    """Shelf-packs each sheet's metric UV rectangle (ribs along V, never rotated). Returns the atlas side in metres."""
    mesh = obj.data
    island = mesh.attributes['island'].data
    uv = mesh.uv_layers['UVMap'].data
    groups = {}
    for poly in mesh.polygons:
        groups.setdefault(island[poly.index].value, []).append(poly)
    rects = []
    for key, polys in groups.items():
        us = [uv[i].uv[0] for p in polys for i in p.loop_indices]
        vs = [uv[i].uv[1] for p in polys for i in p.loop_indices]
        rects.append((max(vs) - min(vs), max(us) - min(us), key, polys))
    rects.sort(key=lambda r: -r[0])
    area = sum((w + margin) * (h + margin) for h, w, _, _ in rects)
    side = math.sqrt(area) * 1.02
    while True:
        x = y = row = 0.0
        place = {}
        ok = True
        for h, w, key, _ in rects:
            if x + w + margin > side:
                x, y, row = 0.0, y + row + margin, 0.0
            if y + h + margin > side:
                ok = False
                break
            place[key] = (x + margin / 2, y + margin / 2)
            x += w + margin
            row = max(row, h)
        if ok:
            break
        side *= 1.03
    for h, w, key, polys in rects:
        ox, oy = place[key]
        for p in polys:
            for i in p.loop_indices:
                u, v = uv[i].uv
                uv[i].uv = ((ox + u) / side, (oy + v) / side)
    return side


def smart_uv(obj, margin=.004):
    activate([obj])
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=margin, area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(rotate=True, margin_method='FRACTION', margin=margin, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')


def atlas_metres(obj):
    """Metres per UV unit of an evenly scaled atlas (from mesh and UV areas)."""
    mesh, uv = obj.data, obj.data.uv_layers['UVMap'].data
    world = uv_area = 0.0
    for poly in mesh.polygons:
        world += poly.area
        pts = [uv[i].uv for i in poly.loop_indices]
        uv_area += abs(sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))) / 2
    return math.sqrt(world / uv_area) if uv_area > 0 else 0.0


class Graph:
    def __init__(self, mat):
        mat.use_nodes = True
        self.tree = mat.node_tree
        self.tree.nodes.clear()
        self.x = 0

    def node(self, kind, **inputs):
        n = self.tree.nodes.new(kind)
        n.location = (self.x, 0)
        self.x += 160
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

    def add(self, *values):
        out = values[0]
        for v in values[1:]:
            out = self.math('ADD', out, v)
        return out

    def mul(self, a, b):
        return self.math('MULTIPLY', a, b)

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
        if isinstance(factor, bpy.types.NodeSocket):
            self.tree.links.new(factor, socket(n.inputs, 'Factor_Float'))
        else:
            socket(n.inputs, 'Factor_Float').default_value = factor
        for name, v in (('A_' + suffix, a), ('B_' + suffix, b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.tree.links.new(v, socket(n.inputs, name))
            else:
                socket(n.inputs, name).default_value = v
        return socket(n.outputs, 'Result_' + suffix)

    def mixf(self, factor, a, b):
        return self.mix(factor, a, b, 'FLOAT')

    def noise(self, vector, scale, detail=4, roughness=.55, w=0.0):
        n = self.node('ShaderNodeTexNoise')
        n.noise_dimensions = '4D'
        self.tree.links.new(vector, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = roughness
        n.inputs['W'].default_value = w
        return n.outputs['Fac']

    def voronoi(self, vector, scale, feature='F1', output='Distance'):
        n = self.node('ShaderNodeTexVoronoi')
        n.voronoi_dimensions = '3D'
        n.feature = feature
        self.tree.links.new(vector, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        return n.outputs[output]

    def vec(self, op, a, b):
        n = self.node('ShaderNodeVectorMath')
        n.operation = op
        for i, v in enumerate((a, b)):
            if isinstance(v, bpy.types.NodeSocket):
                self.tree.links.new(v, n.inputs[i])
            else:
                n.inputs[i].default_value = v
        return n.outputs['Value'] if op in ('DOT_PRODUCT', 'LENGTH', 'DISTANCE') else n.outputs['Vector']

    def xyz(self, vector):
        return self.node('ShaderNodeSeparateXYZ', Vector=vector).outputs

    def attr(self, name, kind='Fac'):
        n = self.node('ShaderNodeAttribute')
        n.attribute_type = 'GEOMETRY'
        n.attribute_name = name
        return n.outputs[kind]

    def uv(self, name):
        n = self.node('ShaderNodeUVMap')
        n.uv_map = name
        return n.outputs['UV']

    def eq(self, value, target):
        """1 where an integer attribute equals target."""
        return self.ramp(self.math('ABSOLUTE', self.math('SUBTRACT', value, target)), .5, .0)


def finish(g, albedo, metallic, rough, height, strength=.35, distance=.002):
    bump = g.node('ShaderNodeBump', Strength=strength, Distance=distance, Height=height)
    shader = g.node('ShaderNodeBsdfPrincipled', **{'Base Color': albedo, 'Metallic': metallic, 'Roughness': rough,
                                                   'Normal': bump.outputs['Normal']})
    emission = g.node('ShaderNodeEmission', Strength=1.0)
    output = g.node('ShaderNodeOutputMaterial')
    image = g.node('ShaderNodeTexImage')
    g.tree.nodes.active = image
    return {'albedo': albedo, 'metal': metallic, 'rough': rough, 'bsdf': shader, 'emit': emission, 'output': output, 'image': image}


def common(g):
    tc = g.node('ShaderNodeTexCoord')
    p = tc.outputs['Object']
    geo = g.node('ShaderNodeNewGeometry')
    n = geo.outputs['Normal']
    px, py, pz = g.xyz(p)
    nx, ny, nz = g.xyz(n)
    piece = g.attr('piece')
    return p, n, px, py, pz, nx, ny, nz, piece, geo


def new_image(name, color):
    img = bpy.data.images.get(name)
    if img is not None:
        bpy.data.images.remove(img)
    res = CFG['res']
    img = bpy.data.images.new(name, res, res, alpha=False, float_buffer=True)
    img.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
    return img


def bake_atlas(name, obj, entries, ao_distance):
    scene, RES, TEXTURES = CFG['scene'], CFG['res'], CFG['textures']
    """entries: list of node dicts (one per material slot of obj)."""
    images = {k: new_image(f'{name}_{k}', k == 'Albedo') for k in ('Albedo', 'Metal', 'Rough', 'AO', 'Normal')}

    def run(kind, image, channel=None):
        for nodes in entries:
            links = nodes['output'].id_data.links
            for link in list(nodes['output'].inputs['Surface'].links):
                links.remove(link)
            if channel is None:
                links.new(nodes['bsdf'].outputs[0], nodes['output'].inputs['Surface'])
            else:
                value = nodes[channel]
                if isinstance(value, float):
                    nodes['emit'].inputs['Color'].default_value = (value, value, value, 1)
                    for link in list(nodes['emit'].inputs['Color'].links):
                        links.remove(link)
                else:
                    links.new(value, nodes['emit'].inputs['Color'])
                links.new(nodes['emit'].outputs[0], nodes['output'].inputs['Surface'])
            nodes['image'].image = image
            nodes['output'].id_data.nodes.active = nodes['image']
        activate([obj])
        if kind == 'NORMAL':
            bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', margin=8, use_clear=True)
        else:
            bpy.ops.object.bake(type=kind, margin=8, use_clear=True)

    scene.cycles.samples = 8
    run('EMIT', images['Albedo'], 'albedo')
    run('EMIT', images['Metal'], 'metal')
    run('EMIT', images['Rough'], 'rough')
    scene.world.light_settings.distance = ao_distance
    scene.cycles.samples = CFG['samples_ao']
    run('AO', images['AO'])
    scene.cycles.samples = 4
    run('NORMAL', images['Normal'])

    def pixels(img):
        data = np.empty(RES * RES * 4, dtype=np.float32)
        img.pixels.foreach_get(data)
        return data.reshape(RES, RES, 4)
    mask = np.zeros((RES, RES, 4), dtype=np.float32)
    mask[..., 0] = pixels(images['Metal'])[..., 0]
    mask[..., 1] = np.clip(CFG['occlusion_floor'] + (1 - CFG['occlusion_floor']) * pixels(images['AO'])[..., 0], 0, 1)
    mask[..., 2] = 1
    mask[..., 3] = 1 - pixels(images['Rough'])[..., 0]
    packed = bpy.data.images.get(f'{name}_Mask')
    if packed is not None:
        bpy.data.images.remove(packed)
    packed = bpy.data.images.new(f'{name}_Mask', RES, RES, alpha=True, float_buffer=True)
    packed.colorspace_settings.name = 'Non-Color'
    packed.pixels.foreach_set(mask.ravel())
    for key, img in (('Albedo', images['Albedo']), ('Normal', images['Normal']), ('Mask', packed)):
        path = TEXTURES / f'{name}_{key}.png'
        img.filepath_raw = str(path)
        img.file_format = 'PNG'
        img.save(filepath=str(path))
    return images['Albedo'], images['Normal'], packed


def final_material(name, maps):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    tree.links.new(bsdf.outputs[0], out.inputs['Surface'])
    if maps:
        albedo, normal, mask = maps
        tex = tree.nodes.new('ShaderNodeTexImage')
        tex.image = albedo
        tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
        ntex = tree.nodes.new('ShaderNodeTexImage')
        ntex.image = normal
        nmap = tree.nodes.new('ShaderNodeNormalMap')
        tree.links.new(ntex.outputs['Color'], nmap.inputs['Color'])
        tree.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    return mat
