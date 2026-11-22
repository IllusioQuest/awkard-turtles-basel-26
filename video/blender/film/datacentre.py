"""A data centre a few hundred metres away: a long flat hall with rooftop dry coolers whose
heat rises into the cold night as soft warm plumes. Fictional building, not a real operator."""
from __future__ import annotations

import math

import bpy

from . import util as U
from . import street as S

DC_ORIGIN = (260.0, 140.0)  # local metres from Bob's house (illustrative)


def plume_material():
    """Soft, additive warm column: transparent with a vertical fade and drifting noise."""
    m = bpy.data.materials.new("heat plume")
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    tc = n.new("ShaderNodeTexCoord")
    mp = n.new("ShaderNodeMapping")
    l.new(tc.outputs["Object"], mp.inputs["Vector"])
    U.ctrl_register("plume_phase", mp.inputs["Location"], index=2, scale=-1.0)
    nz = n.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = 0.35
    nz.inputs["Detail"].default_value = 4
    nz.inputs["Distortion"].default_value = 1.5
    l.new(mp.outputs["Vector"], nz.inputs["Vector"])
    gen = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Generated"], gen.inputs[0])
    fade = n.new("ShaderNodeMapRange")  # bottom strong, top transparent
    fade.inputs["From Min"].default_value = 0.0
    fade.inputs["From Max"].default_value = 1.0
    fade.inputs["To Min"].default_value = 1.0
    fade.inputs["To Max"].default_value = 0.0
    l.new(gen.outputs["Z"], fade.inputs["Value"])
    lw = n.new("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.5
    soft = n.new("ShaderNodeMath")
    soft.operation = "POWER"
    soft.inputs[1].default_value = 2.0
    l.new(lw.outputs["Facing"], soft.inputs[0])
    inv = n.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    l.new(soft.outputs[0], inv.inputs[1])
    a = n.new("ShaderNodeMath")
    a.operation = "MULTIPLY"
    l.new(fade.outputs["Result"], a.inputs[0])
    l.new(inv.outputs[0], a.inputs[1])
    b = n.new("ShaderNodeMath")
    b.operation = "MULTIPLY"
    l.new(a.outputs[0], b.inputs[0])
    l.new(nz.outputs["Fac"], b.inputs[1])
    g = n.new("ShaderNodeMath")
    g.operation = "MULTIPLY"
    l.new(b.outputs[0], g.inputs[0])
    g.inputs[1].default_value = 3.0
    e = n.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = U.hex_rgb("#ff7a2a")
    l.new(g.outputs[0], e.inputs["Strength"])
    t = n.new("ShaderNodeBsdfTransparent")
    add = n.new("ShaderNodeAddShader")
    l.new(t.outputs[0], add.inputs[0])
    l.new(e.outputs[0], add.inputs[1])
    l.new(add.outputs[0], n["Material Output"].inputs["Surface"])
    U.blended(m)
    return m


def build(coll):
    ox, oy = DC_ORIGIN
    r = U.rng(5)
    wall = U.principled("dc cladding", U.hex_rgb("#5d6266"), rough=0.6, metal=0.3, noise=0.2, noise_scale=0.5)
    dark = U.principled("dc dark", U.hex_rgb("#1d2023"), rough=0.5, metal=0.5)
    roof = S.snow_mix_material("dc roof", "#3c3f42", rough=0.7, scale=0.8)
    fan = U.principled("fan grille", U.hex_rgb("#2a2d30"), rough=0.4, metal=0.8)
    # hall
    W, D, H = 90.0, 40.0, 14.0
    hall = U.box("data centre hall", (W, D, H), (ox, oy, H / 2), wall, coll=coll)
    U.box("dc roof", (W + 0.6, D + 0.6, 0.6), (ox, oy, H + 0.3), roof, coll=coll)
    # vertical cladding ribs on the street side
    ribs = []
    for k in range(int(W / 1.2)):
        ribs.append((ox - W / 2 + 0.6 + k * 1.2, oy - D / 2 - 0.08, 0.0, 0.12, 0.16, H, 0))
    U.boxes_mesh("dc ribs", ribs, dark, coll)
    # band of lit windows (security/office) and loading doors
    strip = U.emission("dc window strip", U.kelvin(4500), 2.5)
    U.box("dc window band", (W * 0.3, 0.05, 1.0), (ox - W * 0.25, oy - D / 2 - 0.2, 4.0), strip, coll=coll)
    for k in range(4):
        U.box("dc loading door", (4.0, 0.1, 5.0), (ox + 10 + k * 7, oy - D / 2 - 0.12, 2.5), dark, coll=coll)
    # aviation-red marker on the roof edge (subtle)
    U.sphere("dc marker", 0.25, (ox + W / 2 - 1, oy - D / 2 + 1, H + 1.0), U.emission("marker", (1, 0.05, 0.02), 30), seg=8, coll=coll)
    # rooftop dry coolers: 3 rows x 6 units with fans on top
    plume_m = plume_material()
    cool = []
    fans = []
    for row in range(3):
        for k in range(6):
            cx = ox - W / 2 + 12 + k * 13
            cy = oy - 10 + row * 10
            cool.append((cx, cy, H + 0.6, 10.0, 3.0, 2.4, 0))
            for f in range(4):
                fans.append((cx - 3.75 + f * 2.5, cy))
    U.boxes_mesh("dry coolers", cool, wall, coll)
    for (fx, fy) in fans:
        U.cylinder("fan", 1.05, 0.25, (fx, fy, H + 3.1), fan, verts=20, coll=coll)
    # warm plumes above the coolers (one per cooler, overlapping)
    plumes = []
    for row in range(3):
        for k in range(6):
            cx = ox - W / 2 + 12 + k * 13
            cy = oy - 10 + row * 10
            p = U.cylinder("heat plume", 5.5, 28.0, (cx + r.uniform(-1, 1), cy, H + 3.3 + 14.0), plume_m, verts=24, coll=coll, r2=8.5, cap=False)
            p.visible_shadow = False
            p.visible_diffuse = False
            p.visible_glossy = False
            plumes.append(p)
    # warm uplight on the plumes from the roof (soft)
    U.area_light("dc roof glow", (ox, oy, H + 4), 60.0, 2500, U.hex_rgb("#ff8a3a"), target=(ox, oy, H + 60), size_y=30, coll=coll)
    # fence and a few lamps
    for k in range(10):
        lx = ox - W / 2 + k * 10
        U.cylinder("dc lamp pole", 0.08, 8, (lx, oy - D / 2 - 14, 4), dark, verts=8, coll=coll)
        U.spot_light("dc lamp", (lx, oy - D / 2 - 13.6, 7.9), 1500, U.kelvin(4200), (lx, oy - D / 2 - 6, 0), angle=1.6, blend=0.8, radius=0.2, coll=coll)
    # ground around: snowy yard
    U.box("dc yard", (W + 80, D + 80, 0.1), (ox, oy, -0.05), S.snow_mix_material("dc yard", "#3a3a3c", scale=0.3), coll=coll)
    return dict(hall=hall, plumes=plumes, centre=(ox, oy, H))
