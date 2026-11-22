"""A Basel residential street in the style of Gundeldingen: 19th-century row houses with
plastered facades, red-sandstone window surrounds, steep tiled roofs and small front gardens.

Local coordinates in metres: x along the street (east), y north, z up. Road centre on y = 0.
The north row faces south; Bob's house is the north-row house centred on x = 0, its facade at
y = FACADE_Y. All geometry is procedural and seeded; nothing here represents a real address.
"""
from __future__ import annotations

import math

import bpy

from . import util as U

FACADE_Y = 8.5
GROUND_FLOOR_Z = 1.2  # raised ground floor ("Hochparterre")
STOREY = 3.1
BOB_W = 7.2
ROAD_HALF = 3.6
SIDEWALK = 2.6

PLASTER = ["#c9b79a", "#b9a98f", "#d3c6ad", "#a8a593", "#c4a98a", "#b7ad9c", "#9fa39a", "#cdbfa6", "#bfa890"]
SANDSTONE = "#7d4a3a"  # Basel red sandstone


class Ctx:
    def __init__(self, coll):
        self.coll = coll
        self.mats = {}
        self.glass_cards = []  # (material, lit) for night windows

    def mat(self, key, factory):
        if key not in self.mats:
            self.mats[key] = factory()
        return self.mats[key]


# ----------------------------------------------------------------------------------------------
def wall_with_holes(name, width, height, holes, mat, coll, thickness=0.42):
    """A planar wall in the local xz plane (x 0..width, z 0..height) with rectangular openings.
    holes: [(x0, z0, x1, z1)]. The wall faces -y; thickness is extruded towards +y."""
    xs = sorted({0.0, width, *[h[0] for h in holes], *[h[2] for h in holes]})
    zs = sorted({0.0, height, *[h[1] for h in holes], *[h[3] for h in holes]})
    vid = {}
    verts, faces = [], []

    def v(x, z):
        k = (round(x, 4), round(z, 4))
        if k not in vid:
            vid[k] = len(verts)
            verts.append((x, 0.0, z))
        return vid[k]

    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            cx, cz = (xs[i] + xs[i + 1]) / 2, (zs[j] + zs[j + 1]) / 2
            if any(h[0] < cx < h[2] and h[1] < cz < h[3] for h in holes):
                continue
            faces.append((v(xs[i], zs[j]), v(xs[i + 1], zs[j]), v(xs[i + 1], zs[j + 1]), v(xs[i], zs[j + 1])))
    o = U.mesh_from(name, verts, faces, mat, coll)
    s = o.modifiers.new("solid", "SOLIDIFY")
    s.thickness = thickness
    s.offset = -1  # towards +y after rotation below (normal faces -y)
    s.use_even_offset = True
    return o


def interior_card_mat(ctx, r, lit):
    """Emissive 'room behind curtains' for neighbour windows at night."""
    key = f"card{lit}{r.randint(0, 5)}"

    def make():
        m = bpy.data.materials.new(key)
        nt, n, l = U._nodes(m)
        p = n["Principled BSDF"]
        p.inputs["Base Color"].default_value = U.hex_rgb("#1a1714")
        p.inputs["Roughness"].default_value = 0.9
        if lit:
            tc = n.new("ShaderNodeTexCoord")
            nz = n.new("ShaderNodeTexWave")
            nz.wave_type = "BANDS"
            nz.bands_direction = "X"
            nz.inputs["Scale"].default_value = r.uniform(6, 14)
            nz.inputs["Distortion"].default_value = 2.0
            l.new(tc.outputs["Object"], nz.inputs["Vector"])
            cr = n.new("ShaderNodeValToRGB")
            k = r.choice([2300, 2600, 2900, 3300])
            base = U.kelvin(k)[:3]
            cr.color_ramp.elements[0].color = (*[c * 0.35 for c in base], 1)
            cr.color_ramp.elements[1].color = (*base, 1)
            l.new(nz.outputs["Fac"], cr.inputs["Fac"])
            p.inputs["Emission Strength"].default_value = r.uniform(1.5, 4.0)
            l.new(cr.outputs["Color"], p.inputs["Emission Color"])
            U.ctrl_register("night_windows", p.inputs["Emission Strength"], scale=p.inputs["Emission Strength"].default_value)
        return m

    return ctx.mat(key, make)


