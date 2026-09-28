"""Two original rim-winch candidates with wire rope; execute through Blender MCP.

A: skid-mounted electric drum winch with a tripod headframe, lead and head sheaves, hook block.
B: trailer jib crane: diesel turret, hydraulic drum winch, lattice boom on pendant ropes, ball hook.

Coordinates below are Unity axes (x right, y up, z forward toward the hole) in base units: every
machine is built at 1/K of its size and exported K times larger, because it hauls oversized
salvage. Parts a person uses (controls, levers, ladders, sandbags) are authored inside `human()`
blocks, which keep real metres around a scaled anchor. The origin is the ground under the machine.

Both machines slew: after the rope lifts a load clear of the hole, the upper works turn to set it
down beside the rim. Each model exports a fixed root (`A_Base`, `B_Chassis`) and a slewing root
(`A_Slew`, `B_Turret`, origin on the slew axis at the ring top, turning about local y) that parents
everything that swings with it: `Drum` and `*Sheave` (origin on their axle, turning about local x),
`Rope` (the free rope), `Attachment` (the small device that sticks onto the find: A's lifting magnet,
B's suction cup; origin at the rope end) and, on B, `Jib` (origin on the foot pin,
luffing about local x, parenting `TipSheave`) and `Pendants` (the jib's fixed ropes). Faces use shared materials `SDT_Winch_<Key>`, which Unity remaps to project materials by name.
"""
import bpy, bmesh, math
from contextlib import contextmanager
from pathlib import Path
from mathutils import Vector

ROOT = Path(r'C:/Users/pavel/Desktop/Dev/CompanyProjects/something-down-there')
OUT = ROOT / 'unity/Assets/Content/WinchCandidates/Models'
OUT.mkdir(parents=True, exist_ok=True)
NAME = 'SDT_WinchCandidates'
previous = bpy.context.window.scene
if NAME in bpy.data.scenes:
    old = bpy.data.scenes[NAME]
    for obj in list(old.objects): bpy.data.objects.remove(obj, do_unlink=True)
    for coll in list(old.collection.children): bpy.data.collections.remove(coll)
    bpy.data.scenes.remove(old)
scene = bpy.data.scenes.new(NAME)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'

# Blender preview looks only; WinchCandidatesSetup owns the tuned Unity materials.
LOOKS = {'Yellow': (.72, .43, .07), 'Blue': (.16, .31, .42), 'DarkSteel': (.16, .17, .18), 'Steel': (.55, .56, .57),
         'Grey': (.42, .44, .45), 'Rope': (.3, .29, .27), 'Rubber': (.05, .05, .05), 'Black': (.06, .06, .065),
         'Red': (.62, .09, .06), 'Green': (.12, .45, .16), 'Amber': (.95, .55, .08), 'Cream': (.86, .82, .7),
         'Wood': (.42, .27, .15), 'Rust': (.38, .2, .1), 'Canvas': (.44, .39, .27)}
METAL = {'DarkSteel': .6, 'Steel': .75, 'Rope': .55, 'Grey': .3, 'Rust': .3}
MATS = {}
for key, colour in LOOKS.items():
    mat = bpy.data.materials.get('SDT_Winch_' + key) or bpy.data.materials.new('SDT_Winch_' + key)
    mat.diffuse_color = (*colour, 1)
    bsdf = mat.node_tree.nodes.get('Principled BSDF') if mat.node_tree else None
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (*colour, 1)
        bsdf.inputs['Metallic'].default_value = METAL.get(key, 0)
        bsdf.inputs['Roughness'].default_value = .45 if key in METAL else .7
    MATS[key] = mat

K = 3.0  # machine scale
ROPE_R = .026 / K  # 52 mm wire rope; SalvageWinchSettings.RopeRadius should follow when it is wired
Y, B, D, S = 'Yellow', 'Blue', 'DarkSteel', 'Steel'


# ---------- geometry kit (Unity coordinates in, Blender mesh out) ----------
HUMAN = None  # (reference, landing) of the current human() block, in base units
LIFT = 0.0  # base units the slewing upper works stand above their authored height (the slew ring)


def P(v):
    v, lift = Vector(v), Vector((0, LIFT, 0))
    w = (v + lift) * K if HUMAN is None else (HUMAN[1] + lift) * K + (v - HUMAN[0])
    return Vector((w.x, w.z, w.y))


@contextmanager
def human(ref, at=None):
    """Geometry inside keeps real size: offsets from `ref` (base units) are real metres, placed
    around `at` (default `ref`) on the scaled machine."""
    global HUMAN
    HUMAN = (Vector(ref), Vector(ref if at is None else at))
    try: yield
    finally: HUMAN = None


def real(anchor, offset):
    """Base-unit point for a real-metre offset from a scaled anchor."""
    return Vector(anchor) + Vector(offset) / K


class Part:
    def __init__(self, name, origin, parent):
        self.name, self.pivot, self.parent, self.bm, self.keys = name, P(origin), parent, bmesh.new(), []

    def slot(self, key):
        if key not in self.keys: self.keys.append(key)
        return self.keys.index(key)


class Model:
    def __init__(self, name): self.name, self.parts = name, {}

    def part(self, name, origin=(0, 0, 0), parent=None):
        global CUR
        if name not in self.parts: self.parts[name] = Part(name, origin, parent)
        CUR = self.parts[name]
        return CUR


CUR = None


def emit(bm, key):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    index = CUR.slot(key)
    for face in bm.faces: face.material_index = index; face.smooth = True
    mesh = bpy.data.meshes.new('_tmp'); bm.to_mesh(mesh); bm.free()
    CUR.bm.from_mesh(mesh); bpy.data.meshes.remove(mesh)


def basis(axis):
    a = Vector(axis).normalized()
    if abs(abs(a.x) - 1) < 1e-6: return a, Vector((0, 0, 1)), Vector((0, 1, 0))  # zy-plane angles: 0 = +z, 90 = up
    ref = Vector((0, 1, 0)) if abs(a.y) < .9 else Vector((1, 0, 0))
    u = a.cross(ref).normalized()
    return a, u, a.cross(u).normalized()


def obox(key, centre, axes, size, bevel=0.0, seg=2):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    c = Vector(centre); ax = [Vector(x).normalized() for x in axes]
    for vert in bm.verts:
        x, y, z = vert.co
        vert.co = P(c + ax[0] * x * size[0] + ax[1] * y * size[1] + ax[2] * z * size[2])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=min(bevel, min(size) * .45), segments=seg,
                        affect='EDGES', profile=.5, clamp_overlap=True)
    emit(bm, key)


def box(key, centre, size, bevel=.01, yaw=0.0, seg=2):
    c, s = math.cos(yaw), math.sin(yaw)
    obox(key, centre, ((c, 0, -s), (0, 1, 0), (s, 0, c)), size, bevel, seg)


def beam(key, a, b, width, height, up=(0, 1, 0), bevel=.006):
    a, b = Vector(a), Vector(b); d = (b - a).normalized()
    side = d.cross(Vector(up)).normalized(); top = side.cross(d).normalized()
    obox(key, (a + b) / 2, (side, top, d), (width, height, (b - a).length), bevel)


def lathe(key, centre, axis, profile, n=24, a0=0.0, a1=2 * math.pi, closed=False):
    """Revolve (radius, offset-along-axis) points about `axis` through `centre`."""
    c = Vector(centre); a, u, v = basis(axis)
    full = abs(a1 - a0 - 2 * math.pi) < 1e-6
    count = n if full else n + 1
    bm = bmesh.new(); rings = []
    for r, t in profile:
        if r < 1e-6:
            rings.append([bm.verts.new(P(c + a * t))] * count)
        else:
            rings.append([bm.verts.new(P(c + a * t + (u * math.cos(a0 + (a1 - a0) * i / n) + v * math.sin(a0 + (a1 - a0) * i / n)) * r))
                          for i in range(count)])
    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for r0, r1 in pairs:
        for i in range(n):
            j = (i + 1) % count
            quad = []
            for q in (r0[i], r0[j], r1[j], r1[i]):
                if q not in quad: quad.append(q)
            if len(quad) >= 3: bm.faces.new(quad)
    if not full and closed:
        for index in (0, count - 1):
            cap = []
            for ring in rings:
                if ring[index] not in cap: cap.append(ring[index])
            bm.faces.new(cap)
    emit(bm, key)


