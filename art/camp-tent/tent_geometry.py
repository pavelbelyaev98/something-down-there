"""Geometry of the camp tent: an olive-drab canvas frame tent with its walls rolled up, on a floor of Euro pallets
(references: excavation frame tents over a dig, olive army canvas tents, canvas wall tents with guy ropes).

Blender axes, metres: X across the 9 m front, Y front (-) to back (+), Z up; the ground is z = 0. The ridge runs
along X; front, sides and back are open except the back wall, which hangs to the ground. Parts share the Group
builder of the workshop (art/camp-workshop/workshop_geometry.py); canvas is built as metric UV islands like the
workshop's sheets, so its weave detail tiles evenly.
"""
import math
import random
import sys
from pathlib import Path

from mathutils import Vector

WORKSHOP = Path(__file__).resolve().parents[1] / 'camp-workshop'
if str(WORKSHOP) not in sys.path:
    sys.path.insert(0, str(WORKSHOP))
import importlib  # noqa: E402
import workshop_geometry  # noqa: E402
importlib.reload(workshop_geometry)  # Blender keeps modules between runs
Group = workshop_geometry.Group

LEGS = (-4.5, -1.5, 1.5, 4.5)       # leg and rafter lines across the front
HD = 2.5                            # front and back legs at y = -HD, +HD
EAVE, RIDGE = 2.1, 3.2
TUBE = .024                         # 48 mm frame tube
CANVAS = TUBE + .012                # canvas face above the tube centre lines
OVER = .1                           # roof overhang past the gable rafters
SAG = .055
VALANCE = .3
VAL_OUT = .075                      # valance stands this far outside the legs
ROLL_R, ROLL_Z = .075, EAVE - .36
ROOF_ROWS = 24                      # rows from ridge to eave rail (two more bend over it)
PALLET = (1.2, .8, .144)
TABLE = (-3.95, -1.45, 1.9)         # x from, x to, y centre
LAMPS = (-2.25, 2.25)               # pendant lamps under the ridge
rng = random.Random(11)


def ridge_z(y):
    return RIDGE - (RIDGE - EAVE) * min(1.0, abs(y) / HD)


def bay_sag(x, t):
    """Canvas sag between rafters (zero on the tubes), a little droop on the overhang."""
    ax = abs(x)
    if ax > LEGS[-1]:
        return .012 * (ax - LEGS[-1]) / OVER * (.4 + .6 * t)
    for x0, x1 in zip(LEGS, LEGS[1:]):
        if x0 <= x <= x1:
            u = (x - x0) / (x1 - x0)
            return SAG * math.sin(math.pi * u) * math.sin(math.pi * t) ** .8
    return 0.0


def wrinkle(x, y, z, seed):
    """Small tension ripples and creases: sums of sines, strongest near corners."""
    r = .0025 * math.sin(x * 7.3 + seed) * math.sin(y * 5.1 + seed * 1.7) + .0015 * math.sin(x * 19.0 + y * 13.0 + seed * 3.1)
    for cx in LEGS:
        for cy in (-HD, HD):
            d = math.hypot(x - cx, y - cy)
            r += .006 * math.exp(-d / .45) * math.sin((x - cx + (y - cy)) * 38.0 + seed)
    return r


def oriented(group, fn, nu, nv, width, length, outward, value=None, local=None):
    """Adds a surface island whose faces point toward 'outward' (judged at its centre)."""
    c, ds, dt = Vector(fn(.5, .5)), Vector(fn(.51, .5)), Vector(fn(.5, .51))
    normal = (ds - c).cross(dt - c)
    flip = normal.dot(Vector(outward)) < 0
    return group.surface(fn, nu, nv, width, length, value=value, flip=flip, local=local)


def canvas_pair(out, inside, fn, nu, nv, width, length, outward, local=None):
    """Outer face toward 'outward'; the inner face 1.5 mm behind it, facing the other way."""
    value = rng.random()
    oriented(out, fn, nu, nv, width, length, outward, value, local)
    n = Vector(outward).normalized()
    oriented(inside, lambda s, t: Vector(fn(s, t)) - n * .0015, nu, nv, width, length, -n, value, local)