def glass_mat(ctx):
    def make():
        m = bpy.data.materials.new("window glass")
        nt, n, l = U._nodes(m)
        for x in list(n):
            if x.type == "BSDF_PRINCIPLED":
                n.remove(x)
        g = n.new("ShaderNodeBsdfGlossy")
        g.inputs["Roughness"].default_value = 0.04
        g.inputs["Color"].default_value = (0.9, 0.92, 0.95, 1)
        t = n.new("ShaderNodeBsdfTransparent")
        fr = n.new("ShaderNodeLayerWeight")
        fr.inputs["Blend"].default_value = 0.25
        mx = n.new("ShaderNodeMixShader")
        l.new(fr.outputs["Fresnel"], mx.inputs["Fac"])
        l.new(t.outputs[0], mx.inputs[1])
        l.new(g.outputs[0], mx.inputs[2])
        l.new(mx.outputs[0], n["Material Output"].inputs["Surface"])
        return m

    return ctx.mat("glass", make)


def snow_mix_material(name, base_hex, rough=0.8, noise=0.25, snow_ctrl="snow", flat_only=True, scale=3.0):
    """Base material with snow on up-facing surfaces; the amount is the animated control `snow_ctrl`."""
    m = bpy.data.materials.new(name)
    nt, n, l = U._nodes(m)
    p = n["Principled BSDF"]
    base = U.hex_rgb(base_hex)
    tc = n.new("ShaderNodeTexCoord")
    nz = n.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = scale
    nz.inputs["Detail"].default_value = 8
    l.new(tc.outputs["Object"], nz.inputs["Vector"])
    # base colour variation
    bm = n.new("ShaderNodeMix")
    bm.data_type = "RGBA"
    bm.inputs[6].default_value = base
    bm.inputs[7].default_value = tuple(c * 0.7 for c in base[:3]) + (1,)
    mr = n.new("ShaderNodeMapRange")
    mr.inputs["To Max"].default_value = noise
    l.new(nz.outputs["Fac"], mr.inputs["Value"])
    l.new(mr.outputs["Result"], bm.inputs["Factor"])
    # snow mask = up-facing x noise threshold driven by snow amount
    geo = n.new("ShaderNodeNewGeometry")
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(geo.outputs["Normal"], sep.inputs[0])
    up = n.new("ShaderNodeMapRange")
    up.inputs["From Min"].default_value = 0.35
    up.inputs["From Max"].default_value = 0.75
    l.new(sep.outputs["Z"], up.inputs["Value"])
    amt = n.new("ShaderNodeMath")
    amt.operation = "SUBTRACT"
    amt.inputs[1].default_value = 1.0
    l.new(nz.outputs["Fac"], amt.inputs[0])  # fac - 1  in [-1, 0]
    add = n.new("ShaderNodeMath")
    add.operation = "ADD"
    add.use_clamp = False
    l.new(amt.outputs[0], add.inputs[0])
    add.inputs[1].default_value = 1.0  # snow amount (+ offset): keyed
    U.ctrl_register(snow_ctrl, add.inputs[1], scale=1.25)
    st = n.new("ShaderNodeMapRange")
    st.inputs["From Min"].default_value = 0.0
    st.inputs["From Max"].default_value = 0.12
    l.new(add.outputs[0], st.inputs["Value"])
    mask = n.new("ShaderNodeMath")
    mask.operation = "MULTIPLY"
    mask.use_clamp = True
    l.new(st.outputs["Result"], mask.inputs[0])
    if flat_only:
        l.new(up.outputs["Result"], mask.inputs[1])
    else:
        mask.inputs[1].default_value = 1
    col = n.new("ShaderNodeMix")
    col.data_type = "RGBA"
    l.new(mask.outputs[0], col.inputs["Factor"])
    l.new(bm.outputs[2], col.inputs[6])
    col.inputs[7].default_value = (0.82, 0.86, 0.92, 1)
    l.new(col.outputs[2], p.inputs["Base Color"])
    rr = n.new("ShaderNodeMix")
    rr.data_type = "FLOAT"
    l.new(mask.outputs[0], rr.inputs["Factor"])
    rr.inputs[2].default_value = rough
    rr.inputs[3].default_value = 0.55
    l.new(rr.outputs[0], p.inputs["Roughness"])
    # snow sparkle via subsurface-ish sheen is too costly; keep a soft coat
    b = n.new("ShaderNodeBump")
    b.inputs["Strength"].default_value = 0.15
    l.new(nz.outputs["Fac"], b.inputs["Height"])
    l.new(b.outputs["Normal"], p.inputs["Normal"])
    # wet look when the snow is gone (March): lower roughness, controlled by "wet"
    return m


