"""Wider Basel for the pull-out: procedural perimeter blocks with lit windows and street lights,
an approximate Rhine, and the map layer (canton boundaries, simulated sites, job flows).

The control `map` (0..1) turns the realistic night city into the flat dark map style of the
web dashboard (`web/src/admin`, CARTO dark-matter look), so the camera can rise out of Bob's
street and land on the map without a cut.
Geography: local metres around Bob's illustrative position in Gundeldingen (BOB_LATLON).
Canton outlines: prototypes/network-map-draft/data/switzerland-cantons.geojson (Natural Earth,
public domain). The Rhine course is hand-approximated (stylised, not survey data).
"""
from __future__ import annotations

import json
import math

import bpy

from . import util as U

BOB_LATLON = (47.5440, 7.5930)  # illustrative house position (Basel-Gundeldingen), scenario choice
M_PER_DEG_LAT = 110540.0
M_PER_DEG_LON = 111320.0 * math.cos(math.radians(BOB_LATLON[0]))
RHINE = [(47.5560, 7.7900), (47.5480, 7.7600), (47.5400, 7.7200), (47.5500, 7.6800), (47.5560, 7.6600),
         (47.5595, 7.6300), (47.5560, 7.6100), (47.5575, 7.5990), (47.5603, 7.5895), (47.5640, 7.5840),
         (47.5700, 7.5850), (47.5800, 7.5880), (47.5900, 7.5890), (47.6100, 7.5800), (47.6400, 7.5600),
         (47.6900, 7.5400)]
MAP_BG = "#111111"
MAP_LINE = "#4a4a46"
SERIES1 = "#3987e5"  # web dashboard --series-1 (load)
HEAT = "#ff8a2a"


def xy(lat, lon):
    return ((lon - BOB_LATLON[1]) * M_PER_DEG_LON, (lat - BOB_LATLON[0]) * M_PER_DEG_LAT)


def _map_mix(mat, flat_hex, strength=1.0):
    """Insert a mix: realistic surface -> flat emissive map colour, driven by control `map`."""
    nt = mat.node_tree
    n, l = nt.nodes, nt.links
    out = n["Material Output"]
    src = out.inputs["Surface"].links[0].from_socket
    e = n.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = U.hex_rgb(flat_hex)
    e.inputs["Strength"].default_value = strength
    mx = n.new("ShaderNodeMixShader")
    mx.inputs["Fac"].default_value = 0.0
    U.ctrl_register("map", mx.inputs["Fac"])
    l.new(src, mx.inputs[1])
    l.new(e.outputs[0], mx.inputs[2])
    l.new(mx.outputs[0], out.inputs["Surface"])
    return mat


