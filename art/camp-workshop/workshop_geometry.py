"""Geometry of the camp workshop: a corrugated-iron shed on a timber frame and a concrete slab, built the way a
small rural workshop is (references: green corrugated workshops, weathered galvanised sheds with a big door).

Blender axes, metres: X across the 8 m front, Y front (-) to back (+), Z up; the ground is z = 0 under the slab.
The gable faces the front (-Y) with a 4.6 m roller shutter; the ridge runs front to back.
Every object is a group of parts that share one finish; each part keeps its own random value in the face
attribute "piece" so baked wear varies from sheet to sheet and beam to beam.
"""
import bmesh
import bpy
import math
import random
from mathutils import Vector

W, D = 8.0, 6.0                  # outside of the frame
HW, HD = W / 2, D / 2
SLAB = .15                       # slab top above the ground
PLATE = SLAB + 2.80              # top of the wall plates
PITCH = .35                      # rise per metre (19.3 degrees)
COS = 1 / math.sqrt(1 + PITCH * PITCH)
RAFTER, PURLIN = .15, .07        # depths
OVER_EAVE, OVER_GABLE = .25, .25
DOOR_HALF, DOOR_TOP = 2.30, SLAB + 2.70
WIN_HALF, WIN_LOW, WIN_HIGH = .60, SLAB + 1.15, SLAB + 2.05
SHEET_W, SHEET_LAP = .762, .076  # one corrugation of side lap
rng = random.Random(7)


def rafter_top(x):
    """Height of the rafters' top edge (under the purlins) at distance x from the centre line."""
    return PLATE + RAFTER / COS + (HW - abs(x)) * PITCH


def roof_z(x):
    """Height of the roof sheets' outer face."""
    return rafter_top(x) + PURLIN / COS