def asphalt_material():
    """Road: snow with two darker, wet tyre tracks per lane."""
    m = snow_mix_material("asphalt", "#2b2b2d", rough=0.55, noise=0.35, scale=1.6)
    nt = m.node_tree
    n, l = nt.nodes, nt.links
    mask = next(x for x in n if x.type == "MATH" and x.operation == "MULTIPLY" and x.use_clamp)
    tc = n.new("ShaderNodeTexCoord")
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Object"], sep.inputs[0])
    # tracks at |y| = 0.9 and 2.6 (two lanes), width ~0.35 m, softened with noise
    w = n.new("ShaderNodeTexWave")
    w.wave_type = "BANDS"
    w.bands_direction = "Y"
    w.wave_profile = "SIN"
    w.inputs["Scale"].default_value = 0.0
    tri = n.new("ShaderNodeMath")
    tri.operation = "PINGPONG"
    tri.inputs[1].default_value = 0.85
    off = n.new("ShaderNodeMath")
    off.operation = "ADD"
    off.inputs[1].default_value = 0.0
    l.new(sep.outputs["Y"], off.inputs[0])
    l.new(off.outputs[0], tri.inputs[0])
    tr = n.new("ShaderNodeMapRange")
    tr.inputs["From Min"].default_value = 0.0
    tr.inputs["From Max"].default_value = 0.3
    tr.inputs["To Min"].default_value = 0.25
    tr.inputs["To Max"].default_value = 1.0
    l.new(tri.outputs[0], tr.inputs["Value"])
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    mul.use_clamp = True
    out_links = [lk for lk in nt.links if lk.from_node == mask]
    l.new(mask.outputs[0], mul.inputs[0])
    l.new(tr.outputs["Result"], mul.inputs[1])
    for lk in out_links:
        l.new(mul.outputs[0], lk.to_socket)
    return m