def tube_surface(group, a, b, radius, outward=True, value=None, segments=16, rows=None):
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    ref = Vector((0, 0, 1)) if abs(axis.z) < .9 else Vector((1, 0, 0))
    u = axis.cross(ref).normalized()
    v = axis.cross(u).normalized()
    length = (b - a).length
    rows = rows or max(2, int(length / .25))

    def fn(s, t):
        ang = 2 * math.pi * s
        return a + (b - a) * t + (u * math.cos(ang) + v * math.sin(ang)) * radius
    c, ds, dt = Vector(fn(.0, .5)), Vector(fn(.01, .5)), Vector(fn(.0, .51))
    flip = ((ds - c).cross(dt - c)).dot(c - (a + b) / 2) < 0
    faces = group.surface(fn, segments, rows, 2 * math.pi * radius, length, value=value, flip=flip)
    for f in faces:
        f[group.axis_layer] = Vector((abs(axis.x), abs(axis.y), abs(axis.z)))
    return faces


def roll(group, a, b, spans, value):
    """A rolled-up wall: sags between its ties, lumpy along its length."""
    axis = (b - a).normalized()
    u = axis.cross(Vector((0, 0, 1))).normalized()
    v = axis.cross(u).normalized()
    length = (b - a).length

    def fn(s, t):
        ang = 2 * math.pi * s
        sag = .03 * abs(math.sin(math.pi * t * spans))  # zero at each tie
        r = ROLL_R * (1 + .07 * math.sin(ang * 2 + t * 23) + .05 * math.sin(t * 41 + ang))
        return a + (b - a) * t + Vector((0, 0, -sag)) + (u * math.cos(ang) + v * math.sin(ang)) * r
    c, ds, dt = Vector(fn(0, .5)), Vector(fn(.01, .5)), Vector(fn(0, .51))
    centre = a + (b - a) * .5 + Vector((0, 0, -.03 * abs(math.sin(math.pi * .5 * spans))))
    flip = ((ds - c).cross(dt - c)).dot(c - centre) < 0
    faces = group.surface(fn, 18, max(8, int(length / .12)), 2 * math.pi * ROLL_R, length, value=value, flip=flip)
    for f in faces:
        f[group.axis_layer] = Vector((abs(axis.x), abs(axis.y), abs(axis.z)))
    return faces