def building_material():
    """Aerial night facades: window grid on walls with random lit windows; dark roofs."""
    m = bpy.data.materials.new("city buildings")
    nt, n, l = U._nodes(m)
    p = n["Principled BSDF"]
    p.inputs["Base Color"].default_value = U.hex_rgb("#5b544c")
    p.inputs["Roughness"].default_value = 0.85
    tc = n.new("ShaderNodeTexCoord")
    geo = n.new("ShaderNodeNewGeometry")
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(geo.outputs["Normal"], sep.inputs[0])
    absz = n.new("ShaderNodeMath")
    absz.operation = "ABSOLUTE"
    l.new(sep.outputs["Z"], absz.inputs[0])
    wall = n.new("ShaderNodeMath")
    wall.operation = "LESS_THAN"
    wall.inputs[1].default_value = 0.5
    l.new(absz.outputs[0], wall.inputs[0])
    # window cells: brick texture in world space (3.2 m storeys, 3 m axes)
    br = n.new("ShaderNodeTexBrick")
    br.offset = 0.0
    br.inputs["Scale"].default_value = 1.0
    br.inputs["Brick Width"].default_value = 3.0
    br.inputs["Row Height"].default_value = 3.2
    br.inputs["Mortar Size"].default_value = 0.9
    br.inputs["Color1"].default_value = (1, 1, 1, 1)
    br.inputs["Color2"].default_value = (0.2, 0.2, 0.2, 1)
    br.inputs["Mortar"].default_value = (0, 0, 0, 1)
    br.inputs["Mortar Smooth"].default_value = 0.0
    # use x+y along the wall so both orientations get windows
    add = n.new("ShaderNodeVectorMath")
    add.operation = "MULTIPLY"
    add.inputs[1].default_value = (1, 1, 1)
    l.new(tc.outputs["Object"], add.inputs[0])
    sx = n.new("ShaderNodeSeparateXYZ")
    l.new(add.outputs[0], sx.inputs[0])
    s = n.new("ShaderNodeMath")
    s.operation = "ADD"
    l.new(sx.outputs["X"], s.inputs[0])
    l.new(sx.outputs["Y"], s.inputs[1])
    cmb = n.new("ShaderNodeCombineXYZ")
    l.new(s.outputs[0], cmb.inputs["X"])
    l.new(sx.outputs["Z"], cmb.inputs["Y"])
    l.new(cmb.outputs[0], br.inputs["Vector"])
    win = n.new("ShaderNodeMath")
    win.operation = "LESS_THAN"
    win.inputs[1].default_value = 0.5  # mortar (0) = window opening
    l.new(br.outputs["Fac"], win.inputs[0])
    # random lit: white noise per cell
    wn = n.new("ShaderNodeTexWhiteNoise")
    wn.noise_dimensions = "3D"
    fl = n.new("ShaderNodeVectorMath")
    fl.operation = "SNAP"
    fl.inputs[1].default_value = (3.0, 3.0, 3.2)
    l.new(tc.outputs["Object"], fl.inputs[0])
    l.new(fl.outputs[0], wn.inputs["Vector"])
    lit = n.new("ShaderNodeMath")
    lit.operation = "GREATER_THAN"
    lit.inputs[1].default_value = 0.55
    l.new(wn.outputs["Value"], lit.inputs[0])
    a = n.new("ShaderNodeMath")
    a.operation = "MULTIPLY"
    l.new(win.outputs[0], a.inputs[0])
    l.new(lit.outputs[0], a.inputs[1])
    b = n.new("ShaderNodeMath")
    b.operation = "MULTIPLY"
    l.new(a.outputs[0], b.inputs[0])
    l.new(wall.outputs[0], b.inputs[1])
    st = n.new("ShaderNodeMath")
    st.operation = "MULTIPLY"
    l.new(b.outputs[0], st.inputs[0])
    st.inputs[1].default_value = 0
    U.ctrl_register("night_windows", st.inputs[1], scale=2.2)
    p.inputs["Emission Color"].default_value = U.kelvin(2700)
    l.new(st.outputs[0], p.inputs["Emission Strength"])
    # roofs darker (tiles)
    rc = n.new("ShaderNodeMix")
    rc.data_type = "RGBA"
    l.new(wall.outputs[0], rc.inputs["Factor"])
    rc.inputs[6].default_value = U.hex_rgb("#4a2e26")
    rc.inputs[7].default_value = U.hex_rgb("#6b645a")
    l.new(rc.outputs[2], p.inputs["Base Color"])
    return _map_mix(m, "#161615")