# ----------------------------------------------------------------------------------------------
def row_house(ctx, x0, width, r, bob=False, storeys=None, south=False):
    """One row house occupying x0..x0+width on the north (or south, mirrored) side of the street."""
    coll = ctx.coll
    storeys = storeys or r.choice([3, 3, 4, 4, 4])
    h_wall = GROUND_FLOOR_Z + storeys * STOREY + 0.4
    plaster_hex = PLASTER[r.randrange(len(PLASTER))] if not bob else "#c7b291"
    plaster = ctx.mat(("plaster", plaster_hex), lambda: U.principled(f"plaster {plaster_hex}", U.hex_rgb(plaster_hex), rough=0.92, noise=0.35, noise_scale=1.3, bump=0.25))
    stone = ctx.mat("sandstone", lambda: U.principled("red sandstone", U.hex_rgb(SANDSTONE), rough=0.85, noise=0.5, noise_scale=6, bump=0.4))
    frame_hex = r.choice(["#e8e4da", "#ded7c8", "#3b4a3c", "#e8e4da"])
    frame = ctx.mat(("frame", frame_hex), lambda: U.principled(f"window frame {frame_hex}", U.hex_rgb(frame_hex), rough=0.45))
    glass = glass_mat(ctx)

    n_ax = 3 if width > 6.5 else 2
    win_w, win_h = 1.15, 1.75
    holes, windows = [], []
    door_axis = 0 if not bob else n_ax - 1
    for s in range(storeys):
        z0 = GROUND_FLOOR_Z + 0.85 + s * STOREY
        for a in range(n_ax):
            cx = width * (a + 0.5) / n_ax
            if s == 0 and a == door_axis:
                continue
            holes.append((cx - win_w / 2, z0, cx + win_w / 2, z0 + win_h))
            windows.append((cx, z0, s, a))
    # entrance door (raised, steps)
    dcx = width * (door_axis + 0.5) / n_ax
    holes.append((dcx - 0.6, GROUND_FLOOR_Z, dcx + 0.6, GROUND_FLOOR_Z + 2.35))
    # basement window
    for a in range(n_ax):
        if a == door_axis:
            continue
        cx = width * (a + 0.5) / n_ax
        holes.append((cx - 0.45, 0.28, cx + 0.45, 0.85))
    sign = -1 if south else 1
    fy = FACADE_Y * sign
    wall = wall_with_holes(f"facade {x0:.0f}{'S' if south else ''}", width, h_wall, holes, plaster, coll)
    wall.location = (x0, fy, 0)
    if south:
        wall.rotation_euler = (0, 0, math.pi)
        wall.location = (x0 + width, fy, 0)
    # body of the house behind the facade (side walls shared; a box is enough from the street)
    depth = 12.0
    body = None if bob else U.box(f"body {x0:.0f}", (width - 0.02, depth - 0.5, h_wall), (x0 + width / 2, sign * (FACADE_Y + 0.42 + (depth - 0.5) / 2), h_wall / 2), plaster, coll=coll)
    if bob:
        ctx.bob_body = body
    # plinth (sandstone) and cornice
    U.box("plinth", (width, 0.12, GROUND_FLOOR_Z - 0.05), (x0 + width / 2, fy - sign * 0.05, (GROUND_FLOOR_Z - 0.05) / 2), stone, coll=coll)
    for z in [GROUND_FLOOR_Z + STOREY * k + 0.05 for k in (1,)] + [h_wall - 0.25]:
        U.box("cornice", (width, 0.22, 0.22 if z < h_wall - 1 else 0.4), (x0 + width / 2, fy - sign * 0.1, z), stone if z < h_wall - 1 else plaster, bevel=0.03, coll=coll)
    # windows: sandstone surround, frame, glass, sill, interior card
    for cx, z0, s, a in windows:
        wx = x0 + cx if not south else x0 + width - cx
        living = bob and s == 0
        sur = 0.14
        for (sx, sz, ox, oz) in ((win_w + 2 * sur, sur, 0, win_h + sur / 2), (win_w + 2 * sur + 0.1, 0.12, 0, -0.06),
                                 (sur, win_h, -(win_w + sur) / 2, win_h / 2), (sur, win_h, (win_w + sur) / 2, win_h / 2)):
            U.box("surround", (sx, 0.1, sz), (wx + ox, fy - sign * 0.05, z0 + oz), stone, coll=coll)
        # sill (snow sits on it)
        U.box("sill", (win_w + 0.4, 0.28, 0.07), (wx, fy - sign * 0.12, z0 - 0.08), ctx.mat("sillsnow", lambda: snow_mix_material("sill", SANDSTONE, scale=8)), coll=coll)
        # frame: outer + mullion + transom
        fdepth = fy + sign * 0.22
        for (sx, sz, ox, oz) in ((win_w, 0.07, 0, 0.035), (win_w, 0.07, 0, win_h - 0.035), (0.07, win_h, -win_w / 2 + 0.035, win_h / 2),
                                 (0.07, win_h, win_w / 2 - 0.035, win_h / 2), (0.06, win_h, 0, win_h / 2), (win_w, 0.06, 0, win_h * 0.72)):
            U.box("frame", (sx, 0.08, sz), (wx + ox, fdepth, z0 + oz), frame, coll=coll)
        gl = U.box("glass", (win_w - 0.08, 0.01, win_h - 0.08), (wx, fdepth + sign * 0.01, z0 + win_h / 2), glass, coll=coll)
        if living:
            gl.name = "bob living glass"
            ctx.bob_living_window = (wx, z0, win_w, win_h)
            continue
        lit = r.random() < (0.62 if not bob else 0.0)
        card = U.box("room", (win_w, 0.02, win_h), (wx, fdepth + sign * 0.55, z0 + win_h / 2), interior_card_mat(ctx, r, lit), coll=coll)
        # shutters on some houses (folded open, Basel green/grey)
        if frame_hex == "#3b4a3c" or r.random() < 0.15:
            sh = ctx.mat("shutter", lambda: U.principled("shutter", U.hex_rgb("#4b5a4a"), rough=0.6, noise=0.2))
            for sx in (-1, 1):
                U.box("shutter", (0.58, 0.05, win_h), (wx + sx * (win_w / 2 + 0.45), fy - sign * 0.07, z0 + win_h / 2), sh, coll=coll)
    # door
    door_m = ctx.mat(("door", r.randint(0, 2)), lambda: U.principled("door wood", U.hex_rgb(r.choice(["#3a2a20", "#2f3a33", "#4a3526"])), rough=0.5, noise=0.4, noise_scale=20, bump=0.1))
    dwx = x0 + dcx if not south else x0 + width - dcx
    U.box("door", (1.1, 0.08, 2.3), (dwx, fy + sign * 0.3, GROUND_FLOOR_Z + 1.15), door_m, coll=coll)
    U.box("door lintel", (1.5, 0.12, 0.25), (dwx, fy - sign * 0.06, GROUND_FLOOR_Z + 2.45), stone, coll=coll)
    steps = ctx.mat("steps", lambda: snow_mix_material("steps", "#6f6a63", scale=6))
    for k in range(4):
        U.box("step", (1.6, 0.3, 0.3 * (k + 1)), (dwx, fy - sign * (0.15 + 0.3 * (3 - k)), 0.15 * (k + 1)), steps, coll=coll)
    # door light
    U.point_light("door lamp", (dwx + 0.9, fy - sign * 0.3, GROUND_FLOOR_Z + 2.6), r.uniform(8, 20), U.kelvin(2700), 0.05, coll=coll)
    # basement window glass (dark)
    for a in range(n_ax):
        if a == door_axis:
            continue
        cx = width * (a + 0.5) / n_ax
        bwx = x0 + cx if not south else x0 + width - cx
        g = U.box("basement glass", (0.9, 0.02, 0.57), (bwx, fy + sign * 0.2, 0.565), glass, coll=coll)
        if bob:
            g.name = "bob basement glass"
            ctx.bob_basement_window = (bwx, 0.28, 0.9, 0.57)
        else:
            U.box("basement dark", (0.9, 0.02, 0.57), (bwx, fy + sign * 0.6, 0.565), interior_card_mat(ctx, r, False), coll=coll)
    # roof: steep gable, ridge along x
    roof_m = ctx.mat("rooftiles", lambda: snow_mix_material("roof tiles", "#7a3d2c", rough=0.7, noise=0.4, scale=14))
    pitch = math.radians(r.choice([48, 52, 55]))
    run = depth / 2 + 0.5
    rise = run * math.tan(pitch) * 0.55
    yb = sign * (FACADE_Y - 0.4)
    yt = sign * (FACADE_Y + depth / 2)
    verts = [(x0, yb, h_wall), (x0 + width, yb, h_wall), (x0 + width, yt, h_wall + rise), (x0, yt, h_wall + rise),
             (x0, sign * (FACADE_Y + depth + 0.4), h_wall), (x0 + width, sign * (FACADE_Y + depth + 0.4), h_wall)]
    faces = [(0, 1, 2, 3), (3, 2, 5, 4)] if not south else [(3, 2, 1, 0), (4, 5, 2, 3)]
    roof = U.mesh_from("roof", verts, faces, roof_m, coll)
    sol = roof.modifiers.new("s", "SOLIDIFY")
    sol.thickness = 0.18
    # dormer
    if r.random() < 0.6:
        dx = x0 + width * r.choice([0.3, 0.5, 0.7])
        dy = sign * (FACADE_Y + 1.6)
        U.box("dormer", (1.6, 1.8, 1.6), (dx, dy, h_wall + 0.9), plaster, coll=coll)
        U.box("dormer roof", (1.9, 2.0, 0.15), (dx, dy, h_wall + 1.75), roof_m, coll=coll, rot=(sign * -0.12, 0, 0))
        U.box("dormer glass", (0.9, 0.02, 1.0), (dx, dy - sign * 0.91, h_wall + 0.9), interior_card_mat(ctx, r, r.random() < 0.4), coll=coll)
    # chimney
    if r.random() < 0.7:
        U.box("chimney", (0.55, 0.55, 1.6), (x0 + r.uniform(0.8, width - 0.8), yt - sign * 1.2, h_wall + rise - 0.2), plaster, coll=coll)
    # front garden: low wall + fence + hedge
    gy = sign * (ROAD_HALF + SIDEWALK + 0.15)
    U.box("garden wall", (width, 0.3, 0.45), (x0 + width / 2, gy, 0.225), ctx.mat("gwall", lambda: snow_mix_material("garden wall", "#77706a", scale=5)), bevel=0.02, coll=coll)
    if r.random() < 0.7:
        hedge = ctx.mat("hedge", lambda: snow_mix_material("hedge", "#1f2b1d", rough=0.95, noise=0.6, scale=9, flat_only=False))
        U.box("hedge", (width - 1.6, 0.6, 0.9), (x0 + width / 2 - 0.4, gy + sign * 0.6, 0.45 + 0.1), hedge, bevel=0.2, coll=coll)
    return h_wall


