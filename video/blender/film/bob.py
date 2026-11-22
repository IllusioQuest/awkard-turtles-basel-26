"""Bob: a simple, friendly stylised figure (rounded shapes, dot eyes, knitted hat, scarf).

Built from primitives parented to pivot empties, so the film animates him with plain keyframes:
  root (position/turn), hips, torso (lean), head, arm_L/arm_R (shoulder), fore_L/fore_R (elbow),
  lid_L/lid_R (blinks), hat, scarf_tail. Plus props: envelope, letter, breath puffs.
"""
from __future__ import annotations

import math

import bpy

from . import util as U


def _empty(name, loc, parent=None, coll=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.05
    e.location = loc
    U.link(e, coll)
    if parent:
        e.parent = parent
    return e


def _child(o, parent):
    o.parent = parent
    return o


def build(coll, loc=(0, 0, 0), rot_z=0.0):
    skin = U.principled("bob skin", U.hex_rgb("#e9b48e"), rough=0.55, noise=0.08, noise_scale=6)
    nose_m = U.principled("bob nose", U.hex_rgb("#e9a084"), rough=0.5)
    sweater = U.principled("bob sweater", U.hex_rgb("#c9932f"), rough=0.95, noise=0.35, noise_scale=90, bump=0.6)
    trousers = U.principled("bob trousers", U.hex_rgb("#2f3540"), rough=0.85, noise=0.2, noise_scale=40)
    hat_m = U.principled("bob hat", U.hex_rgb("#3a6a8a"), rough=0.95, noise=0.4, noise_scale=120, bump=0.8)
    scarf_m = U.principled("bob scarf", U.hex_rgb("#b8412f"), rough=0.95, noise=0.4, noise_scale=120, bump=0.8)
    shoe_m = U.principled("bob shoes", U.hex_rgb("#3a2a20"), rough=0.5)
    eye_m = U.principled("bob eyes", U.hex_rgb("#1a1412"), rough=0.15)
    hl_m = U.emission("eye highlight", (1, 1, 1), 0.35)
    # the nose reddens with the cold (control "cold_nose" 0..1)
    nn = nose_m.node_tree.nodes["Principled BSDF"]
    U.ctrl_register("cold_nose", nn.inputs["Base Color"], index=0, scale=0.12, offset=U.hex_rgb("#e9a084")[0])
    U.ctrl_register("cold_nose", nn.inputs["Base Color"], index=1, scale=-0.12, offset=U.hex_rgb("#e9a084")[1])

    rig = {}
    root = _empty("bob root", loc, coll=coll)
    root.rotation_euler = (0, 0, rot_z)
    rig["root"] = root
    hips = _empty("bob hips", (0, 0, 0.78), root, coll)
    rig["hips"] = hips
    torso = _empty("bob torso", (0, 0, 0), hips, coll)
    rig["torso"] = torso
    body = U.sphere("bob body", 0.3, (0, 0, 0.36), sweater, seg=32, coll=coll, scale=(1.0, 0.82, 1.45))
    _child(body, torso)
    # sweater rib at the waist
    hem = U.sphere("bob hem", 0.29, (0, 0, 0.0), sweater, seg=24, coll=coll, scale=(1.02, 0.85, 0.35))
    _child(hem, torso)
    neck = _empty("bob neck", (0, 0, 0.78), torso, coll)
    head = _empty("bob head", (0, 0, 0.0), neck, coll)
    rig["head"] = head
    hd = U.sphere("bob head mesh", 0.25, (0, 0, 0.24), skin, seg=40, coll=coll, scale=(1.0, 0.96, 1.02))
    _child(hd, head)
    nose = U.sphere("bob nose", 0.052, (0, -0.245, 0.2), nose_m, seg=20, coll=coll, scale=(1.0, 0.95, 0.9))
    _child(nose, head)
    for side, x in (("L", 0.085), ("R", -0.085)):
        eye = U.sphere(f"bob eye {side}", 0.032, (x, -0.225, 0.3), eye_m, seg=16, coll=coll, scale=(1, 0.6, 1.25))
        _child(eye, head)
        hl = U.sphere(f"bob eye hl {side}", 0.0055, (x + 0.01, -0.243, 0.313), hl_m, seg=8, coll=coll)
        _child(hl, head)
        lid_p = _empty(f"bob lid {side}", (x, -0.2, 0.345), head, coll)
        lid = U.sphere(f"bob lid mesh {side}", 0.04, (0, -0.012, 0.0), skin, seg=16, coll=coll, scale=(1.0, 0.7, 0.55))
        _child(lid, lid_p)
        lid_p.scale = (1, 1, 0.0)  # open; blink scales z to 1 (and drops)
        rig[f"lid_{side}"] = lid_p
        brow = U.box(f"bob brow {side}", (0.07, 0.02, 0.016), (x, -0.215, 0.37), eye_m, bevel=0.007, coll=coll)
        _child(brow, head)
        rig[f"brow_{side}"] = brow
        ear = U.sphere(f"bob ear {side}", 0.05, (x * 2.85, -0.02, 0.22), skin, seg=12, coll=coll, scale=(0.5, 0.8, 1))
        _child(ear, head)
    # mouth: a small dark curved bar (smile); scale for "o" shape
    mouth = U.sphere("bob mouth", 0.04, (0, -0.228, 0.1), U.principled("mouth", U.hex_rgb("#5a2a22"), rough=0.6), seg=16, coll=coll, scale=(1.0, 0.35, 0.28))
    _child(mouth, head)
    rig["mouth"] = mouth
    # knitted hat with pompom
    hat = _empty("bob hat", (0, 0, 0.405), head, coll)
    rig["hat"] = hat
    h1 = U.sphere("bob hat dome", 0.262, (0, 0, -0.04), hat_m, seg=32, coll=coll, scale=(1, 1, 0.85))
    _child(h1, hat)
    rim = U.torus("bob hat rim", 0.25, 0.045, (0, 0, 0), hat_m, coll)
    _child(rim, hat)
    pom = U.sphere("bob pompom", 0.07, (0, 0, 0.2), hat_m, seg=16, coll=coll)
    _child(pom, hat)
    rig["pompom"] = pom
    # scarf
    sc = U.torus("bob scarf", 0.19, 0.06, (0, 0, 0.74), scarf_m, coll)
    sc.scale = (1, 0.9, 1)
    _child(sc, torso)
    tail = _empty("bob scarf tail", (0.09, -0.17, 0.7), torso, coll)
    t1 = U.box("bob scarf tail mesh", (0.11, 0.035, 0.36), (0, 0, -0.17), scarf_m, bevel=0.015, coll=coll)
    _child(t1, tail)
    rig["scarf"] = sc
    rig["scarf_tail"] = tail
    # arms
    for side, x in (("L", 0.31), ("R", -0.31)):
        sh = _empty(f"bob arm {side}", (x, 0, 0.6), torso, coll)
        up = U.sphere(f"bob upper arm {side}", 0.075, (0, 0, -0.16), sweater, seg=16, coll=coll, scale=(1, 1, 2.4))
        _child(up, sh)
        el = _empty(f"bob fore {side}", (0, 0, -0.32), sh, coll)
        fo = U.sphere(f"bob forearm {side}", 0.068, (0, 0, -0.14), sweater, seg=16, coll=coll, scale=(1, 1, 2.2))
        _child(fo, el)
        hand = _empty(f"bob hand {side}", (0, 0, -0.3), el, coll)
        mit = U.sphere(f"bob mitten {side}", 0.07, (0, 0, 0), skin, seg=16, coll=coll, scale=(0.95, 0.8, 1.05))
        _child(mit, hand)
        sh.rotation_euler = (0, math.copysign(0.12, x), 0)
        rig[f"arm_{side}"] = sh
        rig[f"fore_{side}"] = el
        rig[f"hand_{side}"] = hand
    # legs
    for side, x in (("L", 0.13), ("R", -0.13)):
        hp = _empty(f"bob leg {side}", (x, 0, 0.0), hips, coll)
        th = U.sphere(f"bob thigh {side}", 0.105, (0, 0, -0.2), trousers, seg=16, coll=coll, scale=(1, 1, 2.0))
        _child(th, hp)
        kn = _empty(f"bob knee {side}", (0, 0, -0.4), hp, coll)
        sh_ = U.sphere(f"bob shin {side}", 0.09, (0, 0, -0.17), trousers, seg=16, coll=coll, scale=(1, 1, 2.0))
        _child(sh_, kn)
        shoe = U.sphere(f"bob shoe {side}", 0.09, (0, -0.06, -0.36), shoe_m, seg=16, coll=coll, scale=(1.0, 1.6, 0.6))
        _child(shoe, kn)
        rig[f"leg_{side}"] = hp
        rig[f"knee_{side}"] = kn
    # props: envelope + letter, parented to the right hand
    paper = U.principled("envelope paper", U.hex_rgb("#efe9dc"), rough=0.8)
    env = U.box("envelope", (0.3, 0.006, 0.2), (0, -0.06, 0.02), paper, coll=coll)
    env.parent = rig["hand_R"]
    env.rotation_euler = (1.35, 0, 0.2)
    flap = _empty("envelope flap", (0, -0.054, 0.1), env, coll)
    fl = U.mesh_from("envelope flap mesh", [(-0.15, 0, 0), (0.15, 0, 0), (0, 0, -0.11)], [(0, 1, 2)], paper, coll)
    _child(fl, flap)
    letter_m = _letter_material()
    letter = U.box("letter", (0.27, 0.003, 0.34), (0, 0.0, -0.02), letter_m, coll=coll)
    letter.parent = env
    rig["envelope"] = env
    rig["flap"] = flap
    rig["letter"] = letter
    # breath puffs (cold): three soft spheres in front of the mouth, animated by the film
    puff_m = _breath_material()
    puffs = []
    for k in range(3):
        p = U.sphere(f"breath {k}", 0.05, (0, -0.3, 0.1), puff_m, seg=12, coll=coll)
        p.parent = head
        p.scale = (0.001, 0.001, 0.001)
        puffs.append(p)
    rig["puffs"] = puffs
    return rig


def _breath_material():
    """Soft breath cloud: mostly transparent, fading towards the edge."""
    m = bpy.data.materials.new("breath")
    nt, n, l = U._nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    lw = n.new("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.5
    p = n.new("ShaderNodeMath")
    p.operation = "POWER"
    p.inputs[1].default_value = 3.0
    l.new(lw.outputs["Facing"], p.inputs[0])
    inv = n.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    l.new(p.outputs[0], inv.inputs[1])
    a = n.new("ShaderNodeMath")
    a.operation = "MULTIPLY"
    a.inputs[1].default_value = 0.14
    l.new(inv.outputs[0], a.inputs[0])
    d = n.new("ShaderNodeBsdfDiffuse")
    d.inputs["Color"].default_value = (0.8, 0.85, 0.95, 1)
    t = n.new("ShaderNodeBsdfTransparent")
    mx = n.new("ShaderNodeMixShader")
    l.new(a.outputs[0], mx.inputs["Fac"])
    l.new(t.outputs[0], mx.inputs[1])
    l.new(d.outputs[0], mx.inputs[2])
    l.new(mx.outputs[0], n["Material Output"].inputs["Surface"])
    U.blended(m)
    return m


def _letter_material():
    """The heating bill: white paper with a header band and grey text lines (no real figures)."""
    m = bpy.data.materials.new("heating bill")
    nt, n, l = U._nodes(m)
    p = n["Principled BSDF"]
    tc = n.new("ShaderNodeTexCoord")
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Generated"], sep.inputs[0])
    lines = n.new("ShaderNodeTexWave")
    lines.wave_type = "BANDS"
    lines.bands_direction = "Z"
    lines.inputs["Scale"].default_value = 14
    l.new(tc.outputs["Generated"], lines.inputs["Vector"])
    thr = n.new("ShaderNodeMath")
    thr.operation = "GREATER_THAN"
    thr.inputs[1].default_value = 0.8
    l.new(lines.outputs["Fac"], thr.inputs[0])
    cr = n.new("ShaderNodeMix")
    cr.data_type = "RGBA"
    l.new(thr.outputs[0], cr.inputs["Factor"])
    cr.inputs[6].default_value = U.hex_rgb("#f6f3ec")
    cr.inputs[7].default_value = U.hex_rgb("#8a8a8a")
    # header band (red) near the top: z > 0.8
    hb = n.new("ShaderNodeMath")
    hb.operation = "GREATER_THAN"
    hb.inputs[1].default_value = 0.82
    l.new(sep.outputs["Z"], hb.inputs[0])
    c2 = n.new("ShaderNodeMix")
    c2.data_type = "RGBA"
    l.new(hb.outputs[0], c2.inputs["Factor"])
    l.new(cr.outputs[2], c2.inputs[6])
    c2.inputs[7].default_value = U.hex_rgb("#b8412f")
    l.new(c2.outputs[2], p.inputs["Base Color"])
    p.inputs["Roughness"].default_value = 0.85
    return m


# ----------------------------------------------------------------------------------------------
# animation helpers
def k_rot(o, f, x=None, y=None, z=None):
    e = list(o.rotation_euler)
    for i, v in enumerate((x, y, z)):
        if v is not None:
            e[i] = v
    o.rotation_euler = e
    o.keyframe_insert("rotation_euler", frame=f)


def k_loc(o, f, loc):
    o.location = loc
    o.keyframe_insert("location", frame=f)


def k_scale(o, f, s):
    o.scale = s if isinstance(s, (tuple, list)) else (s, s, s)
    o.keyframe_insert("scale", frame=f)


def shiver(rig, f0, f1, amp=0.03, rate=3, seed=1):
    """Fast small rotations of the torso and head (every `rate` frames), linear keys."""
    r = U.rng(seed)
    t = rig["torso"]
    h = rig["head"]
    base_t = tuple(t.rotation_euler)
    base_h = tuple(h.rotation_euler)
    f = f0
    while f <= f1:
        a = amp * (1 if (f // rate) % 2 else -1) * r.uniform(0.6, 1.0)
        t.rotation_euler = (base_t[0] + r.uniform(-0.3, 0.3) * amp, base_t[1] + a, base_t[2])
        t.keyframe_insert("rotation_euler", frame=f)
        h.rotation_euler = (base_h[0], base_h[1] - a * 0.6, base_h[2] + r.uniform(-0.5, 0.5) * amp)
        h.keyframe_insert("rotation_euler", frame=f)
        f += rate
    t.rotation_euler = base_t
    t.keyframe_insert("rotation_euler", frame=f1 + rate)
    h.rotation_euler = base_h
    h.keyframe_insert("rotation_euler", frame=f1 + rate)


def blink(rig, f):
    for s in ("L", "R"):
        lid = rig[f"lid_{s}"]
        k_scale(lid, f - 1, (1, 1, 0.0))
        k_scale(lid, f + 1, (1, 1, 1.0))
        k_scale(lid, f + 4, (1, 1, 0.0))


def breathe_puffs(rig, f0, f1, every=40):
    """Little clouds of breath in the cold room."""
    for k, p in enumerate(rig["puffs"]):
        f = f0 + k * every // 3
        while f < f1:
            p.location = (-0.04, -0.28, 0.09)
            p.keyframe_insert("location", frame=f)
            k_scale(p, f, 0.001)
            k_scale(p, f + 3, 0.5)
            p.location = (-0.42, -0.36, 0.02)
            p.keyframe_insert("location", frame=f + 30)
            k_scale(p, f + 30, 1.3)
            k_scale(p, f + 31, 0.001)
            f += every