def build_city(coll, radius=2600, exclude=None, seed=21):
    """Perimeter blocks on slightly rotated grids, skipping the river and the hero street."""
    r = U.rng(seed)
    rh = [xy(*p) for p in RHINE]

    def river_dist(x, y):
        best = 1e9
        for (ax, ay), (bx, by) in zip(rh, rh[1:]):
            dx, dy = bx - ax, by - ay
            t = max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)))
            best = min(best, math.hypot(x - ax - t * dx, y - ay - t * dy))
        return best

    items, lights = [], []
    bw, bd, street_w = 78.0, 62.0, 16.0
    for gi in range(-40, 41):
        for gj in range(-50, 51):
            # districts: grid rotation changes every ~700 m
            ang = math.radians(8 * math.sin(gi * 0.21) + 6 * math.cos(gj * 0.17))
            cx = gi * (bw + street_w) + 0.0
            cy = gj * (bd + street_w) * 0.75
            if math.hypot(cx, cy) > radius:
                continue
            if exclude and any(x0 < cx < x1 and y0 < cy < y1 for (x0, y0, x1, y1) in exclude):
                continue
            if river_dist(cx, cy) < 150:
                continue
            c, s = math.cos(ang), math.sin(ang)
            hgt = r.choice([11, 13, 15, 15, 17, 19]) * (0.75 if math.hypot(cx, cy) > 1800 else 1.0)
            if r.random() < 0.06:
                continue  # park / square
            depth = 11.0
            for (ox, oy, sx, sy) in ((0, -(bd - depth) / 2, bw, depth), (0, (bd - depth) / 2, bw, depth),
                                     (-(bw - depth) / 2, 0, depth, bd - 2 * depth), ((bw - depth) / 2, 0, depth, bd - 2 * depth)):
                px, py = cx + c * ox - s * oy, cy + s * ox + c * oy
                items.append((px, py, 0.0, sx - 0.5, sy - 0.5, hgt + r.choice([0, 0, 1.5, 3]), ang))
            # street light dots at block corners
            for (lx, ly) in ((-bw / 2 - 4, -bd / 2 - 4), (bw / 2 + 4, -bd / 2 - 4), (0, -bd / 2 - 4)):
                lights.append((cx + c * lx - s * ly, cy + s * lx + c * ly, 6.0, 1.6, 1.6, 0.6, 0))
    city = U.boxes_mesh("city blocks", items, building_material(), coll)
    lamp = _map_mix(U.emission("city street lights", U.kelvin(2900), 40.0), MAP_BG, 1.0)
    U.boxes_mesh("city street lights", lights, lamp, coll)
    return city


def ground(coll, size=120000.0):
    g = U.principled("night ground", U.hex_rgb("#1a1c1f"), rough=0.9, noise=0.3, noise_scale=0.001)
    _map_mix(g, MAP_BG)
    o = U.box("ground", (size, size, 1.0), (0, 0, -2.2), g, coll=coll)
    # local ground at street level around the hero street, with a hole for Bob's basement
    hx0, hx1, hy0, hy1 = -3.9, 3.9, 8.5, 14.9
    for (x0, y0, x1, y1) in ((-400, -300, 400, hy0), (-400, hy1, 400, 300), (-400, hy0, hx0, hy1), (hx1, hy0, 400, hy1)):
        U.box("ground near", (x1 - x0, y1 - y0, 1.5), ((x0 + x1) / 2, (y0 + y1) / 2, -0.87), g, coll=coll)
    water = U.principled("rhine water", U.hex_rgb("#0b1016"), rough=0.08, metal=0.0)
    _map_mix(water, "#0f1a26")
    pts = [(*xy(*p), -0.05) for p in RHINE]
    rv = U.tube_path("rhine", pts, 95.0, water, coll, res=4)
    rv.scale = (1, 1, 0.002)
    return o


def map_layer(coll, region_sites, pool_sites):
    """Canton outlines, region houses, Dreispitz sites; all hidden until `map_layer` > 0."""
    root = U.ROOT if hasattr(U, "ROOT") else None
    gj = json.loads((U.HERE.parent.parent / "prototypes/network-map-draft/data/switzerland-cantons.geojson").read_text())
    line = U.emission("map line", U.hex_rgb(MAP_LINE), 0.8, transparent_mix=1.0)
    _fade_ctrl(line, "map_layer")
    for f in gj["features"]:
        if f["properties"].get("code") not in ("BS", "BL", "SO", "AG", "JU"):
            continue
        g = f["geometry"]
        polys = g["coordinates"] if g["type"] == "MultiPolygon" else [g["coordinates"]]
        for poly in polys:
            ring = poly[0]
            pts = [(*xy(lat, lon), 2.0) for lon, lat in ring]
            o = U.tube_path(f"canton {f['properties']['code']}", pts, 9.0, line, coll, res=2)
            o.scale = (1, 1, 0.05)
    return line


def _fade_ctrl(mat, name):
    """Materials built with emission(..., transparent_mix=1.0): key the mix towards visible."""
    mx = next(x for x in mat.node_tree.nodes if x.type == "MIX_SHADER")
    mx.inputs["Fac"].default_value = 0.0
    U.ctrl_register(name, mx.inputs["Fac"])