def build(collection):
    groups = {name: Group('Tent_' + name) for name in ('CanvasOut', 'CanvasIn', 'Metal', 'Wood', 'Bulb')}
    out, inside, metal, wood, bulb = (groups[k] for k in ('CanvasOut', 'CanvasIn', 'Metal', 'Wood', 'Bulb'))
    x0, x1 = LEGS[0] - OVER, LEGS[-1] + OVER
    slope = math.hypot(HD, RIDGE - EAVE)

    # ------------------------------------------------------------ canvas
    for sy in (-1, 1):
        seed = 1.3 if sy < 0 else 4.7

        bend = ROOF_ROWS / (ROOF_ROWS + 2)

        def roof(s, t, sy=sy, seed=seed, bend=bend):
            x = x0 + (x1 - x0) * s
            r = HD * t / bend if t <= bend else HD + VAL_OUT * (t - bend) / (1 - bend)
            z = RIDGE + (EAVE - RIDGE) * min(r, HD) / HD + CANVAS - max(0.0, r - HD) * .9  # over the eave rail
            n = Vector((0, sy * (RIDGE - EAVE), HD)).normalized()
            k = min(1.0, r / HD)
            return Vector((x, sy * r, z)) - n * bay_sag(x, k) + n * wrinkle(x, sy * r, z, seed) * k
        canvas_pair(out, inside, roof, 96, ROOF_ROWS + 2, x1 - x0, slope + VAL_OUT, (0, sy * .4, 1),
                    local=lambda s, t: (s, t))
        # Valance: a 30 cm skirt hanging from the eave, its hem a little wavy.
        yv = sy * (HD + VAL_OUT)
        zv = EAVE + CANVAS - VAL_OUT * .9

        def valance(s, t, sy=sy, yv=yv, zv=zv):
            x = x0 + (x1 - x0) * s
            return Vector((x, yv + sy * .006 * math.sin(x * 4.1) * t + sy * .004 * t, zv - VALANCE * t))
        canvas_pair(out, inside, valance, 92, 3, x1 - x0, VALANCE, (0, sy, 0))
    for sx in (-1, 1):
        xg = sx * (LEGS[-1] + .05)

        def gable(s, t, sx=sx, xg=xg):
            y = -HD + 2 * HD * s
            top = ridge_z(y) + CANVAS - .005
            z = EAVE + (top - EAVE) * t
            bulge = .02 * math.sin(math.pi * s) * math.sin(math.pi * min(1, t * 1.2))
            return Vector((xg + sx * bulge, y, z))
        canvas_pair(out, inside, gable, 30, 8, 2 * HD, RIDGE - EAVE, (sx, 0, 0))

        def end_valance(s, t, sx=sx):
            y = -(HD + CANVAS) + 2 * (HD + CANVAS) * s
            return Vector((sx * (x1 + .002) + sx * .004 * t, y, EAVE + CANVAS - VALANCE * t))
        canvas_pair(out, inside, end_valance, 40, 3, 2 * (HD + CANVAS), VALANCE, (sx, 0, 0))
    # Back wall: hangs from the eave to the ground in loose pleats, pegged at the legs; a sod cloth lies inward.
    yb = HD + TUBE + .006
    top_z = EAVE - .03

    def pleat(x, z):
        near = min(abs(x - lx) for lx in LEGS)
        amp = (.006 + .018 * (1 - z / EAVE)) * min(1.0, near / .45)
        return amp * (.5 + .5 * math.sin(2 * math.pi * x / .52 + .8 * math.sin(x * 1.3))) + .01 * (1 - z / EAVE)

    def back(s, t):
        x = LEGS[0] + (LEGS[-1] - LEGS[0]) * s
        z = top_z * (1 - t) + .015 * t
        return Vector((x, yb + pleat(x, z), z))
    canvas_pair(out, inside, back, 90, 20, LEGS[-1] - LEGS[0], top_z, (0, 1, 0))

    def sod(s, t):
        x = LEGS[0] + (LEGS[-1] - LEGS[0]) * s
        return Vector((x, yb + pleat(x, .015) + .15 * t, .015 - .008 * t + .003 * math.sin(x * 9)))
    canvas_pair(out, inside, sod, 90, 2, LEGS[-1] - LEGS[0], .15, (0, 0, 1))
    # Rolled walls, tied up under the valance with canvas straps (and their tails).
    rolls = [((LEGS[0] + .05, -(HD + VAL_OUT + .09), ROLL_Z), (LEGS[-1] - .05, -(HD + VAL_OUT + .09), ROLL_Z))]
    rolls += [((sx * (x1 + .09), -HD + .05, ROLL_Z), (sx * (x1 + .09), HD - .05, ROLL_Z)) for sx in (-1, 1)]
    for a, b in rolls:
        a, b = Vector(a), Vector(b)
        spans = max(2, round((b - a).length / 1.5))
        out.kind = 2  # rolled cloth
        roll(out, a, b, spans, rng.random())
        axis_ = (b - a).normalized()
        ref = Vector((0, 0, 1))
        u_ = axis_.cross(ref).normalized()
        v_ = axis_.cross(u_).normalized()
        for end, sign in ((a, -1), (b, 1)):
            def cap(s, t, end=end):
                ang = 2 * math.pi * s
                spiral = 1 - t + .03 * math.sin(ang * 3 + t * 20)  # the end of a rolled cloth
                return end + (u_ * math.cos(ang) + v_ * math.sin(ang)) * ROLL_R * spiral
            out.kind = 2
            oriented(out, cap, 18, 3, 2 * math.pi * ROLL_R, ROLL_R, axis_ * sign)
        out.kind = inside.kind = 1  # webbing straps
        for k in range(spans + 1):
            c = a + (b - a) * min(max(k / spans, .03), .97)
            axis = (b - a).normalized()
            tube_surface(out, c - axis * .022, c + axis * .022, ROLL_R + .006, segments=18, rows=1)
            # The strap's tail hangs from the bottom of the roll.
            side = Vector((0, 0, 1)).cross(axis).normalized()

            def tail(s, t, c=c, axis=axis):
                return c + axis * (-.02 + .04 * s) + Vector((0, 0, -ROLL_R - .2 * t)) + side * (.01 * math.sin(t * 2.5))
            canvas_pair(out, inside, tail, 1, 4, .04, .2, side)

    out.kind = inside.kind = 0

    # ------------------------------------------------------------ frame (galvanised 48 mm tube)
    metal.kind = 0
    for x in LEGS:
        for y in (-HD, HD):
            metal.tube((x, y, .006), (x, y, EAVE), TUBE)
            metal.box((x - .075, y - .075, 0), (x + .075, y + .075, .006))
            metal.tube((x, y, EAVE - .1), (x, y, EAVE + .005), TUBE + .005)  # eave fitting
        for sy in (-1, 1):
            metal.tube((x, sy * HD, EAVE), (x, 0, RIDGE), TUBE)
        metal.tube((x - .06, 0, RIDGE), (x + .06, 0, RIDGE), TUBE + .005)      # ridge fitting
    for y in (-HD, HD):
        metal.tube((LEGS[0] - .03, y, EAVE), (LEGS[-1] + .03, y, EAVE), TUBE)
    metal.tube((LEGS[0] - .03, 0, RIDGE), (LEGS[-1] + .03, 0, RIDGE), TUBE)
    for sx in (-1, 1):
        metal.tube((sx * LEGS[-1], 0, .006), (sx * LEGS[-1], 0, RIDGE), TUBE)
        metal.box((sx * LEGS[-1] - .075, -.075, 0), (sx * LEGS[-1] + .075, .075, .006))
        metal.tube((sx * LEGS[-1], -HD, EAVE), (sx * LEGS[-1], HD, EAVE), TUBE * .8)
    # Guy ropes from the leg tops and corners to steel stakes, each with a wooden runner.
    anchors = [((x, sy * HD, EAVE), (x, sy * (HD + 1.6), 0)) for x in LEGS for sy in (-1, 1)]
    anchors += [((sx * LEGS[-1], 0, RIDGE - .05), (sx * (LEGS[-1] + 1.9), 0, 0)) for sx in (-1, 1)]
    for top, ground in anchors:
        top, ground = Vector(top), Vector(ground)
        metal.kind = 1
        points = []
        for k in range(6):
            t = k / 5
            p = top.lerp(ground, t)
            p.z -= .035 * math.sin(math.pi * t)
            points.append(p)
        for p, q in zip(points, points[1:]):
            metal.tube(p, q, .0045, segments=6)
        metal.kind = 2
        r = top.lerp(ground, .82)
        d = (ground - top).normalized()
        metal.beam(r - d * .06, r + d * .06, .035, .02, up=(0, 0, 1), bevel=.003)
        metal.kind = 3
        out_dir = Vector((ground.x - top.x, ground.y - top.y, 0)).normalized()
        metal.tube(ground + Vector((0, 0, -.3)) - out_dir * .1, ground + Vector((0, 0, .06)) + out_dir * .02, .009, segments=8)
        metal.tube(ground + Vector((0, 0, .06)) + out_dir * .02, ground + Vector((0, 0, .06)) + out_dir * .07, .007, segments=8)
    metal.kind = 0

    # ------------------------------------------------------------ pallet floor and a trestle table
    cols, rows, gap = 7, 6, .025
    span_x, span_y = cols * PALLET[0] + (cols - 1) * gap, rows * PALLET[1] + (rows - 1) * gap
    for i in range(cols):
        for j in range(rows):
            cx = -span_x / 2 + PALLET[0] / 2 + i * (PALLET[0] + gap) + rng.uniform(-.012, .012)
            cy = -span_y / 2 + PALLET[1] / 2 + j * (PALLET[1] + gap) + rng.uniform(-.012, .012)
            pallet(wood, cx, cy, rng.uniform(-.012, .012), rng.random())
    tx0, tx1, ty = TABLE
    top_z = PALLET[2] + .74
    wood.kind = 2
    wood.box((tx0, ty - .4, top_z), (tx1, ty + .4, top_z + .018), bevel=.002)
    wood.kind = 3
    for tx in (tx0 + .25, tx1 - .25):
        for sy in (-1, 1):
            wood.beam((tx - .18, ty + sy * .32, PALLET[2]), (tx, ty + sy * .32, top_z), .045, .045, up=(0, 1, 0))
            wood.beam((tx + .18, ty + sy * .32, PALLET[2]), (tx, ty + sy * .32, top_z), .045, .045, up=(0, 1, 0))
        wood.beam((tx, ty - .36, top_z - .04), (tx, ty + .36, top_z - .04), .07, .045)
        wood.beam((tx - .1, ty - .32, PALLET[2] + .3), (tx + .1, ty - .32, PALLET[2] + .3), .02, .07, up=(0, 0, 1))
    wood.kind = 0

    # ------------------------------------------------------------ two enamel pendant lamps under the ridge
    for lx in LAMPS:
        metal.kind = 5
        metal.tube((lx, 0, RIDGE - TUBE), (lx, 0, RIDGE - .52), .004, segments=6)
        metal.kind = 4
        profile = [(.012, RIDGE - .52), (.03, RIDGE - .54), (.07, RIDGE - .58), (.15, RIDGE - .64), (.19, RIDGE - .665), (.187, RIDGE - .672)]
        lathe(metal, lx, profile, 24, both=True)
        bulb.kind = 0
        lathe(bulb, lx, [(0, RIDGE - .55), (.025, RIDGE - .565), (.038, RIDGE - .6), (.03, RIDGE - .635), (0, RIDGE - .645)], 16)

    return {name: group.finish(collection) for name, group in groups.items()}