def street_lamp(ctx, x, y, r):
    coll = ctx.coll
    metal = ctx.mat("lampmetal", lambda: U.principled("lamp metal", U.hex_rgb("#2b2f2e"), rough=0.4, metal=0.8))
    U.cylinder("lamp pole", 0.07, 5.2, (x, y, 2.6), metal, verts=12, coll=coll)
    arm = U.tube_path("lamp arm", [(x, y, 5.1), (x, y + 0.4 * math.copysign(1, -y), 5.4), (x, y + 1.2 * math.copysign(1, -y), 5.45)], 0.04, metal, coll)
    hy = y + 1.2 * math.copysign(1, -y)
    U.box("lamp head", (0.5, 0.28, 0.12), (x, hy, 5.38), metal, bevel=0.03, coll=coll)
    lens = ctx.mat("lamplens", lambda: U.emission("lamp lens", U.kelvin(3000), 25))
    U.box("lamp lens", (0.42, 0.22, 0.02), (x, hy, 5.31), lens, coll=coll)
    sp = U.spot_light("street lamp", (x, hy, 5.25), 900, U.kelvin(3000), (x, hy, 0), angle=2.3, blend=0.9, radius=0.18, coll=coll)
    return sp


def car(ctx, x, y, rot, r):
    coll = ctx.coll
    paint_hex = r.choice(["#2c3a4a", "#7d8186", "#1c1d20", "#8c2a22", "#d9d6cf", "#3e4a3a", "#55585c"])
    paint = ctx.mat(("paint", paint_hex), lambda: U.principled(f"car paint {paint_hex}", U.hex_rgb(paint_hex), rough=0.25, metal=0.6, coat=1.0))
    snow = ctx.mat("carsnow", lambda: U.principled("car snow", (0.82, 0.86, 0.92, 1), rough=0.6, noise=0.2, bump=0.4, noise_scale=12))
    tyre = ctx.mat("tyre", lambda: U.principled("tyre", U.hex_rgb("#151515"), rough=0.8))
    glass = ctx.mat("carglass", lambda: U.principled("car glass", U.hex_rgb("#0d1115"), rough=0.05, metal=0.3))
    c, s = math.cos(rot), math.sin(rot)

    def at(dx, dy, z):
        return (x + c * dx - s * dy, y + s * dx + c * dy, z)

    L, W = r.uniform(4.1, 4.7), 1.8
    U.box("car body", (L, W, 0.75), at(0, 0, 0.62), paint, bevel=0.18, coll=coll, rot=(0, 0, rot))
    U.box("car cabin", (L * 0.52, W * 0.9, 0.55), at(-0.15, 0, 1.22), glass, bevel=0.16, coll=coll, rot=(0, 0, rot))
    U.box("car roof", (L * 0.42, W * 0.84, 0.06), at(-0.15, 0, 1.5), paint, bevel=0.03, coll=coll, rot=(0, 0, rot))
    U.box("car snow", (L * 0.4, W * 0.8, 0.09), at(-0.15, 0, 1.56), snow, bevel=0.04, coll=coll, rot=(0, 0, rot))
    U.box("bonnet snow", (L * 0.22, W * 0.78, 0.06), at(L * 0.33, 0, 1.0), snow, bevel=0.03, coll=coll, rot=(0, 0, rot))
    for dx in (-L * 0.32, L * 0.32):
        for dy in (-W / 2 + 0.1, W / 2 - 0.1):
            U.cylinder("wheel", 0.33, 0.24, at(dx, dy, 0.33), tyre, verts=16, rot=(math.pi / 2, 0, rot), coll=coll)