def site_marker(coll, name, x, y, radius, color_hex, ctrl, height=2.0):
    """Flat glowing disc + soft halo for one simulated site. `ctrl` names its brightness control."""
    core = U.emission(f"{name} core", U.hex_rgb(color_hex), 0.0)
    U.ctrl_register(ctrl, core.node_tree.nodes[core["emit"]].inputs["Strength"], scale=6.0)
    d = U.cylinder(name, radius, 1.0, (x, y, height), core, verts=32, coll=coll)
    halo_m = _halo_material(f"{name} halo", color_hex, ctrl)
    h = U.mesh_from(f"{name} halo", [(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [(0, 1, 2, 3)], halo_m, coll)
    h.location = (x, y, height - 0.2)
    h.scale = (radius * 5, radius * 5, 1)
    h.visible_shadow = False
    return d, h


def _halo_material(name, color_hex, ctrl):
    m = bpy.data.materials.new(name)
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    tc = n.new("ShaderNodeTexCoord")
    gr = n.new("ShaderNodeTexGradient")
    gr.gradient_type = "QUADRATIC_SPHERE"
    mp = n.new("ShaderNodeMapping")
    l.new(tc.outputs["Object"], mp.inputs["Vector"])
    l.new(mp.outputs["Vector"], gr.inputs["Vector"])
    e = n.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = U.hex_rgb(color_hex)
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    l.new(gr.outputs["Fac"], mul.inputs[0])
    mul.inputs[1].default_value = 0.0
    U.ctrl_register(ctrl, mul.inputs[1], scale=1.6)
    l.new(mul.outputs[0], e.inputs["Strength"])
    t = n.new("ShaderNodeBsdfTransparent")
    add = n.new("ShaderNodeAddShader")
    l.new(t.outputs[0], add.inputs[0])
    l.new(e.outputs[0], add.inputs[1])
    l.new(add.outputs[0], n["Material Output"].inputs["Surface"])
    U.blended(m)
    return m


def flow_arc(coll, name, a, b, height, ctrl, color_hex=SERIES1, radius=6.0):
    """A soft arc from a to b with dashes running towards b; brightness = control `ctrl`."""
    pts = []
    for i in range(33):
        t = i / 32
        x = a[0] + (b[0] - a[0]) * t
        y = a[1] + (b[1] - a[1]) * t
        z = 3 + height * 4 * t * (1 - t)
        pts.append((x, y, z))
    m = bpy.data.materials.new(name)
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    tc = n.new("ShaderNodeTexCoord")
    mp = n.new("ShaderNodeMapping")
    l.new(tc.outputs["UV"], mp.inputs["Vector"])
    U.ctrl_register("flow_dash", mp.inputs["Location"], index=0, scale=-1.0)
    w = n.new("ShaderNodeTexWave")
    w.wave_type = "BANDS"
    w.bands_direction = "X"
    w.wave_profile = "SAW"
    w.inputs["Scale"].default_value = 5.0
    l.new(mp.outputs["Vector"], w.inputs["Vector"])
    pw = n.new("ShaderNodeMath")
    pw.operation = "POWER"
    pw.inputs[1].default_value = 4.0
    l.new(w.outputs["Fac"], pw.inputs[0])
    base = n.new("ShaderNodeMath")
    base.operation = "ADD"
    base.inputs[1].default_value = 0.12
    l.new(pw.outputs[0], base.inputs[0])
    mul = n.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    l.new(base.outputs[0], mul.inputs[0])
    mul.inputs[1].default_value = 0
    U.ctrl_register(ctrl, mul.inputs[1], scale=8.0)
    e = n.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = U.hex_rgb(color_hex)
    l.new(mul.outputs[0], e.inputs["Strength"])
    t = n.new("ShaderNodeBsdfTransparent")
    mx = n.new("ShaderNodeMixShader")
    cl = n.new("ShaderNodeMath")
    cl.operation = "MULTIPLY"
    cl.use_clamp = True
    l.new(mul.outputs[0], cl.inputs[0])
    cl.inputs[1].default_value = 0.5
    l.new(cl.outputs[0], mx.inputs["Fac"])
    l.new(t.outputs[0], mx.inputs[1])
    l.new(e.outputs[0], mx.inputs[2])
    l.new(mx.outputs[0], n["Material Output"].inputs["Surface"])
    U.blended(m)
    o = U.tube_path(name, pts, radius, m, coll, res=4)
    o.visible_shadow = False
    return o