def cyl(key, a, b, r, r2=None, n=16):
    a, b = Vector(a), Vector(b)
    lathe(key, a, b - a, [(0, 0), (r, 0), (r if r2 is None else r2, (b - a).length), (0, (b - a).length)], n)


def ring(key, centre, axis, radius, tube, n=20, m=6):
    a, u, v = basis(axis); c = Vector(centre)
    sweep(key, [c + (u * math.cos(2 * math.pi * i / n) + v * math.sin(2 * math.pi * i / n)) * radius for i in range(n)],
          tube, m, closed=True)


def sphere(key, centre, r, axis=(0, 1, 0), n=16):
    lathe(key, Vector(centre) - Vector(axis).normalized() * r, axis,
          [(r * math.sin(math.pi * i / 8), r - r * math.cos(math.pi * i / 8)) for i in range(9)], n)


def prism(key, poly, origin, uax, vax, depth):
    """Extrude a 2D polygon, drawn in the (u, v) plane at `origin`, by `depth` along u x v."""
    o, u, v = Vector(origin), Vector(uax), Vector(vax); n = u.cross(v).normalized()
    bm = bmesh.new()
    bottom = [bm.verts.new(P(o + u * p[0] + v * p[1])) for p in poly]
    top = [bm.verts.new(P(o + u * p[0] + v * p[1] + n * depth)) for p in poly]
    bm.faces.new(bottom); bm.faces.new(top)
    for i in range(len(poly)):
        j = (i + 1) % len(poly)
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    emit(bm, key)


def sweep(key, pts, radius, n=8, rfn=None, closed=False, caps=True):
    """Tube along a polyline with parallel-transported frames; radius may be a function of length."""
    pts = [Vector(p) for p in pts]; m = len(pts)
    tangents = []
    for i in range(m):
        t = (pts[(i + 1) % m] - pts[i - 1]) if closed else (pts[min(i + 1, m - 1)] - pts[max(i - 1, 0)])
        tangents.append(t.normalized())
    ref = Vector((0, 1, 0)) if abs(tangents[0].y) < .9 else Vector((1, 0, 0))
    normal = tangents[0].cross(ref).normalized()
    lengths = [0.0]
    for i in range(1, m): lengths.append(lengths[-1] + (pts[i] - pts[i - 1]).length)
    bm = bmesh.new(); rings = []
    for i, p in enumerate(pts):
        if i:
            normal = tangents[i - 1].rotation_difference(tangents[i]) @ normal
            normal = (normal - tangents[i] * normal.dot(tangents[i])).normalized()
        binormal = tangents[i].cross(normal)
        r = radius(lengths[i]) if callable(radius) else radius
        rings.append([bm.verts.new(P(p + (normal * math.cos(2 * math.pi * k / n) + binormal * math.sin(2 * math.pi * k / n))
                                     * r * (rfn(lengths[i], 2 * math.pi * k / n) if rfn else 1))) for k in range(n)])
    for i in range(m if closed else m - 1):
        r0, r1 = rings[i], rings[(i + 1) % m]
        for k in range(n): bm.faces.new((r0[k], r0[(k + 1) % n], r1[(k + 1) % n], r1[k]))
    if caps and not closed:
        bm.faces.new(rings[0]); bm.faces.new(rings[-1])
    emit(bm, key)


def resample(pts, step):
    pts = [Vector(p) for p in pts]; out = [pts[0]]; carry = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = (b - a).length; t = step - carry
        while t < seg:
            out.append(a + (b - a) * (t / seg)); t += step
        carry = seg - (t - step)
    if (out[-1] - pts[-1]).length > step * .3: out.append(pts[-1])
    else: out[-1] = pts[-1]
    return out