def bare_tree(ctx, x, y, r, height=9.0):
    coll = ctx.coll
    bark = ctx.mat("bark", lambda: U.principled("bark", U.hex_rgb("#2a231e"), rough=0.9, noise=0.4, bump=0.5, noise_scale=30))
    snowb = ctx.mat("snowbark", lambda: snow_mix_material("bark snow", "#2a231e", rough=0.9, scale=20))
    U.cylinder("trunk", 0.18, height * 0.45, (x, y, height * 0.225), bark, verts=10, coll=coll)
    # tree pit with snow
    def branch(p, d, length, radius, depth):
        if depth == 0 or radius < 0.012:
            return
        e = (p[0] + d[0] * length, p[1] + d[1] * length, p[2] + d[2] * length)
        U.tube_path("branch", [p, e], radius, snowb if depth < 3 else bark, coll, res=4)
        for k in range(r.choice([2, 3])):
            a = r.uniform(0, 2 * math.pi)
            spread = r.uniform(0.35, 0.7)
            nd = (d[0] * 0.6 + math.cos(a) * spread, d[1] * 0.6 + math.sin(a) * spread, d[2] * 0.75 + 0.2)
            ln = math.sqrt(sum(v * v for v in nd))
            nd = tuple(v / ln for v in nd)
            branch(e, nd, length * r.uniform(0.62, 0.78), radius * 0.62, depth - 1)

    top = (x, y, height * 0.45)
    for k in range(4):
        a = k * math.pi / 2 + r.uniform(-0.4, 0.4)
        d = (math.cos(a) * 0.45, math.sin(a) * 0.45, 0.89)
        branch(top, d, height * 0.22, 0.11, 5)