def lathe(group, cx, profile, segments, both=False):
    """Spins (r, z) around the vertical line through (cx, 0); both adds the inside faces."""
    rings = []
    for r, z in profile:
        rings.append([group.bm.verts.new((cx + r * math.cos(2 * math.pi * k / segments), r * math.sin(2 * math.pi * k / segments), z))
                      for k in range(segments)])
    faces = []
    for a, b in zip(rings, rings[1:]):
        for k in range(segments):
            m = (k + 1) % segments
            quad = [a[k], a[m], b[m], b[k]]
            faces.append(group.bm.faces.new(quad))
            if both:
                inner = [group.bm.verts.new(v.co + (Vector((cx, 0, v.co.z)) - v.co).normalized() * .001) for v in reversed(quad)]
                faces.append(group.bm.faces.new(inner))
    group.tag(faces)
    return faces


def pallet(g, cx, cy, yaw, value):
    """A Euro pallet (1200 x 800 x 144 mm): 3 bottom boards, 9 blocks, 3 stringer boards, 5 deck boards."""
    c, s = math.cos(yaw), math.sin(yaw)

    def put(x0, y0, z0, x1, y1, z1, kind):
        g.kind = kind
        faces = g.box((x0, y0, z0), (x1, y1, z1), value=value)
        verts = {v for f in faces for v in f.verts}
        for v in verts:
            x, y = v.co.x, v.co.y
            v.co.x, v.co.y = cx + x * c - y * s, cy + x * s + y * c
    L, Wd = PALLET[0] / 2, PALLET[1] / 2
    for y, w in ((-Wd + .05, .1), (0, .145), (Wd - .05, .1)):
        put(-L, y - w / 2, 0, L, y + w / 2, .022, 0)
    for x in (-L + .0725, 0, L - .0725):
        for y, w in ((-Wd + .05, .1), (0, .145), (Wd - .05, .1)):
            put(x - .0725, y - w / 2, .022, x + .0725, y + w / 2, .1, 1)
        put(x - .0725, -Wd, .1, x + .0725, Wd, .122, 0)
    for y, w in ((-Wd + .0725, .145), (-.18, .1), (0, .145), (.18, .1), (Wd - .0725, .145)):
        put(-L, y - w / 2, .122, L, y + w / 2, .144, 0)
    g.kind = 0