def smooth(pts, per=6):
    """Catmull-Rom through the given points."""
    pts = [Vector(p) for p in pts]; out = []
    ext = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(per):
            t = k / per
            out.append(.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(pts[-1])
    return out


def rope(pts, r=ROPE_R, lay=.36 / K, key='Rope'):
    """Three-strand wire rope: the strands' crests twist once per lay length."""
    sweep(key, resample(pts, .045 / K), r, 12, lambda s, th: 1 + .16 * math.cos(3 * (th - 2 * math.pi * s / lay)))


def ladder(foot, height, across, rung_top=None, lean=(0, 0, 0), width=.42, key=Y):
    """Real-size ladder standing at `foot` (base units): stiles `height` real metres tall, rungs
    every 0.3 m up to `rung_top`; `lean` shifts the top (real metres)."""
    a = Vector(across).normalized(); top = Vector((0, height, 0)) + Vector(lean)
    with human(foot):
        for side in (-.5, .5): cyl(key, foot + a * width * side, foot + top + a * width * side, .022, n=10)
        for k in range(1, int((rung_top or height) / .3) + 1):
            p = foot + top * (k * .3 / height)
            cyl(S, p - a * width / 2, p + a * width / 2, .016, n=8)


def bolt(centre, axis, r=.016, h=.014, key=S):
    a = Vector(axis).normalized(); cyl(key, centre, Vector(centre) + a * h, r, n=6)


def bolt_ring(centre, axis, radius, count, r=.016, key=S, phase=0.0):
    a, u, v = basis(axis)
    for i in range(count):
        ang = phase + 2 * math.pi * i / count
        bolt(Vector(centre) + (u * math.cos(ang) + v * math.sin(ang)) * radius, a, r, key=key)


def gear(key, centre, axis, radius, teeth, thick, tooth=.022):
    c = Vector(centre); a, u, v = basis(axis)
    lathe(key, c, a, [(0, -thick / 2), (radius, -thick / 2), (radius, thick / 2), (0, thick / 2)], max(24, teeth))
    for i in range(teeth):
        d = u * math.cos(2 * math.pi * i / teeth) + v * math.sin(2 * math.pi * i / teeth)
        obox(key, c + d * (radius + tooth * .4), (d, a.cross(d), a), (tooth, math.pi * radius / teeth, thick))


def sheave(centre, axis, pitch, width=.09, hub=.05, spokes=6, rim=S, web=D):
    """Grooved rope wheel; `pitch` is the rope centreline radius."""
    c = Vector(centre); a, u, v = basis(axis); g = pitch - ROPE_R; w = width / 2
    lathe(rim, c, a, [(g - .035, -w), (g + .03, -w), (g + .04, -w * .8), (g + .008, -w * .4), (g, 0), (g + .008, w * .4),
                      (g + .04, w * .8), (g + .03, w), (g - .035, w)], 36, closed=True)
    lathe(web, c, a, [(0, -w * 1.3), (hub, -w * 1.3), (hub, w * 1.3), (0, w * 1.3)], 16)
    for i in range(spokes):
        d = u * math.cos(2 * math.pi * i / spokes) + v * math.sin(2 * math.pi * i / spokes)
        obox(web, c + d * ((hub + g - .03) / 2), (d, a, d.cross(a)), (g - .03 - hub + .02, width * .45, .03))


def drum_rope(centre, core, x0, x1, x_end, end_angle):
    """Rope wound on a drum turning about x: two full ridged layers, then a partial outer layer
    whose last wrap leaves the drum at (x_end, end_angle); zy-plane angles, 0 = +z, 90 = up."""
    c = Vector(centre); d = 2 * ROPE_R; inner = core + 2 * d
    wraps = int((x1 - x0) / d)
    profile = [(core - .01, x0)]
    for k in range(wraps):
        profile += [(inner - .007, x0 + k * d + .002), (inner, x0 + (k + .5) * d)]
    profile += [(inner - .007, x1 - .002), (core - .01, x1)]
    lathe('Rope', c, (1, 0, 0), profile, 32, closed=True)
    radius = inner + ROPE_R; start = x0 + ROPE_R; turns = (x_end - start) / d; steps = int(turns * 22) + 2
    pts = []
    for i in range(steps + 1):
        f = i / steps; ang = end_angle + 2 * math.pi * turns * (1 - f)
        pts.append(c + Vector((start + (x_end - start) * f, radius * math.sin(ang), radius * math.cos(ang))))
    sweep('Rope', pts, ROPE_R, 8)


def tangent(c1, r1, s1, c2, r2, s2):
    """Rope line between two wheels in the (z, y) plane; s = +1 when the wheel is on the rope's left
    (the rope wraps it anticlockwise), -1 on its right. Returns both touch points."""
    c1, c2 = Vector(c1), Vector(c2); D_ = c2 - c1; L = D_.length; dh = D_ / L; ph = Vector((-dh.y, dh.x))
    a = (s2 * r2 - s1 * r1) / L; n = dh * a + ph * math.sqrt(max(0, 1 - a * a))
    return c1 - n * (s1 * r1), c2 - n * (s2 * r2)


def wrap(c, r, s, p_in, p_out, x, step=.02):
    """Arc of rope around a wheel in the (z, y) plane at lateral x, from p_in to p_out."""
    a0 = math.atan2(p_in.y - c[1], p_in.x - c[0]); a1 = math.atan2(p_out.y - c[1], p_out.x - c[0])
    if s > 0 and a1 < a0: a1 += 2 * math.pi
    if s < 0 and a1 > a0: a1 -= 2 * math.pi
    n = max(2, int(abs(a1 - a0) * r / step) + 1)
    return [Vector((x, c[1] + r * math.sin(a0 + (a1 - a0) * i / (n - 1)), c[0] + r * math.cos(a0 + (a1 - a0) * i / (n - 1))))
            for i in range(n)]


def zy(x, p): return Vector((x, p.y, p.x))


def clip(poly, a, b, c):  # keep a*u + b*v <= c
    out = []
    for i in range(len(poly)):
        p, q = poly[i], poly[(i + 1) % len(poly)]
        fp, fq = a * p[0] + b * p[1] - c, a * q[0] + b * q[1] - c
        if fp <= 0: out.append(p)
        if fp * fq < 0:
            t = fp / (fp - fq); out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def hazard(origin, uax, vax, width, height, stripe=.08, t=.004, colour=Y):
    """Diagonal hazard stripes on a W x H panel; u x v must point out of the surface."""
    k, c = 0, -height
    while c < width:
        poly = clip(clip([(0, 0), (width, 0), (width, height), (0, height)], 1, 1, c + stripe), -1, -1, -c)
        if len(poly) >= 3: prism((colour, 'Black')[k % 2], poly, origin, uax, vax, t)
        c += stripe; k += 1


def checker(key, x0, x1, z0, z1, y, step=.1):
    nx, nz = int((x1 - x0) / step), int((z1 - z0) / step)
    for i in range(nx):
        for k in range(nz):
            box(key, (x0 + (i + .5) * (x1 - x0) / nx, y + .003, z0 + (k + .5) * (z1 - z0) / nz), (.012, .006, .05), 0,
                math.radians(45 if (i + k) % 2 else -45))


def socket(top, length=.13, r=.045, axis=(0, -1, 0)):
    lathe(S, top, axis, [(0, 0), (ROPE_R + .004, 0), (r, length * .8), (r, length), (0, length)], 16)


def swivel(top, r=.04, h=.08):
    lathe(S, top, (0, -1, 0), [(0, 0), (r * .75, 0), (r, h * .25), (r, h * .75), (r * .6, h), (0, h)], 16)


def dome(key, centre, axis, r):
    lathe(key, centre, axis, [(0, 0), (r, 0), (r, r * .4), (r * .6, r * .9), (0, r)], 16)


# Attachments are small, real-size devices at the rope end (a unique is only about half a metre):
# they stick flat onto the marked point of the find. Offsets inside are real metres.
def magnet(top):
    """Compact lifting magnet, a 0.6 m puck on a swivel: its pole face clamps onto a metal find."""
    t = Vector(top); up_ = Vector((0, 1, 0))
    with human(t):
        lathe(S, t, -up_, [(0, 0), (ROPE_R * K + .004, 0), (.05, .16), (.05, .2), (0, .2)], 16)  # rope socket
        ring(S, t - up_ * .23, (1, 0, 0), .035, .012)
        swivel(t - up_ * .26)
        r, h = .3, .16
        c = t - up_ * (.36 + h)  # bottom centre of the puck
        lathe(D, c, up_, [(0, 0), (r - .012, 0), (r, .014), (r, h - .014), (r - .012, h), (0, h)], 36)
        lathe(Y, c + up_ * h, up_, [(0, 0), (.24, 0), (.23, .03), (.08, .05), (0, .055)], 36)
        for i in range(8):  # radial ribs on the cap
            d = Vector((math.cos(i * math.pi / 4), 0, math.sin(i * math.pi / 4)))
            obox(D, c + up_ * (h + .02) + d * .16, (d, up_, d.cross(up_)), (.14, .035, .014), .003)
        for i in range(20):  # hazard band
            a0 = 2 * math.pi * i / 20
            lathe(Y if i % 2 else 'Black', c + up_ * (h * .5), up_, [(r + .001, -.03), (r + .007, -.03), (r + .007, .03), (r + .001, .03)],
                  2, a0, a0 + 2 * math.pi / 20, closed=True)
        lathe('Black', c - up_ * .008, up_, [(0, 0), (r - .015, 0), (r - .015, .008), (0, .008)], 36)  # pole face
        lathe(S, c - up_ * .014, up_, [(0, 0), (.09, 0), (.09, .014), (0, .014)], 24)
        lathe(S, c - up_ * .014, up_, [(.22, 0), (.28, 0), (.28, .014), (.22, .014)], 36, closed=True)
        lathe(D, t - up_ * .34, up_, [(0, 0), (.06, 0), (.06, .02), (0, .02)], 16)  # swivel mount on the cap
        tb = c + Vector((.12, h + .07, -.08))  # terminal box and power cable up to the socket
        box(D, tb, (.09, .07, .07), .006)
        box(D, t + Vector((.055, -.12, 0)), (.03, .05, .04), .004)
        sweep('Rubber', smooth([tb + up_ * .035, tb + Vector((.03, .12, .04)), t + Vector((.1, -.24, .02)), t + Vector((.07, -.14, 0))], 6), .014, 8)
        dome('Amber', c + Vector((-.14, h + .035, .06)), up_, .022)


def suction_cup(top):
    """Single 0.6 m suction cup under a compact vacuum canister: it seals onto any smooth face."""
    t = Vector(top); up_ = Vector((0, 1, 0))
    with human(t):
        obox(D, t - up_ * .09, ((1, 0, 0), up_, (0, 0, 1)), (.08, .18, .06), .008)  # wedge socket and pin
        cyl(S, t + Vector((-.05, -.15, 0)), t + Vector((.05, -.15, 0)), .014, n=8)
        swivel(t - up_ * .18)
        cc = t - up_ * .26  # vacuum canister, top at cc
        lathe(Y, cc - up_ * .2, up_, [(0, 0), (.13, 0), (.14, .02), (.14, .18), (.12, .2), (0, .2)], 28)
        lathe(D, cc - up_ * .2, up_, [(.13, .08), (.145, .08), (.145, .1), (.13, .1)], 28, closed=True)
        front = Vector((0, 0, 1))
        lathe('Black', cc + Vector((0, -.07, .137)), front, [(0, 0), (.04, 0), (.04, .01), (0, .01)], 18)
        lathe('Cream', cc + Vector((0, -.07, .147)), front, [(0, 0), (.033, 0), (.033, .002), (0, .002)], 18)
        obox('Red', cc + Vector((0, -.06, .15)), ((0, 1, 0), (1, 0, 0), front), (.025, .004, .002))
        dome('Green', cc + Vector((.08, -.05, .115)), Vector((.6, 0, .8)), .014)
        sweep('Rubber', smooth([cc + Vector((.13, -.12, 0)), cc + Vector((.2, -.2, .02)), cc + Vector((.08, -.29, 0))], 6), .016, 8)
        cp = cc - up_ * .2  # stem, backing plate and the rubber cup
        cyl(S, cp, cp - up_ * .06, .03, n=12)
        lathe(D, cp - up_ * .06, (0, -1, 0), [(0, 0), (.1, 0), (.1, .02), (0, .02)], 24)
        lathe('Rubber', cp - up_ * .08, (0, -1, 0), [(.05, 0), (.07, 0), (.29, .11), (.3, .13), (.28, .13), (.06, .02)], 32, closed=True)


def chain(start, end, sag=.06):
    """Alternating steel links between two points."""
    start, end = Vector(start), Vector(end); d = (end - start).normalized()
    links = int((end - start).length / .055)
    for i in range(links):
        f = (i + .5) / links; p = start + (end - start) * f + Vector((0, -sag * math.sin(math.pi * f), 0))
        side = Vector((1, 0, 0)) if i % 2 else d.cross(Vector((1, 0, 0))).normalized()
        loop = [p + d * (.022 * math.cos(2 * math.pi * k / 16) + (.012 if math.cos(2 * math.pi * k / 16) > 0 else -.012))
                + side * (.016 * math.sin(2 * math.pi * k / 16)) for k in range(16)]
        sweep(D, loop, .006, 5, closed=True)


def model_a():
    global LIFT
    m = Model('A'); m.part('Base')
    ax = -.35  # slew axis z; the upper works turn on the ring above the cross base
    for sx in (-1, 1):  # crossed I-beam arms on timber cribs, chained to stakes at the back
        beam(Y, (-1.05 * sx, .17, ax - 1.05), (1.05 * sx, .17, ax + 1.05), .16, .1)
        for fz in (ax - 1.05, ax + 1.05):
            px = 1.05 * (sx if fz < 0 else -sx)
            for k in range(3):
                box('Wood', (px, .03, fz - .18 + .18 * k), (.55, .06, .12), .01, .03 * sx)
                box('Wood', (px - .18 + .18 * k, .09, fz), (.12, .06, .55), .01, -.03 * sx)
            box(D, (px, .125, fz), (.3, .01, .3), .003)
        eye, stake = Vector((sx * 1.1, .17, ax - 1.17)), Vector((sx * 1.3, .16, ax - 1.9))
        ring(S, eye, (1, 0, 0), .045, .012)
        cyl(S, (stake.x, -.2, stake.z), (stake.x, .2, stake.z), .022, n=10)
        ring(S, (stake.x, .2, stake.z), (1, 0, 0), .035, .01)
        chain(eye + Vector((0, 0, -.04)), stake + Vector((0, 0, .03)))
    box(D, (0, .17, ax), (.6, .1, .6), .01)
    lathe(D, (0, .22, ax), (0, 1, 0), [(.42, 0), (.55, 0), (.55, .06), (.42, .06)], 48, closed=True)
    for i in range(48):  # fixed ring gear the slew pinion walks round
        d = Vector((math.cos(2 * math.pi * i / 48), 0, math.sin(2 * math.pi * i / 48)))
        obox(D, d * .56 + Vector((0, .25, ax)), (d, (0, 1, 0), d.cross(Vector((0, 1, 0)))), (.03, .05, .035))

    LIFT = .16  # everything below is authored at its old skid height and stands on the ring
    m.part('Slew', (0, .12, ax))
    lathe(D, (0, .06, ax), (0, 1, 0), [(.28, 0), (.41, 0), (.41, .06), (.28, .06)], 40, closed=True)
    lathe(D, (0, .12, ax), (0, 1, 0), [(0, 0), (.46, 0), (.46, .04), (0, .04)], 40)
    gear(D, (0, .09, ax + .64), (0, 1, 0), .06, 12, .05)  # slew pinion, shaft and drive
    cyl(S, (0, .12, ax + .64), (0, .34, ax + .64), .02, n=10)
    box(D, (0, .37, ax + .64), (.16, .07, .16), .01)
    cyl(D, (0, .4, ax + .64), (0, .62, ax + .64), .07, n=16)
    for i in range(8):
        d = Vector((math.cos(i * math.pi / 4), 0, math.sin(i * math.pi / 4)))
        obox(D, Vector((0, .51, ax + .64)) + d * .075, (d, (0, 1, 0), d.cross(Vector((0, 1, 0)))), (.012, .2, .01))
    for sx in (-1, 1):  # outward-facing channel runners
        x = sx * .7
        box(Y, (x, .22, -.35), (.025, .2, 2.95), .006)
        for y in (.13, .31): box(Y, (x + sx * .04, y, -.35), (.09, .02, 2.95), .005)
        for z in (-1.82, 1.12): box(Y, (x + sx * .04, .22, z), (.09, .2, .02), .004)
    for z in (-1.72, -1.1, -.3, .45, 1.0): box(Y, (0, .22, z), (1.38, .12, .08), .008)
    box(Y, (0, .22, 1.14), (1.52, .2, .025), .004)
    hazard((-.74, .13, 1.153), (1, 0, 0), (0, 1, 0), 1.48, .18, .09)
    box(D, (0, .3275, -.335), (1.46, .015, 2.83), .003)
    checker(D, -.66, .66, -1.72, 1.05, .335, .13)

    cz, cy = -.85, .8  # drum axle
    drum_r, lead_r = .2 + 5 * ROPE_R, .16  # rope centreline on the outer layer and on the lead wheel
    ry = cy + drum_r  # the rope leaves the drum top level
    lead_c = Vector((1.0, ry + lead_r))
    arc = [(cz + .22 * math.cos(math.radians(a)), cy + .22 * math.sin(math.radians(a))) for a in range(0, 181, 15)]
    plate = [(-1.3, .335), (-.4, .335)] + arc
    for sx, x0 in ((1, .5), (-1, -.46)):
        prism(Y, plate, (x0, 0, 0), (0, 0, 1), (0, 1, 0), .04)
        box(Y, (sx * .48, .36, -.85), (.1, .05, .95), .008)
        cyl(D, (sx * .5, cy, cz), (sx * .57, cy, cz), .1, n=20)
        bolt_ring((sx * .57, cy, cz), (sx, 0, 0), .072, 4, phase=.78)
    box('Cream', (.5 + .003, .5, -.85), (.004, .08, .22), 0)

    m.part('Drum', (0, cy, cz), 'Slew')
    cyl(S, (-.72, cy, cz), (.66, cy, cz), .045)
    lathe(D, (0, cy, cz), (1, 0, 0), [(0, -.42), (.2, -.42), (.2, .42), (0, .42)], 32)
    drum_rope((0, cy, cz), .2, -.42, .42, .10, math.radians(90))
    for sx in (-1, 1):
        lathe(Y, (sx * .4375, cy, cz), (1, 0, 0), [(.19, -.0175), (.38, -.0175), (.38, .0175), (.19, .0175)], 36, closed=True)
        lathe(Y, (sx * .4375, cy, cz), (1, 0, 0), [(.37, -.024), (.385, -.024), (.385, .024), (.37, .024)], 36, closed=True)
        lathe(D, (sx * .47, cy, cz), (1, 0, 0), [(0, -.015), (.15, -.015), (.15, .015), (0, .015)], 24)
        bolt_ring((sx * .485, cy, cz), (sx, 0, 0), .11, 8)
    lathe(S, (-.63, cy, cz), (1, 0, 0), [(.05, -.03), (.22, -.03), (.22, .03), (.05, .03)], 36, closed=True)
    gear(D, (.61, cy, cz), (1, 0, 0), .19, 28, .014)

    m.part('Slew')
    my, mz = .56, -1.5  # motor behind the drum, chain drive on the right
    lathe(D, (0, my, mz), (1, 0, 0), [(0, -.36), (.12, -.36), (.15, -.33), (.165, -.28), (.165, .3), (.15, .33), (.12, .36), (0, .36)], 28)
    for i in range(16):
        ang = 2 * math.pi * i / 16
        if 3.6 < ang < 5.9: continue
        d = Vector((0, math.sin(ang), math.cos(ang)))
        obox(D, Vector((0, my, mz)) + d * .175, (d, (1, 0, 0), d.cross(Vector((1, 0, 0)))), (.022, .56, .012))
    lathe(D, (-.36, my, mz), (-1, 0, 0), [(0, 0), (.16, 0), (.16, .1), (.13, .13), (0, .14)], 28)
    for r in (.04, .08, .12): ring('Black', (-.5 - .01 * (r < .1), my, mz), (1, 0, 0), r, .006, 24)
    for x in (-.2, .2): box(D, (x, .37, mz), (.1, .07, .34), .01)
    box(D, (.05, .76, mz), (.16, .1, .14), .01)
    cyl(S, (-.03, .76, mz), (-.07, .76, mz), .018, n=10)
    box('Cream', (.05, .6, mz + .166), (.14, .07, .006), 0)
    cyl(S, (.36, my, mz), (.66, my, mz), .025, n=12)
    gear(D, (.61, my, mz), (1, 0, 0), .07, 12, .014)
    # Roller chain between the sprockets.
    big, small = Vector((cz, cy)), Vector((mz, my))
    top0, top1 = tangent(small, .085, -1, big, .205, -1)
    bot0, bot1 = tangent(big, .205, -1, small, .085, -1)
    loop = [zy(.61, top0), zy(.61, top1)] + wrap(big, .205, -1, top1, bot0, .61, .01) + [zy(.61, bot1)] \
        + wrap(small, .085, -1, bot1, top0, .61, .01)
    sweep('Black', resample(loop, .006)[:-1], .01, 6, lambda s, th: 1 + .45 * max(0, math.sin(2 * math.pi * s / .032)), closed=True)
    # Band brake with a hand lever.
    band = [Vector((-.63, cy + .235 * math.sin(math.radians(a)), cz + .235 * math.cos(math.radians(a)))) for a in range(-60, 251, 10)]
    sweep(D, band, .011, 6)
    pivot, reach = Vector((-.66, .45, -.5)), Vector((-.05, .95, .3))
    box(D, (-.66, .39, -.5), (.08, .1, .08), .01)
    with human(pivot):
        cyl(D, pivot, pivot + reach, .025, n=10)
        sphere('Red', pivot + reach + Vector((0, .02, 0)), .04)
    cyl(D, band[0], real(pivot, reach * .3), .007, n=6)
    cyl(D, band[-1], real(pivot, reach * .22), .007, n=6)
    # Level wind: diamond screw, guide bar and a rope carriage where the rope leaves the drum.
    for sx in (-1, 1): box(Y, (sx * .49, .78, -.5), (.035, .9, .07), .006)
    cyl(S, (-.49, .95, -.5), (.49, .95, -.5), .022)
    sweep(S, [Vector((-.46 + .92 * i / 330, .95 + .026 * math.sin(i * .38), -.5 + .026 * math.cos(i * .38))) for i in range(331)], .006, 5)
    cyl(S, (-.49, 1.19, -.5), (.49, 1.19, -.5), .018)
    for sx in (-1, 1):
        box(D, (.1 + sx * .065, 1.07, -.5), (.015, .3, .1), .004)
        cyl(S, (.1 + sx * .04, 1.0, -.5), (.1 + sx * .04, 1.18, -.5), .02, n=12)
    for y in (ry - .035, ry + .035): cyl(S, (.04, y, -.5), (.16, y, -.5), .016, n=12)
    box(D, (.1, .95, -.5), (.12, .06, .08), .01)
    cyl(D, (.03, 1.19, -.5), (.17, 1.19, -.5), .03, n=12)
    # Control post on the deck at real size, its enclosure facing the operator inboard (+x).
    post = Vector((-.55, .335, .45))
    def q(x, y, z): return post + Vector((x, y, z))
    with human(post):
        box(D, q(0, .48, 0), (.06, .96, .06), .006)
        box(D, q(0, .01, 0), (.16, .02, .16), .004)
        box('Grey', q(.04, .87, 0), (.12, .36, .28), .015)
        f = .1
        lathe(Y, q(f, .97, -.07), (1, 0, 0), [(0, 0), (.05, 0), (.05, .008), (0, .008)], 20)
        lathe('Red', q(f, .97, -.07), (1, 0, 0), [(0, 0), (.022, 0), (.022, .025), (.042, .03), (.042, .045), (.03, .056), (0, .058)], 20)
        lathe('Black', q(f, .97, .07), (1, 0, 0), [(0, 0), (.026, 0), (.026, .012), (0, .012)], 16)
        lathe('Green', q(f + .012, .97, .07), (1, 0, 0), [(0, 0), (.018, 0), (.018, .012), (0, .012)], 16)
        lathe('Black', q(f, .84, 0), (1, 0, 0), [(0, 0), (.028, 0), (.028, .018), (0, .018)], 16)
        box('Black', q(f + .025, .84, 0), (.012, .012, .05), .003)
        box('Cream', q(f + .002, .74, 0), (.004, .045, .2), 0)
        lathe('Black', q(.04, 1.05, 0), (0, 1, 0), [(0, 0), (.045, 0), (.045, .025), (0, .025)], 20)
        lathe('Amber', q(.04, 1.075, 0), (0, 1, 0), [(0, 0), (.035, 0), (.035, .06), (.02, .08), (0, .085)], 20)
        cyl(S, q(-.03, .62, 0), q(-.07, .62, 0), .008, n=6)
        sweep('Rubber', [q(-.07 - .012 * i / 24, .5 + .12 * math.cos(2 * math.pi * i / 24), .12 * math.sin(2 * math.pi * i / 24))
                         for i in range(24 * 3 + 1)], .01, 8)
    cable = smooth([(-.03, .76, mz), (-.14, .7, mz - .02), (-.3, .36, -1.35), (-.57, .355, -.95), (-.57, .355, -.1),
                    (-.56, .355, .3)] + [real(post, o_) for o_ in ((-.04, .05, -.06), (-.04, .6, -.04), (0, .69, -.02))], 5)
    sweep('Rubber', cable, .008, 8)
    # Backstay posts on the rear cross member, bracing the crown.
    for sx in (-1, 1):
        cyl(Y, (sx * .6, .28, -1.74), (sx * .6, 1.35, -1.74), .035, n=10)
        cyl(Y, (sx * .6, .9, -1.74), (sx * .69, .32, -1.2), .02, n=8)
        obox(D, (sx * .6, 1.37, -1.74), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.03, .08, .08), .004)
    cyl(Y, (-.6, 1.3, -1.74), (.6, 1.3, -1.74), .025, n=8)
    # Tripod headframe leaning out over the hole: two raked front legs and two back legs meet at
    # the crown; wire backstays run from the crown to the ground stakes behind the skid.
    top_y, zc = 5.2, 2.95  # crown underside and its reach past the origin
    legs = {(sx, fz): (Vector((sx * .72, .42, fz)), Vector((sx * .12, top_y, zc + (.12 if fz > .5 else -.12))))
            for sx in (-1, 1) for fz in (.95, -.15)}
    def at(key, y):
        a, b = legs[key]; return a + (b - a) * ((y - a.y) / (b.y - a.y))
    for (sx, fz), (foot, head) in legs.items():
        cyl(Y, foot, head, .055, n=14)
        cyl(D, foot, foot + (head - foot).normalized() * .14, .066, n=14)
        box(D, (foot.x, .335, foot.z), (.2, .02, .2), .004)
        for ex in (-.07, .07): box(D, (foot.x + ex, .39, foot.z), (.02, .11, .1), .004)
        cyl(S, (foot.x - .09, .42, foot.z), (foot.x + .09, .42, foot.z), .014, n=10)
        for bx in (-.07, .07):
            for bz in (-.07, .07): bolt((foot.x + bx, .345, foot.z + bz), (0, 1, 0), .012)
    foot, head = legs[(-1, .95)]; d = (head - foot).normalized()
    out = (Vector((-1, 0, 0)) - d * d.x).normalized(); across = d.cross(out).normalized()
    y0, y1 = .55, top_y - .6
    for side in (-1, 1):
        a_, b_ = at((-1, .95), y0) + out * .07 + across * side * .07, at((-1, .95), y1) + out * .07 + across * side * .07
        cyl(Y, a_, b_, .008, n=8)
        for k in range(5):
            y = y0 + .05 + (y1 - y0 - .1) * k / 4
            cyl(Y, at((-1, .95), y) + across * side * .07, at((-1, .95), y) + out * .07 + across * side * .07, .006, n=6)
    rungs = int((at((-1, .95), y1) - at((-1, .95), y0)).length * K / .3)
    for k in range(1, rungs):
        c = at((-1, .95), y0 + (y1 - y0) * k / rungs) + out * .07
        cyl(S, c - across * .07, c + across * .07, .006, n=6)
    for sx in (-1, 1):
        for y in (1.0, 2.5, 3.9): cyl(Y, at((sx, .95), y), at((sx, -.15), y), .03, n=10)
        for ya, yb in ((1.02, 2.48), (2.52, 3.88)): cyl(Y, at((sx, .95), ya), at((sx, -.15), yb), .025, n=10)
    for y in (2.0, 3.4): cyl(Y, at((-1, -.15), y), at((1, -.15), y), .03, n=10)
    for y in (1.6, 2.9, 4.1): cyl(Y, at((-1, .95), y), at((1, .95), y), .03, n=10)
    # Lead wheel on the skid's front cross member turns the rope up into the frame.
    ly, lz = lead_c.y, lead_c.x
    plate = [(lz - .1, .28), (lz + .1, .28)] + [(lz + .1 * math.cos(math.radians(a)), ly + .1 * math.sin(math.radians(a))) for a in range(0, 181, 20)]
    for sx in (-1, 1):
        prism(Y, plate, (sx * .055 + (.025 if sx > 0 else 0), 0, 0), (0, 0, 1), (0, 1, 0), .025)
        bolt((sx * .08, ly, lz), (sx, 0, 0), .02)
    cyl(S, (-.1, ly, lz), (.1, ly, lz), .025, n=12)
    head_c, head_r = Vector((zc, top_y - .36)), .3
    box(D, (0, top_y + .04, zc), (.36, .12, .36), .015)
    cheek = [(zc - .16, top_y - .04)] + [(zc + .16 * math.cos(math.radians(a)), head_c.y + .16 * math.sin(math.radians(a))) for a in range(180, 361, 20)] + [(zc + .16, top_y - .04)]
    for sx in (-1, 1):
        prism(Y, cheek, (sx * .06 + (.025 if sx > 0 else 0), 0, 0), (0, 0, 1), (0, 1, 0), .025)
        cyl(D, (sx * .085, head_c.y, zc), (sx * .11, head_c.y, zc), .035, n=6)
        obox(D, (sx * .1, top_y + .02, zc - .2), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.02, .08, .1), .004)
    cyl(S, (-.12, head_c.y, zc), (.12, head_c.y, zc), .03, n=12)
    lathe('Black', (0, top_y + .1, zc + .1), (0, 1, 0), [(0, 0), (.05, 0), (.05, .03), (0, .03)], 20)
    lathe('Amber', (0, top_y + .13, zc + .1), (0, 1, 0), [(0, 0), (.04, 0), (.04, .07), (.022, .095), (0, .1)], 20)
    ring(S, (0, top_y + .16, zc - .1), (1, 0, 0), .05, .012)
    # Rear lifting eyes, backstays to the rear posts and sandbag counterweight, all slewing.
    for sx in (-1, 1):
        ring(S, (sx * .745, .22, -1.88), (1, 0, 0), .045, .012)
        lug, anchor = Vector((sx * .1, top_y + .02, zc - .25)), Vector((sx * .6, 1.4, -1.74))
        dv = (anchor - lug).normalized()
        buckle = anchor - dv * .5
        rope([lug + dv * .06, buckle - dv * .08])
        socket(lug + dv * .06, .06, .03, -dv); socket(buckle - dv * .08, .06, .03, dv)
        cyl(S, buckle - dv * .08, buckle + dv * .08, .018, n=8)
        cyl(D, buckle + dv * .08, anchor, .01, n=8)
        for k, z in enumerate((-1.7, -1.45, -1.2)):
            with human((sx * .8, .32, z)):
                box('Canvas', (sx * .8, .32 + .085, z), (.3, .17, .5), .07, sx * (.05 - .04 * k), 3)
        for k, z in enumerate((-1.575, -1.325)):
            with human((sx * .8, .32, z)):
                box('Canvas', (sx * .8, .32 + .245, z), (.28, .15, .46), .065, -sx * (.06 - .05 * k), 3)
    # Real-size access ladder hanging from the left runner, clear of the base arms as it slews.
    foot = .27  # base units above the ground
    ladder(Vector((-.83, foot - LIFT, -1.0)), (.335 + LIFT - foot) * K + .9, (0, 0, 1), rung_top=(.335 + LIFT - foot) * K, lean=(.1, 0, 0))
    # Rope: drum top -> level wind -> under the lead wheel -> up and over the head sheave -> down.
    drum_c = Vector((cz, cy))
    d_out, l_in = tangent(drum_c, drum_r, -1, lead_c, lead_r, 1)
    l_out, h_in = tangent(lead_c, lead_r, 1, head_c, head_r, -1)
    h_out = Vector((head_c.x + head_r, head_c.y))
    hook_top = 2.2  # rope end
    path = [zy(.1, d_out), Vector((.1, d_out.y, -.5)), zy(0, l_in)] + wrap(lead_c, lead_r, 1, l_in, l_out, 0) \
        + wrap(head_c, head_r, -1, h_in, h_out, 0) + [Vector((0, hook_top, h_out.x))]
    m.part('Rope', (0, .12, ax), 'Slew'); rope(path)
    m.part('LeadSheave', (0, lead_c.y, lead_c.x), 'Slew'); sheave((0, lead_c.y, lead_c.x), (1, 0, 0), lead_r, .08, .045, 5)
    m.part('HeadSheave', (0, head_c.y, head_c.x), 'Slew'); sheave((0, head_c.y, head_c.x), (1, 0, 0), head_r, .09, .06, 6)
    # Lifting magnet at the rope end: it clamps onto the find instead of hooking it.
    m.part('Attachment', (0, hook_top, h_out.x), 'Slew')
    magnet((0, hook_top, h_out.x))
    LIFT = 0.0
    return m