def snowfall(ctx, center, size, count, seed, frame_start, frame_end, name="snowfall"):
    """Falling snow: particle system emitting small soft flakes from a plane above the camera area."""
    coll = ctx.coll
    em = U.mesh_from(name, [(-.5, -.5, 0), (.5, -.5, 0), (.5, .5, 0), (-.5, .5, 0)], [(0, 1, 2, 3)], None, coll)
    em.location = center
    em.scale = (size[0], size[1], 1)
    flake_m = ctx.mat("flake", lambda: U.emission("flake", (0.85, 0.9, 1.0), 0.6, transparent_mix=0.65))
    fl = U.sphere("flake", 0.022, (0, 0, -500), flake_m, seg=6, coll=coll)
    ps = em.modifiers.new("snow", "PARTICLE_SYSTEM").particle_system
    st = ps.settings
    st.count = count
    st.frame_start = frame_start - 240
    st.frame_end = frame_end
    st.lifetime = 400
    st.emit_from = "FACE"
    st.normal_factor = 0
    st.object_align_factor = (0, 0, -0.9)
    st.effector_weights.gravity = 0.0
    st.brownian_factor = 0.25
    st.drag_factor = 0.0
    st.render_type = "OBJECT"
    st.instance_object = fl
    st.particle_size = 1.0
    st.size_random = 0.6
    st.use_rotation_instance = False
    ps.seed = seed
    em.show_instancer_for_render = False
    return em