class Group:
    """One bmesh per finish; parts get a random 'piece' value."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.layer = self.bm.faces.layers.float.new('piece')
        self.kind_layer = self.bm.faces.layers.int.new('kind')
        self.kind = 0
        self.closed = []
        self.island_layer = self.bm.faces.layers.int.new('island')
        self.axis_layer = self.bm.faces.layers.float_vector.new('axis')
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.local = self.bm.loops.layers.uv.new('Local')
        self.islands = 0

    def solid(self, faces):
        self.closed.extend(faces)
        return faces

    def tag(self, faces, value=None):
        value = rng.random() if value is None else value
        for f in faces:
            f[self.layer] = value
            f[self.kind_layer] = self.kind
        return faces

    def strip(self, rows, value=None, double=False):
        """Quads between consecutive rows of points; double adds the back faces 2 mm behind."""
        faces = []
        grid = [[self.bm.verts.new(p) for p in row] for row in rows]
        for i in range(len(grid) - 1):
            for k in range(len(grid[i]) - 1):
                faces.append(self.bm.faces.new((grid[i][k], grid[i][k + 1], grid[i + 1][k + 1], grid[i + 1][k])))
        if double:
            a, b, c = Vector(rows[0][0]), Vector(rows[0][1]), Vector(rows[1][0])
            n = (b - a).cross(c - a).normalized()
            back = [[self.bm.verts.new(Vector(p) - n * .002) for p in row] for row in rows]
            for i in range(len(back) - 1):
                for k in range(len(back[i]) - 1):
                    faces.append(self.bm.faces.new((back[i + 1][k], back[i + 1][k + 1], back[i][k + 1], back[i][k])))
        return self.tag(faces, value)

    def poly(self, points, value=None):
        verts = [self.bm.verts.new(p) for p in points]
        return self.tag([self.bm.faces.new(verts)], value)[0]

    def box(self, lo, hi, value=None, bevel=0.0):
        (x0, y0, z0), (x1, y1, z1) = lo, hi
        made = bmesh.ops.create_cube(self.bm, size=1)
        verts = made['verts']
        for v in verts:
            v.co = Vector(((x0 + x1) / 2 + v.co.x * (x1 - x0), (y0 + y1) / 2 + v.co.y * (y1 - y0), (z0 + z1) / 2 + v.co.z * (z1 - z0)))
        faces = list({f for v in verts for f in v.link_faces})
        size = (abs(x1 - x0), abs(y1 - y0), abs(z1 - z0))
        longest = Vector([1.0 if s == max(size) else 0.0 for s in size])
        for f in faces:
            f[self.axis_layer] = longest
        if bevel <= 0:
            return self.solid(self.tag(faces, value))
        if bevel > 0:
            edges = list({e for f in faces for e in f.edges})
            out = bmesh.ops.bevel(self.bm, geom=edges + verts, offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
            faces = [f for f in faces if f.is_valid] + [f for f in out.get('faces', []) if f not in faces]
            for f in faces:
                f[self.axis_layer] = longest
        return self.solid(self.tag(faces, value))

    def beam(self, a, b, width, depth, up=(0, 0, 1), value=None, bevel=.004):
        """A rectangular member from a to b: 'width' across, 'depth' along 'up' (projected square to the axis)."""
        a, b, up = Vector(a), Vector(b), Vector(up)
        axis = (b - a).normalized()
        up = (up - axis * up.dot(axis)).normalized()
        side = axis.cross(up).normalized()
        corners = []
        for s, u in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            corners.append(side * (s * width / 2) + up * (u * depth / 2))
        ring_a = [self.bm.verts.new(a + c) for c in corners]
        ring_b = [self.bm.verts.new(b + c) for c in corners]
        faces = [self.bm.faces.new((ring_a[3], ring_a[2], ring_a[1], ring_a[0])), self.bm.faces.new(ring_b)]
        for i in range(4):
            j = (i + 1) % 4
            faces.append(self.bm.faces.new((ring_a[i], ring_a[j], ring_b[j], ring_b[i])))
        for f in faces:
            f[self.axis_layer] = Vector((abs(axis.x), abs(axis.y), abs(axis.z)))
        if bevel > 0:
            verts = ring_a + ring_b
            edges = list({e for f in faces for e in f.edges})
            out = bmesh.ops.bevel(self.bm, geom=edges + verts, offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
            faces = [f for f in faces if f.is_valid] + [f for f in out.get('faces', []) if f not in faces]
            for f in faces:
                f[self.axis_layer] = Vector((abs(axis.x), abs(axis.y), abs(axis.z)))
        return self.solid(self.tag(faces, value))

    def tube(self, a, b, radius, segments=12, value=None, caps=True):
        a, b = Vector(a), Vector(b)
        axis = (b - a).normalized()
        ref = Vector((0, 0, 1)) if abs(axis.z) < .9 else Vector((1, 0, 0))
        u = axis.cross(ref).normalized()
        v = axis.cross(u).normalized()
        ring = [u * math.cos(2 * math.pi * i / segments) * radius + v * math.sin(2 * math.pi * i / segments) * radius for i in range(segments)]
        ra = [self.bm.verts.new(a + c) for c in ring]
        rb = [self.bm.verts.new(b + c) for c in ring]
        faces = []
        for i in range(segments):
            j = (i + 1) % segments
            faces.append(self.bm.faces.new((ra[i], ra[j], rb[j], rb[i])))
        if caps:
            faces.append(self.bm.faces.new(list(reversed(ra))))
            faces.append(self.bm.faces.new(rb))
        for f in faces:
            f[self.axis_layer] = Vector((abs(axis.x), abs(axis.y), abs(axis.z)))
        return self.solid(self.tag(faces, value)) if caps else self.tag(faces, value)

    def surface(self, fn, nu, nv, width, length, value=None, flip=False, local=None):
        """A quad-grid island from fn(s, t) -> point, s and t in [0, 1]; UVs in metres (s * width, t * length),
        packed later. Faces point along d/ds x d/dt (reversed with flip). local(s, t) -> (u, v) fills 'Local'."""
        grid = [[self.bm.verts.new(fn(i / nu, j / nv)) for j in range(nv + 1)] for i in range(nu + 1)]
        self.islands += 1
        island = self.islands
        faces = []
        for i in range(nu):
            for j in range(nv):
                quad = [grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]]
                st = [(i / nu, j / nv), ((i + 1) / nu, j / nv), ((i + 1) / nu, (j + 1) / nv), (i / nu, (j + 1) / nv)]
                if flip:
                    quad, st = list(reversed(quad)), list(reversed(st))
                face = self.bm.faces.new(quad)
                for loop, (a, b) in zip(face.loops, st):
                    loop[self.uv].uv = (a * width, b * length)
                    loop[self.local].uv = local(a, b) if local else (a, b)
                face[self.island_layer] = island
                faces.append(face)
        return self.tag(faces, value)

    def finish(self, collection):
        live = [f for f in self.closed if f.is_valid]
        if live:
            bmesh.ops.recalc_face_normals(self.bm, faces=live)
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        collection.objects.link(obj)
        return obj


def sheet_strip(g_out, g_in, a, along, across, length, width, normal, value, bow=.004, segments=4):
    """A corrugated sheet as a flat panel (ribs come from the normal map), outer and inner faces 2 mm apart.
    a: one bottom corner; along: unit vector of the ribs; across: unit vector across the sheet; normal: outward."""
    a, along, across, normal = Vector(a), Vector(along), Vector(across), Vector(normal)
    rows = []
    for i in range(segments + 1):
        t = i / segments
        sag = bow * math.sin(math.pi * t)  # each sheet bows a little between its fixings
        rows.append([a + along * (length * t) + across * (width * k) + normal * (sag * (1 if k == .5 else .4)) for k in (0, .5, 1)])
    for group, offset, flip in ((g_out, .0, False), (g_in, -.002, True)):
        verts = [[group.bm.verts.new(p + normal * offset) for p in row] for row in rows]
        group.islands += 1
        island = group.islands  # 0 means not a sheet
        faces = []
        for i in range(segments):
            for k in range(2):
                quad = [verts[i][k], verts[i][k + 1], verts[i + 1][k + 1], verts[i + 1][k]]
                coords = [((k) * .5, i / segments), ((k + 1) * .5, i / segments), ((k + 1) * .5, (i + 1) / segments), (k * .5, (i + 1) / segments)]
                if flip:
                    quad, coords = list(reversed(quad)), list(reversed(coords))
                face = group.bm.faces.new(quad)
                for loop, (cu, cv) in zip(face.loops, coords):
                    loop[group.uv].uv = (cu * width, cv * length)      # metres; packed into the atlas later
                    loop[group.local].uv = (cu, cv * length)           # across fraction, metres along the ribs
                face[group.island_layer] = island
                faces.append(face)
        group.tag(faces, value)
        for f in faces:
            f.normal_update()
            if (f.normal.dot(normal) < 0) != flip:
                f.normal_flip()


def build(collection):
    groups = {name: Group('Workshop_' + name) for name in
              ('SheetOut', 'SheetIn', 'Skylight', 'Timber', 'Concrete', 'Galv', 'Trim', 'Glass', 'Rack', 'Fittings', 'TubeLamp')}
    g = groups

    # ------------------------------------------------------------ slab and apron
    g['Concrete'].box((-HW, -HD, -.35), (HW, HD, SLAB), bevel=.012)
    # A shallow apron ramp in front of the door.
    ap = g['Concrete']
    x0, x1, y0, y1 = -DOOR_HALF - .35, DOOR_HALF + .35, -HD - .9, -HD
    pts = [(x0, y0, -.05), (x1, y0, -.05), (x1, y1, -.05), (x0, y1, -.05), (x0, y0, .025), (x1, y0, .025), (x1, y1, SLAB), (x0, y1, SLAB)]
    v = [ap.bm.verts.new(p) for p in pts]
    ap.tag([ap.bm.faces.new(f) for f in ((v[0], v[1], v[5], v[4]), (v[4], v[5], v[6], v[7]), (v[1], v[2], v[6], v[5]), (v[3], v[0], v[4], v[7]))])

    # ------------------------------------------------------------ timber frame
    t = g['Timber']
    stud_w, stud_d = .05, .10
    # Sole plates on the slab and top plates on the side walls.
    for sx in (-1, 1):
        x = sx * (HW - stud_d / 2)
        t.beam((x, -HD, SLAB + .025), (x, HD, SLAB + .025), stud_d, .05)
        t.beam((x, -HD, PLATE - .05), (x, HD, PLATE - .05), stud_d, .10)
        # Studs every 1 m, with the window framed between y = -0.6 and 0.6.
        for y in (-HD + .05, -2, -1, -WIN_HALF - .025, WIN_HALF + .025, 1, 2, HD - .05):
            t.beam((x, y, SLAB + .05), (x, y, PLATE - .10), stud_d, stud_w, up=(0, 1, 0))
        # Girts (rails) the sheets are nailed to, stopping at the window.
        for z in (SLAB + .7, SLAB + 2.3):
            if WIN_LOW < z < WIN_HIGH:
                continue
            t.beam((x + sx * .005, -HD, z), (x + sx * .005, HD, z), stud_d - .01, .05)
        for z in (WIN_LOW - .03, WIN_HIGH + .03):
            t.beam((x, -WIN_HALF, z), (x, WIN_HALF, z), stud_d, .05)
    # Front and back walls: corner posts, door posts, header and gable studs to the roofline.
    for sy, wall in ((-1, 'front'), (1, 'back')):
        y = sy * (HD - stud_d / 2)
        t.beam((-HW + .05, y, SLAB + .025), (HW - .05, y, SLAB + .025), stud_d, .05) if wall == 'back' else None
        if wall == 'front':
            for x0, x1 in ((-HW + .05, -DOOR_HALF - .05), (DOOR_HALF + .05, HW - .05)):
                t.beam((x0, y, SLAB + .025), (x1, y, SLAB + .025), stud_d, .05)
            for x in (-DOOR_HALF - .05, DOOR_HALF + .05):
                t.beam((x, y, SLAB), (x, y, rafter_top(x) - .02), .10, stud_d, up=(1, 0, 0))
            t.beam((-DOOR_HALF - .1, y, DOOR_TOP + .1), (DOOR_HALF + .1, y, DOOR_TOP + .1), stud_d, .20)
            xs = (-HW + .05, -3.2, 3.2, HW - .05, -1.5, -.75, 0, .75, 1.5)
        else:
            xs = (-HW + .05, -3.2, -2.4, -1.6, -.8, 0, .8, 1.6, 2.4, 3.2, HW - .05)
        for x in xs:
            low = DOOR_TOP + .2 if wall == 'front' and abs(x) < DOOR_HALF else SLAB + .05
            top = rafter_top(x) - .02 if abs(x) < HW - .1 else PLATE - .1
            t.beam((x, y, low), (x, y, top), stud_d, stud_w, up=(1, 0, 0))
        for z in (SLAB + .7, SLAB + 2.3):
            if wall == 'front' and z < DOOR_TOP + .2:
                for x0, x1 in ((-HW + .05, -DOOR_HALF - .1), (DOOR_HALF + .1, HW - .05)):
                    t.beam((x0, y + sy * .005, z), (x1, y + sy * .005, z), stud_d - .01, .05)
            else:
                t.beam((-HW + .05, y + sy * .005, z), (HW - .05, y + sy * .005, z), stud_d - .01, .05)
    # Rafters, collar ties, ridge board and purlins.
    frames = (-HD + .025, -1.5, 0, 1.5, HD - .025)
    for y in frames:
        for sx in (-1, 1):
            foot = Vector((sx * (HW + OVER_EAVE * .6), y, PLATE + RAFTER / COS / 2 - OVER_EAVE * .6 * PITCH))
            head = Vector((sx * .02, y, rafter_top(0) - RAFTER / COS / 2))
            t.beam(foot, head, .05, RAFTER, up=(-sx * PITCH, 0, 1))
        if -HD + .1 < y < HD - .1:
            zc = PLATE + 1.0
            reach = HW - (zc + .075 - PLATE - RAFTER / COS) / PITCH
            t.beam((-reach, y + .045, zc), (reach, y + .045, zc), .04, .15, up=(0, 0, 1))
    t.beam((0, -HD - OVER_GABLE + .02, rafter_top(0) - .1), (0, HD + OVER_GABLE - .02, rafter_top(0) - .1), .03, .2)
    for sx in (-1, 1):
        for k in range(5):
            x = sx * (.15 + k * (HW + OVER_EAVE * .6 - .2) / 4)
            z = rafter_top(x) + PURLIN / COS / 2
            t.beam((x, -HD - OVER_GABLE, z), (x, HD + OVER_GABLE, z), .05, PURLIN, up=(-sx * PITCH, 0, 1))

    # ------------------------------------------------------------ cladding
    so, si = g['SheetOut'], g['SheetIn']
    out_x = HW + .002
    # Side walls: vertical sheets from just over the slab edge to the eave, the window cut out.
    for sx in (-1, 1):
        n = Vector((sx, 0, 0))
        y = -HD - .01
        i = 0
        while y < HD + .01:
            w = min(SHEET_W, HD + .01 - y)
            off = (i % 2) * .003
            value = rng.random()
            lo, hi = SLAB - .1, PLATE + .12
            ya, yb = y, y + w
            pieces = [(ya, yb, lo, hi)]
            if yb > -WIN_HALF and ya < WIN_HALF:
                pieces = [(ya, yb, lo, WIN_LOW), (ya, yb, WIN_HIGH, hi)]
                if ya < -WIN_HALF:
                    pieces.append((ya, -WIN_HALF, WIN_LOW, WIN_HIGH))
                if yb > WIN_HALF:
                    pieces.append((WIN_HALF, yb, WIN_LOW, WIN_HIGH))
            for p0, p1, z0, z1 in pieces:
                sheet_strip(so, si, (sx * (out_x + off), p0 if sx > 0 else p1, z0), (0, 0, 1), (0, 1 if sx > 0 else -1, 0),
                            z1 - z0, p1 - p0, n, value, segments=4 if z1 - z0 > 1 else 2)
            # Beside the window the sheet keeps going: fill the gap columns left and right of the opening.
            y += SHEET_W - SHEET_LAP
            i += 1
    # Front (door gable) and back gable: vertical sheets up to the roofline.
    for sy in (-1, 1):
        n = Vector((0, sy, 0))
        x = -HW - .01
        i = 0
        while x < HW + .01:
            w = min(SHEET_W, HW + .01 - x)
            off = (i % 2) * .003
            value = rng.random()
            xc = x + w / 2
            top = rafter_top(min(abs(x), abs(x + w)) if x < 0 < x + w else min(abs(x), abs(x + w))) - .01
            spans = [(SLAB - .1, top)]
            if sy < 0 and x + w > -DOOR_HALF and x < DOOR_HALF:
                # Over the door: only above the header; the part beside the jamb is a narrow strip.
                spans = [(DOOR_TOP + .02, top)]
                if x < -DOOR_HALF:
                    sheet_strip(so, si, (x, sy * (HD + .002 + off), SLAB - .1), (0, 0, 1), (1, 0, 0), DOOR_TOP + .02 - SLAB + .1,
                                -DOOR_HALF - x, n, value, segments=3)
                if x + w > DOOR_HALF:
                    sheet_strip(so, si, (DOOR_HALF, sy * (HD + .002 + off), SLAB - .1), (0, 0, 1), (1, 0, 0), DOOR_TOP + .02 - SLAB + .1,
                                x + w - DOOR_HALF, n, value, segments=3)
            for z0, z1 in spans:
                base = (x, sy * (HD + .002 + off), z0) if sy < 0 else (x + w, sy * (HD + .002 + off), z0)
                sheet_strip(so, si, base, (0, 0, 1), (1 if sy < 0 else -1, 0, 0), z1 - z0, w, n, value, segments=3)
            x += SHEET_W - SHEET_LAP
            i += 1
    # The gable sheets' tops follow the roof slope: trim each strip's top row onto the rafter line.
    for group in (so, si):
        for v in group.bm.verts:
            if abs(abs(v.co.y) - (HD + .002)) < .01 and v.co.z > PLATE:
                v.co.z = min(v.co.z, rafter_top(v.co.x) - .01)
    # Roof: two slopes of sheets running down the slope, an end lap part way down; two clear skylights a side.
    sky = g['Skylight']
    slope_len = math.hypot(HW + OVER_EAVE, (HW + OVER_EAVE) * PITCH)
    for sx in (-1, 1):
        down = Vector((sx, 0, -PITCH)).normalized()
        n = Vector((sx * PITCH, 0, 1)).normalized()
        ridge = Vector((0, 0, roof_z(0) + .003))
        y = -HD - OVER_GABLE
        i = 0
        while y < HD + OVER_GABLE - .01:
            w = min(SHEET_W, HD + OVER_GABLE - y)
            clear = i in (2, 7)
            for k, (s0, s1) in enumerate(((0, slope_len * .55 + .1), (slope_len * .55 - .1, slope_len))):
                lift = (i % 2) * .003 + (.004 if k == 1 else 0)
                start = ridge + down * s0 + n * lift + Vector((0, y if sx < 0 else y + w, 0))
                across = Vector((0, 1 if sx < 0 else -1, 0))
                if clear:
                    sheet_strip(sky, sky, start, down, across, s1 - s0, w, n, rng.random(), bow=.002)
                else:
                    sheet_strip(so, si, start, down, across, s1 - s0, w, n, rng.random(), bow=.003)
            y += SHEET_W - SHEET_LAP
            i += 1

    # ------------------------------------------------------------ trims, gutters, shutter (galvanised)
    gv, tr = g['Galv'], g['Trim']
    top = roof_z(0)
    # Ridge cap: a bent strip over the apex.
    for sx in (-1, 1):
        a = Vector((0, -HD - OVER_GABLE - .02, top + .03))
        b = Vector((sx * .17, -HD - OVER_GABLE - .02, top + .03 - .17 * PITCH))
        a2, b2 = a + Vector((0, D + 2 * OVER_GABLE + .04, 0)), b + Vector((0, D + 2 * OVER_GABLE + .04, 0))
        gv.strip([[a, b], [a2, b2]], double=True)
    # Barge boards along both gable edges (painted timber trim).
    for sy in (-1, 1):
        y = sy * (HD + OVER_GABLE + .015)
        for sx in (-1, 1):
            a = (sx * .02, y, roof_z(0) - .06)
            b = (sx * (HW + OVER_EAVE), y, roof_z(HW + OVER_EAVE) - .06)
            tr.beam(a, b, .025, .2, up=(-sx * PITCH, 0, 1), bevel=.003)
    # Corner flashings: L angles up each vertical corner.
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (HW + .012), sy * (HD + .012)
            gv.box((min(x, x - sx * .11), min(y, y + sy * .002), SLAB - .1), (max(x, x - sx * .11), max(y, y + sy * .002), PLATE + .1))
            gv.box((min(x, x + sx * .002), min(y, y - sy * .11), SLAB - .1), (max(x, x + sx * .002), max(y, y - sy * .11), PLATE + .1))
    # Half-round gutters on both eaves with brackets, and one downpipe at the front left.
    for sx in (-1, 1):
        xg = sx * (HW + OVER_EAVE + .04)
        zg = roof_z(HW + OVER_EAVE) - .1
        seg = 10
        profile = [(xg - sx * .06 * math.cos(math.pi * k / seg), zg - .06 * math.sin(math.pi * k / seg)) for k in range(seg + 1)]
        gv.strip([[(px, -HD - OVER_GABLE, pz) for px, pz in profile], [(px, HD + OVER_GABLE, pz) for px, pz in profile]], double=True)
        for y in (-HD - OVER_GABLE + .1, -1.5, 0, 1.5, HD + OVER_GABLE - .1):
            gv.box((xg - .065 if sx > 0 else xg - .065, y - .015, zg - .07), (xg + .065, y + .015, zg - .06))
    dx, dy = -(HW + OVER_EAVE + .04), -HD - .12
    zg = roof_z(HW + OVER_EAVE) - .14
    gv.tube((dx, -HD - OVER_GABLE + .2, zg), (dx, dy, zg - .25), .038)
    gv.tube((dx, dy, zg - .25), (-(HW + .06), dy, zg - .45), .038)
    gv.tube((-(HW + .06), dy, zg - .45), (-(HW + .06), dy, .12), .038)
    gv.tube((-(HW + .06), dy, .12), (-(HW + .06), dy - .18, .04), .038)
    for z in (.6, 1.6, 2.4):
        gv.box((-(HW + .11), dy - .05, z), (-(HW + .005), dy + .05, z + .03))
    # Roller shutter: box on the gable over the door, guides down both jambs, bottom bar showing.
    yb = -HD - .005
    gv.box((-DOOR_HALF - .14, yb - .33, DOOR_TOP), (DOOR_HALF + .14, yb, DOOR_TOP + .32), bevel=.006)
    for sx in (-1, 1):
        x = sx * (DOOR_HALF + .02)
        gv.box((x - .045, yb - .07, SLAB), (x + .045, yb, DOOR_TOP + .02))
    gv.box((-DOOR_HALF + .02, yb - .06, DOOR_TOP - .1), (DOOR_HALF - .02, yb - .02, DOOR_TOP))
    gv.box((-.12, yb - .09, DOOR_TOP - .085), (.12, yb - .06, DOOR_TOP - .065))
    # Door and window trim (painted angle flashing).
    for sx in (-1, 1):
        x = sx * (DOOR_HALF + .07)
        tr.box((x - .03, yb - .012, SLAB), (x + .03, yb + .004, DOOR_TOP))
    # ------------------------------------------------------------ windows (timber frames, dirty glass)
    gl = g['Glass']
    for sx in (-1, 1):
        x = sx * (HW + .03)
        tr.box((x - .04, -WIN_HALF - .06, WIN_LOW - .07), (x + .04, WIN_HALF + .06, WIN_LOW))
        tr.box((x - .03, -WIN_HALF - .06, WIN_HIGH), (x + .03, WIN_HALF + .06, WIN_HIGH + .06))
        for y in (-WIN_HALF - .03, WIN_HALF + .03):
            tr.box((x - .03, y - .03, WIN_LOW), (x + .03, y + .03, WIN_HIGH))
        tr.box((x - .02, -.02, WIN_LOW), (x + .02, .02, WIN_HIGH))
        tr.box((x - .02, -WIN_HALF, (WIN_LOW + WIN_HIGH) / 2 - .02), (x + .02, WIN_HALF, (WIN_LOW + WIN_HIGH) / 2 + .02))
        gl.box((x - .003, -WIN_HALF, WIN_LOW), (x + .003, WIN_HALF, WIN_HIGH))

    # ------------------------------------------------------------ fittings: bench, vice, racking, lamps
    f = g['Fittings']
    t.kind = 2  # the bench: planed, oiled and stained
    bx, by0, by1 = -HW + .1, -2.2, -.2
    t.box((bx, by0, SLAB + .86), (bx + .75, by1, SLAB + .92), bevel=.004)  # thick timber top
    for xx in (bx + .05, bx + .68):
        for yy in (by0 + .06, by1 - .06):
            t.box((xx, yy - .035, SLAB), (xx + .07, yy + .035, SLAB + .86), bevel=.004)
    t.box((bx + .05, by0 + .06, SLAB + .18), (bx + .72, by1 - .06, SLAB + .21), bevel=.003)
    t.box((bx + .05, by0 + .03, SLAB + .7), (bx + .72, by0 + .1, SLAB + .78))
    t.box((bx + .05, by1 - .1, SLAB + .7), (bx + .72, by1 - .03, SLAB + .78))
    t.kind = 0
    # Bench vice at the front corner: body, fixed and moving jaws, screw and tommy bar.
    vx, vy, vz = bx + .78, by0 + .35, SLAB + .92
    pv = g['Rack']  # painted steel shares the racking finish
    pv.box((vx - .2, vy - .09, vz), (vx - .02, vy + .09, vz + .1), bevel=.008)
    pv.box((vx - .05, vy - .1, vz + .02), (vx + .02, vy + .1, vz + .2), bevel=.006)
    pv.box((vx + .06, vy - .1, vz + .03), (vx + .13, vy + .1, vz + .2), bevel=.006)
    pv.box((vx + .02, vy - .05, vz + .05), (vx + .06, vy + .05, vz + .1))
    f.kind = 2
    f.tube((vx + .13, vy, vz + .09), (vx + .3, vy, vz + .09), .012)
    f.tube((vx + .29, vy - .16, vz + .09), (vx + .29, vy + .16, vz + .09), .008)
    f.kind = 0
    # Slotted-angle racking along the back wall: two bays, four shelves each.
    rk = g['Rack']
    for x0, x1 in ((-3.3, -1.3), (-1.2, .8)):
        y0, y1 = HD - .55, HD - .1
        for xx in (x0, x1):
            for yy in (y0, y1):
                rk.box((xx - .02, yy - .02, SLAB), (xx + .02, yy + .02, SLAB + 2.1))
        for z in (SLAB + .15, SLAB + .75, SLAB + 1.35, SLAB + 1.95):
            t.kind = 1
            t.box((x0 - .02, y0 - .02, z), (x1 + .02, y1 + .02, z + .018))  # chipboard shelves
            t.kind = 0
            rk.box((x0, y0 - .025, z - .04), (x1, y0 - .015, z))
            rk.box((x0, y1 + .015, z - .04), (x1, y1 + .025, z))
    # Twin fluorescent battens hung under the collar ties.
    lamp = g['TubeLamp']
    for y in (-1.5, 1.5):
        zc = PLATE + 1.0 - .09
        f.box((-.78, y - .06, zc - .07), (.78, y + .06, zc - .02))
        lamp.box((-.75, y - .045, zc - .1), (.75, y + .045, zc - .07))
        f.kind = 1
        f.tube((-.6, y, zc - .02), (-.6, y, zc), .004, segments=6)
        f.tube((.6, y, zc - .02), (.6, y, zc), .004, segments=6)
        f.kind = 0

    return {name: group.finish(collection) for name, group in groups.items()}