# ---------- B: trailer jib crane ----------
def model_b():
    m = Model('B'); m.part('Chassis')
    for wz in (-.78, .08):  # tandem axles: wheels, springs, mudguards
        for sx in (-1, 1):
            c = Vector((sx * 1.0, .36, wz)); a = Vector((sx, 0, 0))
            lathe('Rubber', c, (1, 0, 0), [(.25, -.1), (.33, -.11), (.355, -.09), (.365, -.05), (.365, .05), (.355, .09), (.33, .11), (.25, .1)], 36, closed=True)
            for i in range(24):
                ang = 2 * math.pi * i / 24; d = Vector((0, math.sin(ang), math.cos(ang)))
                obox('Rubber', c + d * .366 + a * (.045 if i % 2 else -.045), (d, (1, 0, 0), d.cross(Vector((1, 0, 0)))), (.018, .075, .05))
            lathe('Grey', c, (1, 0, 0), [(.235, -.1), (.255, -.1), (.255, .1), (.235, .1)], 32, closed=True)
            lathe('Grey', c + a * .03, (1, 0, 0), [(0, -.012), (.24, -.012), (.24, .012), (0, .012)], 32)
            lathe(S, c + a * .042, a, [(0, 0), (.07, 0), (.06, .045), (0, .055)], 20)
            bolt_ring(c + a * .042, a, .1, 5, .014)
            lathe(B, c, (1, 0, 0), [(.42, -.14), (.44, -.14), (.44, .14), (.42, .14)], 20, math.radians(15), math.radians(165), closed=True)
            box(D, (sx * .8, .75, wz), (.36, .03, .05), .005)
            for k, (length, y) in enumerate(((.8, .42), (.6, .44), (.4, .46))): box(D, (sx * .62, y, wz), (.06, .018, length), .004)
            for dz in (-.06, .06): box(S, (sx * .62, .41, wz + dz), (.08, .1, .015), .003)
            for dz in (-.4, .4): box(D, (sx * .62, .5, wz + dz), (.03, .1, .03), .004)
        cyl(D, (-.95, .36, wz), (.95, .36, wz), .04)
    for sx in (-1, 1):
        box(B, (sx * .62, .63, -.2), (.08, .16, 3.5), .01)
        beam(B, (sx * .6, .63, -1.95), (sx * .07, .63, -2.9), .08, .12)
        box(B, (sx * .62, .63, 1.705), (.08, .16, .31), .01)
        box('Red', (sx * .55, .63, 1.87), (.12, .06, .02), .006)
        box('Amber', (sx * .665, .63, .5), (.004, .05, .08), .002)
    for z in (-1.9, -1.05, .35, 1.5, 1.82): box(B, (0, .63, z), (1.16, .12, .08), .008)
    box(D, (0, .72, -.02), (1.36, .02, 3.86), .004)
    checker(D, -.64, .64, -1.9, 1.84, .73, .13)
    box(D, (0, .63, -2.95), (.12, .1, .25), .01)
    lathe(S, (0, .6, -3.12), (0, 1, 0), [(0, 0), (.07, 0), (.075, .04), (.05, .08), (0, .085)], 20)
    box('Black', (0, .7, -3.02), (.05, .03, .14), .008)
    jx, jz = .35, -2.55  # jockey wheel
    box(D, (jx - .05, .63, jz), (.1, .12, .1), .01)
    cyl(D, (jx, .14, jz), (jx, 1.0, jz), .035, n=14)
    sweep(S, [(jx, 1.0, jz), (jx, 1.05, jz), (jx + .14, 1.05, jz), (jx + .14, 1.1, jz)], .01, 8)
    cyl('Black', (jx + .14, 1.1, jz), (jx + .14, 1.18, jz), .014, n=10)
    for dx in (-.045, .045): box(D, (jx + dx, .12, jz), (.012, .12, .04), .003)
    lathe('Rubber', (jx, .09, jz), (1, 0, 0), [(.04, -.03), (.09, -.03), (.09, .03), (.04, .03)], 20, closed=True)
    lathe('Grey', (jx, .09, jz), (1, 0, 0), [(0, -.035), (.045, -.035), (.045, .035), (0, .035)], 12)
    box(D, (0, .695, -2.35), (.8, .015, .36), .003)
    tb = Vector((0, .7025, -2.35))  # real-size toolbox on the drawbar
    with human(tb):
        box(B, tb + Vector((0, .13, 0)), (.55, .26, .3), .015)
        box(D, tb + Vector((0, .2, 0)), (.56, .012, .31), 0)
        for dx in (-.18, .18): box(S, tb + Vector((dx, .16, .155)), (.04, .06, .012), .003)
    for z in (1.3, -1.75):  # outriggers on screw jacks and timber pads
        box(D, (0, .66, z), (2.5, .09, .1), .008)
        for sx in (-1, 1):
            box(Y, (sx * 1.3, .66, z), (.2, .1, .11), .008)
            box(Y, (sx * 1.42, .62, z), (.1, .5, .1), .008)
            cyl(S, (sx * 1.42, .87, z), (sx * 1.42, .95, z), .015, n=8)
            sweep(S, [(sx * 1.42, .95, z), (sx * 1.42, .97, z), (sx * 1.42, .97, z + .13), (sx * 1.42, 1.02, z + .13)], .009, 6)
            cyl(S, (sx * 1.42, .08, z), (sx * 1.42, .4, z), .032, n=12)
            lathe(D, (sx * 1.42, .06, z), (0, 1, 0), [(0, 0), (.13, 0), (.13, .025), (.05, .04), (0, .04)], 20)
            box('Wood', (sx * 1.42, .03, z), (.42, .06, .42), .01, .05 * sx * (1 if z > 0 else -1))

    # Fixed ring gear; the turret's pinion walks round it.
    lathe(D, (0, .73, 0), (0, 1, 0), [(.44, 0), (.56, 0), (.56, .08), (.44, .08)], 44, closed=True)
    for i in range(44):
        ang = 2 * math.pi * i / 44; d = Vector((math.cos(ang), 0, math.sin(ang)))
        obox(D, d * .57 + Vector((0, .765, 0)), (d, (0, 1, 0), d.cross(Vector((0, 1, 0)))), (.03, .06, .035))
    # Operator station on a railed front platform (fixed, clear of the turret's swing), at real size.
    ext = Vector((0, .73, 1.68))
    def o(x, y, z): return ext + Vector((x, y, z))
    with human(ext):
        wx, fz = .68 * K - .04, .18 * K - .04
        for x in (-wx, -1.0, 0, 1.0, wx): cyl(Y, o(x, 0, fz), o(x, 1.0, fz), .022, n=10)
        for x, z in ((wx, -fz), (-wx, .1)): cyl(Y, o(x, 0, z), o(x, 1.0, z), .022, n=10)
        for y in (.5, 1.0):
            cyl(Y, o(-wx, y, fz), o(wx, y, fz), .02, n=10)
            cyl(Y, o(wx, y, fz), o(wx, y, -fz), .02, n=10)
            cyl(Y, o(-wx, y, fz), o(-wx, y, .1), .02, n=10)
        box(D, o(-1.1, .5, .2), (.12, 1.0, .12), .008)
        t = math.radians(25); n_ = Vector((0, math.sin(t), -math.cos(t))); tv = Vector((0, math.cos(t), math.sin(t)))
        pc = o(-1.1, 1.05, .17)
        obox('Grey', pc, (n_, tv, (1, 0, 0)), (.06, .2, .34), .01)
        for k, x in enumerate((-.1, 0, .1)):
            base = pc + n_ * .03 + tv * .06 + Vector((x, 0, 0))
            tip_ = base + Vector((0, .26, -.04 - .03 * (k - 1)))
            cyl(S, base, tip_, .011, n=8)
            sphere('Red' if k == 1 else 'Black', tip_, .03)
        gauge = pc + n_ * .031 - tv * .05
        lathe('Black', gauge, n_, [(0, 0), (.05, 0), (.05, .012), (0, .012)], 20)
        lathe('Cream', gauge + n_ * .012, n_, [(0, 0), (.042, 0), (.042, .002), (0, .002)], 20)
        obox('Red', gauge + n_ * .016 + tv * .012 + Vector((.01, 0, 0)), (n_, tv, (1, 0, 0)), (.002, .035, .006))
    ladder(Vector((-.74, 0, 1.62)), .73 * K + .95, (0, 0, 1), rung_top=.73 * K)

    # Slewing turret: everything from here on swings about the ring's axis.
    m.part('Turret', (0, .81, 0))
    box(B, (0, .85, -.1), (1.3, .08, 2.0), .012)
    gear(D, (.53, .765, .4), (0, 1, 0), .06, 12, .05)  # slew pinion, shaft and drive
    cyl(S, (.53, .79, .4), (.53, .9, .4), .02, n=10)
    box(D, (.53, .92, .4), (.14, .06, .14), .01)
    cyl(D, (.53, .95, .4), (.53, 1.1, .4), .055, n=16)
    box(D, (0, 1.165, -.95), (1.25, .55, .28), .03)
    hazard((.575, .94, -1.091), (-1, 0, 0), (0, 1, 0), 1.15, .45, .1)
    for sx in (-1, 1): ring(S, (sx * .4, 1.48, -.95), (0, 0, 1), .045, .012)
    box(B, (0, 1.215, -.45), (1.0, .65, .7), .035)
    for sx in (-1, 1):
        for i in range(6):
            t = math.radians(30)
            obox(D, (sx * .505, 1.0 + i * .08, -.45), ((math.cos(t), -sx * math.sin(t), 0), (sx * math.sin(t), math.cos(t), 0), (0, 0, 1)), (.012, .05, .46))
    for dx, dz, w, h in ((0, -.1, .8, .5),):
        for x in (-w / 2, w / 2): box(D, (x, 1.2, dz + .004), (.012, h, .012), 0)
        for y in (1.2 - h / 2, 1.2 + h / 2): box(D, (0, y, dz + .004), (w, .012, .012), 0)
    box(S, (.3, 1.2, -.09), (.08, .02, .02), .004)
    cyl(D, (.28, 1.54, -.62), (.28, 1.74, -.62), .075, n=16)
    cyl('Rust', (.28, 1.74, -.62), (.28, 2.12, -.62), .04, n=14)
    t = math.radians(25)
    obox(D, (.28, 2.14, -.6), ((1, 0, 0), (0, math.cos(t), math.sin(t)), (0, -math.sin(t), math.cos(t))), (.1, .006, .1), .002)
    lathe(B, (-.25, 1.54, -.55), (0, 1, 0), [(0, 0), (.09, 0), (.09, .2), (0, .2)], 20)
    lathe('Black', (-.25, 1.74, -.55), (0, 1, 0), [(0, 0), (.1, 0), (.1, .03), (.03, .05), (0, .05)], 20)
    cyl(S, (.05, 1.54, -.25), (.05, 1.57, -.25), .035, n=12)
    cyl(D, (-.62, 1.03, -.74), (-.62, 1.03, -.18), .12, n=20)
    cyl(S, (-.62, 1.15, -.3), (-.62, 1.18, -.3), .03, n=10)
    for z in (-.64, -.28):
        lathe('Black', (-.62, 1.03, z), (0, 0, 1), [(.12, -.015), (.128, -.015), (.128, .015), (.12, .015)], 20, closed=True)
    # Drum winch on the turret with hydraulic motor and ratchet.
    dz, dy = .25, 1.22
    arc = [(dz + .17 * math.cos(math.radians(a)), dy + .17 * math.sin(math.radians(a))) for a in range(0, 181, 15)]
    for x0 in (.4, -.36): prism(B, [(0, .89), (.5, .89)] + arc, (x0, 0, 0), (0, 0, 1), (0, 1, 0), .04)
    cyl(D, (.4, dy, dz), (.58, dy, dz), .085, n=20)
    lathe(D, (.58, dy, dz), (1, 0, 0), [(0, 0), (.085, 0), (.06, .04), (0, .045)], 20)
    for k, (y, z) in enumerate(((1.28, .21), (1.28, .29))):
        cyl(S, (.5, y, z), (.5, y + .05, z), .018, n=8)
        sweep('Rubber', smooth([(.5, y + .05, z), (.52, 1.46, z - .05), (.46, 1.48, -.02 - k * .04), (.42 - k * .06, 1.36, -.1)], 5), .014, 8)
    tip_ang = math.radians(50); dirv = Vector((0, math.sin(tip_ang), math.cos(tip_ang))); up = Vector((0, math.cos(tip_ang), -math.sin(tip_ang)))
    F = Vector((0, 1.1, .75)); L = 4.6; T = F + dirv * L  # long jib: the hook hangs well out over the hole
    tip_c, tip_r = Vector((T.z, T.y)), .17
    drum_c, drum_r = Vector((dz, dy)), .13 + 4 * ROPE_R + ROPE_R
    d_out, t_in = tangent(drum_c, drum_r, -1, tip_c, tip_r, -1)
    leave = math.atan2(d_out.y - dy, d_out.x - dz)
    m.part('Drum', (0, dy, dz), 'Turret')
    cyl(S, (-.52, dy, dz), (.52, dy, dz), .035)
    lathe(D, (0, dy, dz), (1, 0, 0), [(0, -.33), (.13, -.33), (.13, .33), (0, .33)], 28)
    drum_rope((0, dy, dz), .13, -.33, .33, -.05, leave)
    for sx in (-1, 1):
        lathe(Y, (sx * .345, dy, dz), (1, 0, 0), [(.12, -.015), (.27, -.015), (.27, .015), (.12, .015)], 32, closed=True)
        bolt_ring((sx * .36, dy, dz), (sx, 0, 0), .08, 6)
    gear(S, (-.44, dy, dz), (1, 0, 0), .15, 20, .02, .03)
    m.part('Turret')
    beam(D, (-.44, 1.3, .1), (-.44, 1.36, .17), .02, .03, up=(0, 0, -1))
    cyl(S, (-.47, 1.3, .1), (-.4, 1.3, .1), .012, n=8)
    # Boom foot brackets and the lattice jib.
    arc = [(.75 + .08 * math.cos(math.radians(a)), 1.1 + .08 * math.sin(math.radians(a))) for a in range(0, 181, 20)]
    for x0 in (.33, -.29): prism(B, [(.55, .89), (.95, .89)] + arc, (x0, 0, 0), (0, 0, 1), (0, 1, 0), .04)
    cyl(S, (-.36, 1.1, .75), (.36, 1.1, .75), .03, n=12)
    m.part('Jib', F, 'Turret')  # luffs about the foot pin
    def chord(s, sx, sy):
        f = s / L; w = .4 + (.22 - .4) * f; h = .3 + (.2 - .3) * f
        return F + dirv * s + Vector((sx * w / 2, 0, 0)) + up * (sy * h / 2)
    for sx in (-1, 1):
        for sy in (-1, 1): cyl(Y, chord(.1, sx, sy), chord(L - .1, sx, sy), .028, n=8)
        obox(Y, F + dirv * .22 + Vector((sx * .215, 0, 0)), ((1, 0, 0), up, dirv), (.02, .34, .5), .006)
        obox(Y, T - dirv * .1 + Vector((sx * .085, 0, 0)), ((1, 0, 0), up, dirv), (.02, .2, .36), .006)
        cyl(D, (sx * .1, T.y, T.z), (sx * .13, T.y, T.z), .03, n=6)
        lug = T - dirv * .15 + up * .14
        obox(D, lug + Vector((sx * .13, 0, 0)), ((1, 0, 0), up, dirv), (.02, .1, .12), .004)
    cyl(S, (-.13, T.y, T.z), (.13, T.y, T.z), .025, n=12)
    bays = 10; s0, s1 = .45, L - .3
    nodes = [s0 + (s1 - s0) * k / bays for k in range(bays + 1)]
    faces = [((-1, 1), (1, 1)), ((-1, -1), (1, -1)), ((-1, -1), (-1, 1)), ((1, -1), (1, 1))]
    for (a_, b_) in faces:
        for k in range(bays):
            p, q = (a_, b_) if k % 2 == 0 else (b_, a_)
            cyl(Y, chord(nodes[k], *p), chord(nodes[k + 1], *q), .013, n=6)
    for k in range(0, bays + 1, 2):
        for (a_, b_) in faces[:2]: cyl(Y, chord(nodes[k], *a_), chord(nodes[k], *b_), .013, n=6)
    # A-mast holding the jib on two pendant ropes.
    m.part('Turret')
    apex = Vector((0, 3.3, -.5))
    for sx in (-1, 1):
        cyl(B, (sx * .5, 1.44, -.95), apex + Vector((sx * .17, 0, 0)), .04, n=12)
        cyl(B, (sx * .42, 1.54, -.16), apex + Vector((sx * .17, 0, 0)), .035, n=12)
        obox(D, apex + Vector((sx * .14, 0, .04)), ((1, 0, 0), (0, 1, 0), (0, 0, 1)), (.02, .1, .14), .004)
    cyl(S, apex - Vector((.22, 0, 0)), apex + Vector((.22, 0, 0)), .04, n=12)
    box(D, apex - Vector((0, .07, 0)), (.26, .06, .12), .01)
    m.part('Pendants', apex, 'Turret')
    for sx in (-1, 1):
        a_, b_ = apex + Vector((sx * .14, 0, .06)), T - dirv * .15 + up * .14 + Vector((sx * .13, 0, 0))
        dvec = (b_ - a_).normalized()
        rope([a_ + dvec * .1, b_ - dvec * .1])
        socket(a_ + dvec * .1, .1, .03, -dvec); socket(b_ - dvec * .1, .1, .03, dvec)
    # Rope part: hoist line from the drum over the tip sheave, hanging to the suction cup.
    hook_top = 2.5  # rope end
    t_out = Vector((tip_c.x + tip_r, tip_c.y))
    m.part('Rope', (0, .81, 0), 'Turret')
    rope([zy(-.05, d_out)] + wrap(tip_c, tip_r, -1, t_in, t_out, 0) + [Vector((0, hook_top, t_out.x))])
    m.part('TipSheave', (0, T.y, T.z), 'Jib'); sheave((0, T.y, T.z), (1, 0, 0), tip_r, .07, .04, 5)
    # Suction cup at the rope end: it seals onto the find instead of hooking it.
    m.part('Attachment', (0, hook_top, t_out.x), 'Turret')
    suction_cup((0, hook_top, t_out.x))
    return m