def build_street(coll, seed=11, x_from=-46, x_to=46, snow_ctrl=True):
    """North row (Bob's house at x=0), south row, road, sidewalks, lamps, cars, trees, tram wires."""
    r = U.rng(seed)
    ctx = Ctx(coll)
    # north row
    x = -BOB_W / 2
    xs = []
    while x > x_from:
        w = r.choice([6.4, 7.0, 7.6, 8.2])
        x -= w
        xs.append((x, w))
    xs.reverse()
    xs.append((-BOB_W / 2, BOB_W))
    x = BOB_W / 2
    while x < x_to:
        w = r.choice([6.4, 7.0, 7.6, 8.2])
        xs.append((x, w))
        x += w
    heights = {}
    for x0, w in xs:
        bob = abs(x0 + BOB_W / 2) < 1e-6
        heights[x0] = row_house(ctx, x0, w, r, bob=bob, storeys=3 if bob else None)
    # south row
    x = x_from
    while x < x_to:
        w = r.choice([6.4, 7.0, 7.6, 8.2, 9.0])
        row_house(ctx, x, w, r, south=True)
        x += w
    # ground: road, sidewalks, gardens
    road = ctx.mat("asphalt", asphalt_material)
    walk = ctx.mat("walk", lambda: snow_mix_material("sidewalk", "#5d5a56", rough=0.8, noise=0.3, scale=2.5))
    garden = ctx.mat("garden", lambda: snow_mix_material("front garden", "#2b3324", rough=0.95, noise=0.5, scale=3))
    L = x_to - x_from + 60
    U.box("road", (L, 2 * ROAD_HALF, 0.1), ((x_from + x_to) / 2, 0, -0.05), road, coll=coll)
    for s in (-1, 1):
        U.box("sidewalk", (L, SIDEWALK, 0.22), ((x_from + x_to) / 2, s * (ROAD_HALF + SIDEWALK / 2), 0.06), walk, coll=coll)
        U.box("kerb", (L, 0.18, 0.2), ((x_from + x_to) / 2, s * (ROAD_HALF + 0.09), 0.07), ctx.mat("kerb", lambda: snow_mix_material("kerb", "#8b8780", scale=6)), coll=coll)
        U.box("garden", (L, FACADE_Y - ROAD_HALF - SIDEWALK, 0.25), ((x_from + x_to) / 2, s * (ROAD_HALF + SIDEWALK + (FACADE_Y - ROAD_HALF - SIDEWALK) / 2), 0.07), garden, coll=coll)
    # snow banks along the kerb
    bank = ctx.mat("bank", lambda: snow_mix_material("snow bank", "#c9cfd8", rough=0.7, noise=0.2, scale=4, flat_only=False))
    for s in (-1, 1):
        for k in range(30):
            bx = x_from + k * (L - 60) / 30 + r.uniform(-1, 1)
            b = U.sphere("snowbank", 1.0, (bx, s * (ROAD_HALF - 0.12), -0.05), bank, seg=12, coll=coll, scale=(r.uniform(0.9, 1.8), 0.32, 0.17))
            ctx.mats.setdefault("_banks", []).append(b)
    # lamps
    for k, lx in enumerate(range(int(x_from) + 6, int(x_to), 22)):
        street_lamp(ctx, lx + (11 if k % 2 else 0), (-1 if k % 2 else 1) * (ROAD_HALF + 0.5), r)
    # parked cars (south side lane and north)
    for cx in (-30, -21, -9.5, 13, 24, 36):
        car(ctx, cx + r.uniform(-1, 1), -(ROAD_HALF - 1.15), 0 if r.random() < 0.5 else math.pi, r)
    for cx in (-17, 6.5, 30):
        car(ctx, cx, ROAD_HALF - 1.15, math.pi, r)
    # trees in the south gardens / sidewalk
    for tx in (-38, -14, 18, 41):
        bare_tree(ctx, tx, -(ROAD_HALF + SIDEWALK - 0.7), r, height=r.uniform(8, 11))
    # tram / trolley overhead wires across and along the street (Basel cue)
    wire = ctx.mat("wire", lambda: U.principled("wire", U.hex_rgb("#111111"), rough=0.5, metal=0.5))
    for wx in range(int(x_from), int(x_to), 30):
        U.tube_path("span wire", [(wx, -FACADE_Y + 0.4, 7.6), (wx, 0, 7.1), (wx, FACADE_Y - 0.4, 7.6)], 0.008, wire, coll)
    for wy in (-0.6, 0.6):
        U.tube_path("contact wire", [(x_from - 30, wy, 7.15), (x_to + 30, wy, 7.15)], 0.007, wire, coll)
    ctx.heights = heights
    return ctx
