"""Bob's living room (raised ground floor) and basement plant room, built inside his house shell.

Bob's house spans x -3.6..3.6; the facade's inner face is at y = 8.92. Living room: x -3.5..1.4,
z 1.2..4.3. Basement: x -3.5..3.5, z -1.35..1.0. Animated controls (util.CTRL):
  servers_on   0..1  server front lights, fans
  flow         0..1  warm pulses in the pipes
  flow_phase   pulse offset (keyed linearly)
  tank_level   0..1  stored heat in the tank (35 kWh = 1)
  radiator     0..1  warm glow above the radiator / warm bounce light in the room
  frost        0..1  frost on the living-room window
  lamp         0..1  Bob's reading lamp
  paused       0..1  servers paused (status lights amber, fans stop)
"""
from __future__ import annotations

import math

import bpy

from . import street as S
from . import util as U

FY = S.FACADE_Y + 0.42  # inner face of the facade
LIV = dict(x0=-3.5, x1=1.4, y0=FY, y1=FY + 5.2, z0=S.GROUND_FLOOR_Z, z1=S.GROUND_FLOOR_Z + S.STOREY)
BAS = dict(x0=-3.5, x1=3.5, y0=FY, y1=FY + 5.6, z0=-1.35, z1=1.0)
WARM = U.hex_rgb("#ff8a2a")
HEAT = U.hex_rgb("#ff7a1f")


def flow_material(name, base_hex, metal=0.9, rough=0.35):
    """Pipe surface with warm pulses running along the curve (Generated X = along the length)."""
    m = bpy.data.materials.new(name)
    nt, n, l = U._nodes(m)
    p = n["Principled BSDF"]
    p.inputs["Base Color"].default_value = U.hex_rgb(base_hex)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    tc = n.new("ShaderNodeTexCoord")
    mp = n.new("ShaderNodeMapping")
    l.new(tc.outputs["UV"], mp.inputs["Vector"])
    U.ctrl_register("flow_phase", mp.inputs["Location"], index=0, scale=-1)
    w = n.new("ShaderNodeTexWave")
    w.wave_type = "BANDS"
    w.bands_direction = "X"
    w.wave_profile = "SAW"
    w.inputs["Scale"].default_value = 3.0
    l.new(mp.outputs["Vector"], w.inputs["Vector"])
    pw = n.new("ShaderNodeMath")
    pw.operation = "POWER"
    pw.inputs[1].default_value = 3.0
    l.new(w.outputs["Fac"], pw.inputs[0])
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    l.new(pw.outputs[0], mul.inputs[0])
    mul.inputs[1].default_value = 0.0
    U.ctrl_register("flow", mul.inputs[1], scale=14.0)
    base_glow = n.new("ShaderNodeMath")
    base_glow.operation = "MULTIPLY_ADD"
    l.new(pw.outputs[0], base_glow.inputs[0])
    base_glow.inputs[1].default_value = 0
    p.inputs["Emission Color"].default_value = HEAT
    l.new(mul.outputs[0], p.inputs["Emission Strength"])
    return m