def finish(model, collection):
    objects = {}
    for part in model.parts.values():
        bm = part.bm
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons: bmesh.ops.triangulate(bm, faces=ngons)
        bmesh.ops.translate(bm, vec=-part.pivot, verts=bm.verts)
        name = model.name + '_' + part.name
        old = bpy.data.objects.get(name)
        if old: bpy.data.objects.remove(old, do_unlink=True)
        mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
        for key in part.keys: mesh.materials.append(MATS[key])
        mesh.set_sharp_from_angle(angle=math.radians(38))
        obj = bpy.data.objects.new(name, mesh); obj.location = part.pivot
        collection.objects.link(obj); objects[part.name] = obj
    for part in model.parts.values():  # moving parts ride on their slewing or luffing parent
        if part.parent:
            child = objects[part.name]
            child.parent = objects[part.parent]; child.matrix_parent_inverse.identity()
            child.location = part.pivot - model.parts[part.parent].pivot
    return list(objects.values())


stats = {}
for build in (model_a, model_b):
    model = build()
    collection = bpy.data.collections.new('RimWinch' + model.name); scene.collection.children.link(collection)
    objects = finish(model, collection)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT / ('RimWinch' + model.name + '.fbx')), use_selection=True, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='Y', axis_up='Z', bake_space_transform=False,
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
    stats[model.name] = {o.name: len(o.data.polygons) for o in objects}

bpy.data.libraries.write(str(ROOT / 'art/winch-candidates/winch-candidates.blend'), {scene}, fake_user=True)
bpy.context.window.scene = previous
result = stats