def build(coll, with_bob_room=True):
    r = U.rng(7)
    mats = {}

    def M(key, f):
        if key not in mats:
            mats[key] = f()
        return mats[key]

    plaster_in = M("pin", lambda: U.principled("interior plaster", U.hex_rgb("#d9d2c4"), rough=0.9, noise=0.15, noise_scale=2, bump=0.05))
    parquet = M("parquet", lambda: _parquet())
    concrete = M("conc", lambda: U.principled("basement concrete", U.hex_rgb("#7c7a75"), rough=0.92, noise=0.55, noise_scale=1.8, bump=0.35))
    floor_c = M("floorc", lambda: U.principled("basement floor", U.hex_rgb("#5f5d59"), rough=0.7, noise=0.6, noise_scale=1.2, bump=0.2))
    white = M("white", lambda: U.principled("white enamel", U.hex_rgb("#e9e6df"), rough=0.35))

    L, B = LIV, BAS
    # ---- living room shell ------------------------------------------------------------------
    floor = U.box("living floor", (L["x1"] - L["x0"], L["y1"] - L["y0"], 0.22), ((L["x0"] + L["x1"]) / 2, (L["y0"] + L["y1"]) / 2, L["z0"] - 0.11), parquet, coll=coll)
    ceil = U.box("living ceiling", (L["x1"] - L["x0"], L["y1"] - L["y0"], 0.2), ((L["x0"] + L["x1"]) / 2, (L["y0"] + L["y1"]) / 2, L["z1"] + 0.1), plaster_in, coll=coll)
    walls = []
    walls.append(U.box("living wall W", (0.2, L["y1"] - L["y0"], L["z1"] - L["z0"]), (L["x0"] - 0.1, (L["y0"] + L["y1"]) / 2, (L["z0"] + L["z1"]) / 2), plaster_in, coll=coll))
    walls.append(U.box("living wall E", (0.2, L["y1"] - L["y0"], L["z1"] - L["z0"]), (L["x1"] + 0.1, (L["y0"] + L["y1"]) / 2, (L["z0"] + L["z1"]) / 2), plaster_in, coll=coll))
    walls.append(U.box("living wall N", (L["x1"] - L["x0"] + 0.4, 0.2, L["z1"] - L["z0"]), ((L["x0"] + L["x1"]) / 2, L["y1"] + 0.1, (L["z0"] + L["z1"]) / 2), plaster_in, coll=coll))
    # stucco cornice + skirting
    for (sx, sy, cx, cy) in (((L["x1"] - L["x0"]), 0.12, (L["x0"] + L["x1"]) / 2, L["y1"] - 0.06), (0.12, L["y1"] - L["y0"], L["x0"] + 0.06, (L["y0"] + L["y1"]) / 2), (0.12, L["y1"] - L["y0"], L["x1"] - 0.06, (L["y0"] + L["y1"]) / 2)):
        U.box("cornice", (sx, sy, 0.14), (cx, cy, L["z1"] - 0.07), white, bevel=0.03, coll=coll)
        U.box("skirting", (sx, sy, 0.1), (cx, cy, L["z0"] + 0.05), white, coll=coll)
    # window reveal (inner lining around the two windows), radiator under window 1 (x = -2.4)
    for wx in (-2.4, 0.0):
        U.box("window board", (1.5, 0.32, 0.05), (wx, FY + 0.12, L["z0"] + 0.85 - 0.04), white, bevel=0.01, coll=coll)
    rad_x = -2.4
    rad = []
    for k in range(14):
        rad.append(U.box("radiator rib", (0.055, 0.12, 0.6), (rad_x - 0.42 + k * 0.065, FY + 0.17, L["z0"] + 0.42), white, bevel=0.02, coll=coll))
    U.box("radiator header", (0.95, 0.06, 0.04), (rad_x, FY + 0.17, L["z0"] + 0.74), white, coll=coll)
    valve = U.cylinder("thermostat valve", 0.03, 0.1, (rad_x + 0.55, FY + 0.17, L["z0"] + 0.66), white, verts=16, coll=coll)
    # warm air glow above radiator (soft transparent emissive card)
    glow = _glow_card("radiator glow", (rad_x, FY + 0.32, L["z0"] + 1.15), (1.1, 0.9), coll)
    # room bounce from the radiator when warm
    warm_light = U.area_light("radiator warmth", (rad_x, FY + 0.6, L["z0"] + 0.9), 1.2, 0, U.kelvin(2200), target=(rad_x, FY + 3, L["z0"] + 1.5), size_y=0.6, coll=coll)
    U.ctrl_register("radiator", warm_light.data, "energy", scale=60)
    # frost on living window glass (two windows)
    frost = M("frost", _frost)
    for wx in (-2.4, 0.0):
        U.box("frost", (1.06, 0.005, 1.66), (wx, S.FACADE_Y + 0.235, L["z0"] + 0.85 + 0.875), frost, coll=coll)
    # curtains (sheer) at the sides of the windows
    curtain = M("curtain", lambda: U.principled("curtain", U.hex_rgb("#c8bfae"), rough=0.9, noise=0.3, noise_scale=40, transmission=0.0))
    for wx in (-2.4, 0.0):
        for s in (-1, 1):
            c = U.box("curtain", (0.28, 0.06, 2.4), (wx + s * 0.86, FY + 0.12, L["z0"] + 1.45), curtain, bevel=0.03, coll=coll)
            wv = c.modifiers.new("wave", "WAVE")
            wv.use_normal = False
    # furniture
    _armchair(coll, (-1.25, FY + 1.35, L["z0"]), math.radians(200), M)
    _side_table_lamp(coll, (-0.35, FY + 1.05, L["z0"]), M)
    _bookshelf(coll, (L["x0"] + 0.2, FY + 3.4, L["z0"]), r, M)
    _rug(coll, (-1.0, FY + 2.3, L["z0"] + 0.005), M)
    _picture(coll, (-0.4, L["y1"] - 0.02, L["z0"] + 1.75), M)
    _plant(coll, (0.95, FY + 0.6, L["z0"]), M)
    # ceiling lamp (off, decorative)
    U.cylinder("ceiling rose", 0.18, 0.04, (-1.0, FY + 2.5, L["z1"] - 0.02), white, verts=24, coll=coll)

    # ---- basement --------------------------------------------------------------------------
    slab = U.box("ground floor slab", (B["x1"] - B["x0"] + 0.4, B["y1"] - B["y0"] + 0.4, 0.2), ((B["x0"] + B["x1"]) / 2, (B["y0"] + B["y1"]) / 2, B["z1"] + 0.1 - 0.02), concrete, coll=coll)
    bfloor = U.box("basement floor", (B["x1"] - B["x0"], B["y1"] - B["y0"], 0.2), ((B["x0"] + B["x1"]) / 2, (B["y0"] + B["y1"]) / 2, B["z0"] - 0.1), floor_c, coll=coll)
    for (sx, sy, cx, cy) in ((0.3, B["y1"] - B["y0"], B["x0"] - 0.15, (B["y0"] + B["y1"]) / 2), (0.3, B["y1"] - B["y0"], B["x1"] + 0.15, (B["y0"] + B["y1"]) / 2),
                             (B["x1"] - B["x0"] + 0.6, 0.3, (B["x0"] + B["x1"]) / 2, B["y1"] + 0.15)):
        U.box("basement wall", (sx, sy, B["z1"] - B["z0"] + 0.25), (cx, cy, (B["z0"] + B["z1"]) / 2 + 0.1), concrete, coll=coll)
    # street-side basement wall below ground (below the facade, z < 0.28 except windows) —
    # the facade covers z >= 0; extend down
    U.box("basement wall S", (B["x1"] - B["x0"] + 0.6, 0.42, -B["z0"]), ((B["x0"] + B["x1"]) / 2, S.FACADE_Y + 0.21, B["z0"] / 2), concrete, coll=coll)
    # light well soil outside basement windows is the garden box; fine.
    # ceiling joists / pipes clutter
    for k in range(6):
        U.box("joist", (B["x1"] - B["x0"], 0.12, 0.08), ((B["x0"] + B["x1"]) / 2, B["y0"] + 0.5 + k * 0.95, B["z1"] - 0.05), concrete, coll=coll)
    # basement batten light (cool), dim
    bat = U.box("batten", (1.2, 0.1, 0.05), (0.6, B["y0"] + 2.4, B["z1"] - 0.12), M("batten", lambda: U.emission("batten tube", U.kelvin(5200), 3.0)), coll=coll)
    batl = U.area_light("batten light", (0.6, B["y0"] + 2.4, B["z1"] - 0.18), 1.2, 60, U.kelvin(5200), target=(0.6, B["y0"] + 2.4, B["z0"]), size_y=0.12, coll=coll)
    U.ctrl_register("basement_light", batl.data, "energy", scale=110)
    bfill = U.area_light("basement fill", (-0.5, B["y0"] + 2.6, B["z1"] - 0.3), 2.5, 40, U.kelvin(4300), target=(-0.5, B["y0"] + 2.6, B["z0"]), size_y=2.0, coll=coll)
    U.ctrl_register("basement_light", bfill.data, "energy", scale=90)
    U.ctrl_register("basement_light", bat.data.materials[0].node_tree.nodes["Emission"].inputs["Strength"], scale=3.0)

    # server rack (water-cooled, against west wall), facing east (+x)
    rack_x, rack_y = B["x0"] + 0.45, B["y0"] + 2.9
    rack_m = M("rack", lambda: U.principled("rack steel", U.hex_rgb("#1d1f22"), rough=0.45, metal=0.7, noise=0.15))
    U.box("rack frame", (0.75, 0.68, 1.35), (rack_x, rack_y, B["z0"] + 0.675 + 0.05), rack_m, bevel=0.015, coll=coll)
    U.box("rack plinth", (0.78, 0.7, 0.06), (rack_x, rack_y, B["z0"] + 0.03), rack_m, coll=coll)
    server_objs = []
    for k in range(4):
        z = B["z0"] + 0.32 + k * 0.26
        s = U.box(f"server {k+1}", (0.7, 0.6, 0.2), (rack_x + 0.04, rack_y, z), M("srv", lambda: U.principled("server case", U.hex_rgb("#2a2d31"), rough=0.4, metal=0.6)), bevel=0.01, coll=coll)
        # front bezel honeycomb glow (servers_on), status LED (paused -> amber)
        bez = U.box(f"bezel {k+1}", (0.01, 0.52, 0.13), (rack_x + 0.395, rack_y - 0.02, z), _bezel_mat(k), coll=coll)
        led = U.box(f"led {k+1}", (0.012, 0.03, 0.02), (rack_x + 0.397, rack_y + 0.27, z + 0.04), _led_mat(k), coll=coll)
        server_objs.append((s, bez, led))
    # rack front light spill (cool white-blue when on)
    rl = U.area_light("rack glow", (rack_x + 0.55, rack_y, B["z0"] + 0.75), 1.0, 0, U.hex_rgb("#7fb2ff"), target=(rack_x + 3, rack_y, B["z0"] + 0.6), size_y=1.0, coll=coll)
    U.ctrl_register("servers_on", rl.data, "energy", scale=18)
    # small label plate on the rack
    # plate heat exchanger on the north wall + tank
    tank_x, tank_y = B["x0"] + 1.75, B["y1"] - 0.65
    hx = U.box("plate heat exchanger", (0.32, 0.16, 0.5), (B["x0"] + 0.75, B["y1"] - 0.12, B["z0"] + 1.35), M("hx", lambda: U.principled("stainless", U.hex_rgb("#b9bcbf"), rough=0.25, metal=1.0)), bevel=0.02, coll=coll)
    tank = U.cylinder("hot water tank", 0.46, 1.95, (tank_x, tank_y, B["z0"] + 0.975 + 0.05), M("tankjacket", lambda: U.principled("tank jacket", U.hex_rgb("#d8d6d0"), rough=0.55, noise=0.1)), verts=48, coll=coll)
    U.cylinder("tank cap", 0.47, 0.06, (tank_x, tank_y, B["z0"] + 2.0 + 0.03), M("tankcap", lambda: U.principled("tank cap", U.hex_rgb("#8e9195"), rough=0.4, metal=0.6)), verts=48, coll=coll)
    for zz in (B["z0"] + 0.25, B["z0"] + 1.9):
        U.cylinder("tank ring", 0.465, 0.05, (tank_x, tank_y, zz), M("tankcap", lambda: None), verts=48, coll=coll)
    tank_glow = _tank_glow(coll, (tank_x, tank_y, B["z0"] + 0.06), 0.475, 1.92)
    # level gauge strip on the tank (thin vertical bar, readable)
    # backup gas boiler (wall-hung, on the north wall), honest: still there for the gap
    U.box("backup boiler", (0.45, 0.35, 0.72), (B["x1"] - 1.2, B["y1"] - 0.2, B["z0"] + 1.5), white, bevel=0.03, coll=coll)
    U.box("boiler display", (0.12, 0.01, 0.05), (B["x1"] - 1.2, B["y1"] - 0.38, B["z0"] + 1.62), M("boilerdisp", lambda: U.emission("boiler display", U.hex_rgb("#4ad0ff"), 1.5)), coll=coll)
    # pipes: rack -> heat exchanger -> tank -> riser to living room radiator
    pipe = M("pipe", lambda: flow_material("pipe flow", "#b87333"))
    ins = M("ins", lambda: flow_material("pipe insulation", "#6d6f72", metal=0.0, rough=0.85))
    pts = [(rack_x + 0.1, rack_y + 0.36, B["z0"] + 1.2), (rack_x + 0.1, rack_y + 0.36, B["z0"] + 1.6), (rack_x + 0.1, B["y1"] - 0.15, B["z0"] + 1.6), (B["x0"] + 0.62, B["y1"] - 0.15, B["z0"] + 1.6), (B["x0"] + 0.62, B["y1"] - 0.15, B["z0"] + 1.45)]
    _pipe("pipe rack-hx", pts, 0.028, ins, coll)
    pts2 = [(B["x0"] + 0.88, B["y1"] - 0.15, B["z0"] + 1.2), (B["x0"] + 0.88, B["y1"] - 0.15, B["z0"] + 0.9), (tank_x - 0.5, B["y1"] - 0.15, B["z0"] + 0.9), (tank_x - 0.5, tank_y, B["z0"] + 0.9), (tank_x - 0.44, tank_y, B["z0"] + 0.9)]
    _pipe("pipe hx-tank", pts2, 0.022, pipe, coll)
    # riser from tank top to the radiator above (through slab), up the facade wall
    rz = L["z0"] + 0.3
    pts3 = [(tank_x, tank_y, B["z0"] + 2.05), (tank_x, tank_y, B["z1"] - 0.25), (tank_x, FY + 0.17, B["z1"] - 0.25), (rad_x - 0.55, FY + 0.17, B["z1"] - 0.25), (rad_x - 0.55, FY + 0.17, rz), (rad_x - 0.45, FY + 0.17, rz)]
    _pipe("riser", pts3, 0.02, pipe, coll)
    pts4 = [(rad_x + 0.45, FY + 0.17, rz), (rad_x + 0.6, FY + 0.17, rz), (rad_x + 0.6, FY + 0.17, B["z1"] - 0.35), (tank_x + 0.25, FY + 0.17, B["z1"] - 0.35), (tank_x + 0.25, tank_y - 0.3, B["z1"] - 0.35), (tank_x + 0.25, tank_y - 0.3, B["z0"] + 0.4)]
    _pipe("return", pts4, 0.018, ins, coll)
    # basement clutter: shelf with jars, boxes, old skis, bicycle-ish frame
    _basement_clutter(coll, B, r, M)
    # moonlight through the basement windows
    for wx in (-2.4, 0.0):
        U.area_light("basement window light", (wx, S.FACADE_Y - 0.6, 0.55), 0.9, 6, U.kelvin(9000), target=(wx, FY + 3, B["z0"]), size_y=0.55, coll=coll)
    return dict(floor=floor, ceil=ceil, slab=slab, walls=walls, servers=server_objs, tank=tank, rack=(rack_x, rack_y), tank_pos=(tank_x, tank_y), rad_x=rad_x)


# ----------------------------------------------------------------------------------------------
def _pipe(name, pts, radius, mat, coll):
    o = U.tube_path(name, pts, radius, mat, coll, res=8)
    return o


def _parquet():
    m = bpy.data.materials.new("parquet")
    nt, n, l = U._nodes(m)
    p = n["Principled BSDF"]
    tc = n.new("ShaderNodeTexCoord")
    br = n.new("ShaderNodeTexBrick")
    br.offset = 0.5
    br.inputs["Scale"].default_value = 1.0
    br.inputs["Brick Width"].default_value = 0.6
    br.inputs["Row Height"].default_value = 0.09
    br.inputs["Mortar Size"].default_value = 0.002
    br.inputs["Color1"].default_value = U.hex_rgb("#7a5233")
    br.inputs["Color2"].default_value = U.hex_rgb("#5e3d25")
    br.inputs["Mortar"].default_value = U.hex_rgb("#2a1a10")
    l.new(tc.outputs["Object"], br.inputs["Vector"])
    nz = n.new("ShaderNodeTexWave")
    nz.inputs["Scale"].default_value = 30
    nz.inputs["Distortion"].default_value = 6
    l.new(tc.outputs["Object"], nz.inputs["Vector"])
    mx = n.new("ShaderNodeMix")
    mx.data_type = "RGBA"
    mx.blend_type = "MULTIPLY"
    mx.inputs["Factor"].default_value = 0.25
    l.new(br.outputs["Color"], mx.inputs[6])
    l.new(nz.outputs["Color"], mx.inputs[7])
    l.new(mx.outputs[2], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.35
    return m


def _frost():
    m = bpy.data.materials.new("frost")
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    tc = n.new("ShaderNodeTexCoord")
    nz = n.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 18
    nz.inputs["Detail"].default_value = 12
    nz.inputs["Roughness"].default_value = 0.7
    l.new(tc.outputs["Object"], nz.inputs["Vector"])
    # frost grows from the edges: distance from centre in local space
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Generated"], sep.inputs[0])
    edge = n.new("ShaderNodeMath")
    edge.operation = "ABSOLUTE"
    sub = n.new("ShaderNodeMath")
    sub.operation = "SUBTRACT"
    sub.inputs[1].default_value = 0.5
    l.new(sep.outputs["Z"], sub.inputs[0])
    l.new(sub.outputs[0], edge.inputs[0])
    sub2 = n.new("ShaderNodeMath")
    sub2.operation = "SUBTRACT"
    sub2.inputs[1].default_value = 0.5
    l.new(sep.outputs["X"], sub2.inputs[0])
    e2 = n.new("ShaderNodeMath")
    e2.operation = "ABSOLUTE"
    l.new(sub2.outputs[0], e2.inputs[0])
    mxe = n.new("ShaderNodeMath")
    mxe.operation = "MAXIMUM"
    l.new(edge.outputs[0], mxe.inputs[0])
    l.new(e2.outputs[0], mxe.inputs[1])
    add = n.new("ShaderNodeMath")
    add.operation = "ADD"
    l.new(mxe.outputs[0], add.inputs[0])
    l.new(nz.outputs["Fac"], add.inputs[1])
    thr = n.new("ShaderNodeMath")
    thr.operation = "SUBTRACT"
    l.new(add.outputs[0], thr.inputs[0])
    thr.inputs[1].default_value = 1.2
    U.ctrl_register("frost", thr.inputs[1], scale=-0.55, offset=1.25)  # frost 1 -> 0.7 (lots), 0 -> 1.25 (none)
    mr = n.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = 0.0
    mr.inputs["From Max"].default_value = 0.15
    l.new(thr.outputs[0], mr.inputs["Value"])
    d = n.new("ShaderNodeBsdfDiffuse")
    d.inputs["Color"].default_value = (0.75, 0.82, 0.9, 1)
    t = n.new("ShaderNodeBsdfTransparent")
    mx = n.new("ShaderNodeMixShader")
    l.new(mr.outputs["Result"], mx.inputs["Fac"])
    l.new(t.outputs[0], mx.inputs[1])
    l.new(d.outputs[0], mx.inputs[2])
    l.new(mx.outputs[0], n["Material Output"].inputs["Surface"])
    return m


def _glow_card(name, loc, size, coll):
    m = bpy.data.materials.new(name)
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    tc = n.new("ShaderNodeTexCoord")
    gr = n.new("ShaderNodeTexGradient")
    gr.gradient_type = "SPHERICAL"
    mp = n.new("ShaderNodeMapping")
    mp.inputs["Location"].default_value = (-0.5, -0.5, -0.5)
    mp.inputs["Scale"].default_value = (2, 2, 2)
    l.new(tc.outputs["Generated"], mp.inputs["Vector"])
    # Generated on a flat box: use x,z
    l.new(mp.outputs["Vector"], gr.inputs["Vector"])
    e = n.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = WARM
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    l.new(gr.outputs["Fac"], mul.inputs[0])
    mul.inputs[1].default_value = 0
    U.ctrl_register("radiator", mul.inputs[1], scale=1.0)
    t = n.new("ShaderNodeBsdfTransparent")
    add = n.new("ShaderNodeAddShader")
    em_s = n.new("ShaderNodeMath")
    em_s.operation = "MULTIPLY"
    em_s.inputs[1].default_value = 1.2
    l.new(mul.outputs[0], em_s.inputs[0])
    l.new(em_s.outputs[0], e.inputs["Strength"])
    l.new(t.outputs[0], add.inputs[0])
    l.new(e.outputs[0], add.inputs[1])
    l.new(add.outputs[0], n["Material Output"].inputs["Surface"])
    m.blend_method = "BLEND"
    o = U.box(name, (size[0], 0.01, size[1]), loc, m, coll=coll)
    o.visible_shadow = False
    o.visible_diffuse = False
    o.visible_glossy = False
    return o


def _tank_glow(coll, base, r, h):
    """X-ray shell: warm glow filling the tank from the bottom up to `tank_level`."""
    m = bpy.data.materials.new("tank glow")
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    tc = n.new("ShaderNodeTexCoord")
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Generated"], sep.inputs[0])
    lt = n.new("ShaderNodeMath")
    lt.operation = "SUBTRACT"
    lt.inputs[0].default_value = 0.0
    U.ctrl_register("tank_level", lt.inputs[0], scale=1.0)
    l.new(sep.outputs["Z"], lt.inputs[1])  # level - z
    st = n.new("ShaderNodeMapRange")
    st.inputs["From Min"].default_value = -0.01
    st.inputs["From Max"].default_value = 0.02
    l.new(lt.outputs[0], st.inputs["Value"])
    # fresnel-ish rim so it reads as a volume inside the jacket
    lw = n.new("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.6
    rim = n.new("ShaderNodeMath")
    rim.operation = "MULTIPLY_ADD"
    l.new(lw.outputs["Facing"], rim.inputs[0])
    rim.inputs[1].default_value = 0.6
    rim.inputs[2].default_value = 0.35
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    l.new(st.outputs["Result"], mul.inputs[0])
    l.new(rim.outputs[0], mul.inputs[1])
    gain = n.new("ShaderNodeMath")
    gain.operation = "MULTIPLY"
    l.new(mul.outputs[0], gain.inputs[0])
    gain.inputs[1].default_value = 0
    U.ctrl_register("tank_glow", gain.inputs[1], scale=3.0)
    e = n.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = WARM
    l.new(gain.outputs[0], e.inputs["Strength"])
    t = n.new("ShaderNodeBsdfTransparent")
    mx = n.new("ShaderNodeMixShader")
    cl = n.new("ShaderNodeMath")
    cl.operation = "MULTIPLY"
    cl.use_clamp = True
    l.new(mul.outputs[0], cl.inputs[0])
    cl.inputs[1].default_value = 0
    U.ctrl_register("tank_glow", cl.inputs[1], scale=0.55)
    l.new(cl.outputs[0], mx.inputs["Fac"])
    l.new(t.outputs[0], mx.inputs[1])
    l.new(e.outputs[0], mx.inputs[2])
    l.new(mx.outputs[0], n["Material Output"].inputs["Surface"])
    m.blend_method = "BLEND"
    o = U.cylinder("tank glow", r, h, (base[0], base[1], base[2] + h / 2), m, verts=48, coll=coll)
    o.visible_shadow = False
    return o


def _bezel_mat(k):
    m = bpy.data.materials.new(f"bezel {k}")
    nt, n, l = U._nodes(m)
    p = n["Principled BSDF"]
    p.inputs["Base Color"].default_value = U.hex_rgb("#0e0f10")
    p.inputs["Roughness"].default_value = 0.5
    tc = n.new("ShaderNodeTexCoord")
    v = n.new("ShaderNodeTexVoronoi")
    v.inputs["Scale"].default_value = 180
    l.new(tc.outputs["Object"], v.inputs["Vector"])
    mr = n.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = 0.0
    mr.inputs["From Max"].default_value = 0.35
    mr.inputs["To Min"].default_value = 1.0
    mr.inputs["To Max"].default_value = 0.0
    l.new(v.outputs["Distance"], mr.inputs["Value"])
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    l.new(mr.outputs["Result"], mul.inputs[0])
    mul.inputs[1].default_value = 0
    U.ctrl_register(f"server_{k}", mul.inputs[1], scale=7.0)
    p.inputs["Emission Color"].default_value = U.hex_rgb("#9cc6ff")
    l.new(mul.outputs[0], p.inputs["Emission Strength"])
    return m


def _led_mat(k):
    m = bpy.data.materials.new(f"led {k}")
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    e = n.new("ShaderNodeEmission")
    e.inputs["Strength"].default_value = 0
    U.ctrl_register(f"server_{k}", e.inputs["Strength"], scale=12)
    e.inputs["Color"].default_value = U.hex_rgb("#5cf2a0")
    U.ctrl_register("paused_color", e.inputs["Color"], index=0, scale=0.9, offset=0.1)
    U.ctrl_register("paused_color", e.inputs["Color"], index=1, scale=-0.35, offset=0.88)
    U.ctrl_register("paused_color", e.inputs["Color"], index=2, scale=-0.32, offset=0.35)
    l.new(e.outputs[0], n["Material Output"].inputs["Surface"])
    return m


def _armchair(coll, loc, rot, M):
    fabric = M("fabric", lambda: U.principled("armchair fabric", U.hex_rgb("#4d5f6a"), rough=0.95, noise=0.4, noise_scale=60, bump=0.3))
    wood = M("wood", lambda: U.principled("dark wood", U.hex_rgb("#3b2618"), rough=0.4, noise=0.4, noise_scale=25))
    x, y, z = loc
    e = bpy.data.objects.new("armchair", None)
    e.location = loc
    e.rotation_euler = (0, 0, rot)
    U.link(e, coll)
    parts = [((0.85, 0.8, 0.22), (0, 0, 0.42)), ((0.85, 0.22, 0.7), (0, 0.32, 0.82)), ((0.18, 0.8, 0.32), (0.4, 0, 0.62)), ((0.18, 0.8, 0.32), (-0.4, 0, 0.62)), ((0.75, 0.6, 0.12), (0, -0.05, 0.56))]
    for size, off in parts:
        b = U.box("armchair part", size, off, fabric, bevel=0.07, coll=coll)
        b.parent = e
    for dx in (-0.36, 0.36):
        for dy in (-0.33, 0.33):
            leg = U.box("chair leg", (0.05, 0.05, 0.32), (dx, dy, 0.16), wood, coll=coll)
            leg.parent = e
    return e


def _side_table_lamp(coll, loc, M):
    wood = M("wood", lambda: None)
    x, y, z = loc
    U.cylinder("side table top", 0.28, 0.04, (x, y, z + 0.58), wood, verts=32, coll=coll)
    U.cylinder("side table leg", 0.03, 0.56, (x, y, z + 0.29), wood, verts=12, coll=coll)
    brass = M("brass", lambda: U.principled("brass", U.hex_rgb("#a88a4a"), rough=0.3, metal=1.0))
    U.cylinder("lamp base", 0.08, 0.03, (x + 0.05, y + 0.05, z + 0.615), brass, verts=24, coll=coll)
    U.cylinder("lamp stem", 0.012, 0.35, (x + 0.05, y + 0.05, z + 0.8), brass, verts=8, coll=coll)
    shade_m = bpy.data.materials.new("lamp shade")
    nt, n, l = U._nodes(shade_m)
    p = n["Principled BSDF"]
    p.inputs["Base Color"].default_value = U.hex_rgb("#e8dcc2")
    p.inputs["Roughness"].default_value = 0.9
    p.inputs["Emission Color"].default_value = U.kelvin(2400)
    U.ctrl_register("lamp", p.inputs["Emission Strength"], scale=3.0)
    sh = U.cylinder("lamp shade", 0.17, 0.2, (x + 0.05, y + 0.05, z + 1.02), shade_m, verts=32, coll=coll, r2=0.11, cap=False)
    pl = U.point_light("reading lamp", (x + 0.05, y + 0.05, z + 0.98), 0, U.kelvin(2400), 0.06, coll=coll)
    U.ctrl_register("lamp", pl.data, "energy", scale=55)
    _turtle(coll, (x + 0.13, y - 0.13, z + 0.6), math.radians(150))
    # tea cup
    cup = M("cup", lambda: U.principled("cup", U.hex_rgb("#f1ece2"), rough=0.2))
    U.cylinder("cup", 0.045, 0.08, (x - 0.12, y - 0.06, z + 0.64), cup, verts=20, coll=coll)


def _turtle(coll, loc, rot_z):
    """A small green turtle figurine on Bob's side table (about 9 cm long)."""
    shell_m = U.principled("turtle shell", U.hex_rgb("#3f7a3a"), rough=0.35, noise=0.5, noise_scale=60, bump=0.4, coat=0.6)
    skin_m = U.principled("turtle skin", U.hex_rgb("#7fae5a"), rough=0.5)
    eye_m = U.principled("turtle eye", U.hex_rgb("#111111"), rough=0.1)
    root = bpy.data.objects.new("turtle", None)
    root.location = loc
    root.rotation_euler = (0, 0, rot_z)
    root.scale = (1.45, 1.45, 1.45)
    U.link(root, coll)
    parts = [
        U.sphere("turtle shell", 0.04, (0, 0, 0.018), shell_m, seg=24, coll=coll, scale=(1.0, 1.2, 0.62)),
        U.sphere("turtle belly", 0.038, (0, 0, 0.012), skin_m, seg=16, coll=coll, scale=(1.0, 1.15, 0.25)),
        U.sphere("turtle head", 0.016, (0, -0.058, 0.022), skin_m, seg=16, coll=coll, scale=(1.0, 1.25, 0.95)),
        U.sphere("turtle tail", 0.006, (0, 0.05, 0.01), skin_m, seg=8, coll=coll, scale=(1, 1.8, 1)),
    ]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(U.sphere("turtle leg", 0.011, (sx * 0.033, sy * 0.032, 0.007), skin_m, seg=10, coll=coll, scale=(1.3, 1.0, 0.6)))
        parts.append(U.sphere("turtle eye", 0.0035, (sx * 0.008, -0.07, 0.028), eye_m, seg=8, coll=coll))
    for p in parts:
        p.parent = root
    return root


def _bookshelf(coll, loc, r, M):
    wood = M("shelfwood", lambda: U.principled("shelf wood", U.hex_rgb("#6b4a30"), rough=0.5, noise=0.3, noise_scale=20))
    x, y, z = loc
    U.box("shelf side", (0.35, 0.03, 2.1), (x + 0.175, y - 0.6, z + 1.05), wood, coll=coll)
    U.box("shelf side", (0.35, 0.03, 2.1), (x + 0.175, y + 0.6, z + 1.05), wood, coll=coll)
    book_cols = ["#7b2f2a", "#2d4a5e", "#c9b27c", "#3e5a3a", "#5a4a6e", "#a36b2c", "#e0d8c8", "#1f2a36"]
    for k in range(5):
        zz = z + 0.05 + k * 0.42
        U.box("shelf board", (0.35, 1.2, 0.025), (x + 0.175, y, zz), wood, coll=coll)
        yy = y - 0.57
        while yy < y + 0.55:
            w = r.uniform(0.025, 0.06)
            hgt = r.uniform(0.22, 0.34)
            col = book_cols[r.randrange(len(book_cols))]
            U.box("book", (r.uniform(0.18, 0.26), w, hgt), (x + 0.15, yy + w / 2, zz + 0.0125 + hgt / 2), M(("book", col), lambda: U.principled("book", U.hex_rgb(col), rough=0.7)), coll=coll)
            yy += w + 0.003


def _rug(coll, loc, M):
    rug = M("rug", lambda: U.principled("rug", U.hex_rgb("#7a3b2e"), rough=0.95, noise=0.6, noise_scale=12, bump=0.2))
    U.box("rug", (2.2, 1.6, 0.012), loc, rug, coll=coll)


def _picture(coll, loc, M):
    frame = M("pframe", lambda: U.principled("picture frame", U.hex_rgb("#2a2520"), rough=0.4))
    art = bpy.data.materials.new("picture")
    nt, n, l = U._nodes(art)
    p = n["Principled BSDF"]
    w = n.new("ShaderNodeTexWave")
    w.inputs["Scale"].default_value = 2.5
    w.inputs["Distortion"].default_value = 3
    cr = n.new("ShaderNodeValToRGB")
    cr.color_ramp.elements[0].color = U.hex_rgb("#2f4a5a")
    cr.color_ramp.elements[1].color = U.hex_rgb("#d8c9a0")
    l.new(w.outputs["Fac"], cr.inputs["Fac"])
    l.new(cr.outputs["Color"], p.inputs["Base Color"])
    U.box("picture frame", (0.9, 0.03, 0.65), loc, frame, coll=coll)
    U.box("picture", (0.8, 0.01, 0.55), (loc[0], loc[1] - 0.02, loc[2]), art, coll=coll)


def _plant(coll, loc, M):
    pot = M("pot", lambda: U.principled("pot", U.hex_rgb("#9a5a3a"), rough=0.8))
    leaf = M("leaf", lambda: U.principled("leaf", U.hex_rgb("#2f5a2a"), rough=0.6, noise=0.3))
    x, y, z = loc
    U.cylinder("pot", 0.16, 0.3, (x, y, z + 0.15), pot, verts=20, coll=coll)
    r = U.rng(3)
    for k in range(9):
        a = k * 0.7
        U.sphere("leaf", 0.12, (x + 0.12 * math.cos(a), y + 0.12 * math.sin(a), z + 0.45 + 0.05 * (k % 3)), leaf, seg=10, coll=coll, scale=(1.4, 0.5, 0.25))


def _basement_clutter(coll, B, r, M):
    wood = M("crate", lambda: U.principled("crate wood", U.hex_rgb("#8a6a45"), rough=0.8, noise=0.5, noise_scale=10))
    card = M("cardboard", lambda: U.principled("cardboard", U.hex_rgb("#a07d55"), rough=0.9, noise=0.3))
    steel = M("shelfsteel", lambda: U.principled("shelf steel", U.hex_rgb("#5a5e62"), rough=0.5, metal=0.7))
    # metal shelf on east wall
    sx = B["x1"] - 0.3
    for k in range(4):
        U.box("shelf", (0.45, 1.6, 0.03), (sx, B["y0"] + 2.2, B["z0"] + 0.3 + k * 0.5), steel, coll=coll)
    for dy in (-0.78, 0.78):
        U.box("shelf post", (0.04, 0.04, 2.0), (sx - 0.2, B["y0"] + 2.2 + dy, B["z0"] + 1.0), steel, coll=coll)
    jar = M("jar", lambda: U.principled("jar glass", U.hex_rgb("#c9a24a"), rough=0.1, transmission=0.6))
    for k in range(4):
        for j in range(r.randint(2, 5)):
            yy = B["y0"] + 1.6 + j * 0.28
            if r.random() < 0.5:
                U.box("box", (0.38, 0.26, r.uniform(0.18, 0.35)), (sx, yy, B["z0"] + 0.32 + k * 0.5 + 0.12), card, bevel=0.01, coll=coll)
            else:
                U.cylinder("jar", 0.05, 0.14, (sx - 0.05, yy, B["z0"] + 0.39 + k * 0.5), jar, verts=12, coll=coll)
    for k in range(3):
        U.box("crate", (0.5, 0.4, 0.3), (B["x1"] - 0.7, B["y1"] - 1.6 - k * 0.1, B["z0"] + 0.15 + k * 0.3), wood, bevel=0.01, coll=coll)
    # skis leaning on the wall
    ski = M("ski", lambda: U.principled("ski", U.hex_rgb("#b23a2a"), rough=0.3))
    for k in range(2):
        U.box("ski", (0.08, 0.02, 1.7), (B["x1"] - 0.25, B["y1"] - 0.5 - k * 0.12, B["z0"] + 0.85), ski, coll=coll, rot=(0.12, 0, 0))
