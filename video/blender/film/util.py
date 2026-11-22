"""Shared helpers: scene reset, materials, meshes, keyframes, animated controls."""
from __future__ import annotations

import math
import os
import random
from pathlib import Path

import bpy
from mathutils import Vector

FPS = 24
HERE = Path(__file__).resolve().parent.parent
FONT_DIRS = ["/usr/share/fonts/opentype/inter", "/usr/share/fonts/truetype/inter", os.path.expanduser("~/.fonts")]


def sec(t: float) -> int:
    """Film time in seconds -> frame number (frame 1 = 0 s)."""
    return int(round(t * FPS)) + 1


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.unit_settings.system = "METRIC"
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    CTRL.clear()
    return sc


def link(obj, coll=None):
    (coll or bpy.context.scene.collection).objects.link(obj)
    return obj


def collection(name):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(c)
    return c


# ----------------------------------------------------------------------------------------------
# animated controls: one name -> many sockets/properties keyed together
CTRL: dict[str, list] = {}


def ctrl_register(name, owner, attr="default_value", index=None, scale=1.0, offset=0.0):
    CTRL.setdefault(name, []).append((owner, attr, index, scale, offset))


def ctrl_key(name, frame, value, interp="BEZIER"):
    for owner, attr, index, scale, offset in CTRL.get(name, []):
        v = value * scale + offset
        if index is None:
            setattr(owner, attr, v)
            owner.keyframe_insert(attr, frame=frame)
        else:
            getattr(owner, attr)[index] = v
            owner.keyframe_insert(attr, index=index, frame=frame)
    set_interp_all(interp)


def set_interp_all(interp="BEZIER"):
    pass  # default Blender interpolation (Bezier, auto-clamped) gives ease-in/out


def key(obj, attr, frame, value=None, interp=None):
    if value is not None:
        setattr(obj, attr, value)
    obj.keyframe_insert(attr, frame=frame)
    if interp:
        ad = obj.animation_data if hasattr(obj, "animation_data") else None
        if ad and ad.action:
            for fc in ad.action.fcurves:
                if fc.data_path == attr:
                    for kp in fc.keyframe_points:
                        if int(kp.co[0]) == frame:
                            kp.interpolation = interp


def linear_all(obj):
    ad = getattr(obj, "animation_data", None)
    if ad and ad.action:
        for fc in ad.action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"


# ----------------------------------------------------------------------------------------------
# materials
def _nodes(mat):
    mat.use_nodes = True
    nt = mat.node_tree
    return nt, nt.nodes, nt.links


def hex_rgb(h, a=1.0):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    return (*lin, a)


def principled(name, color, rough=0.6, metal=0.0, spec=0.5, noise=0.0, noise_scale=8.0, bump=0.0,
               emission=None, emission_strength=0.0, transmission=0.0, alpha=1.0, coat=0.0):
    """Principled material with optional colour/roughness variation from noise (breaks up CG flatness)."""
    m = bpy.data.materials.new(name)
    nt, n, l = _nodes(m)
    p = n["Principled BSDF"]
    col = color if len(color) == 4 else (*color, 1)
    p.inputs["Base Color"].default_value = col
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    p.inputs["IOR"].default_value = 1.45
    if "Specular IOR Level" in p.inputs:
        p.inputs["Specular IOR Level"].default_value = spec
    if transmission:
        p.inputs["Transmission Weight"].default_value = transmission
    if coat:
        p.inputs["Coat Weight"].default_value = coat
    if alpha < 1:
        p.inputs["Alpha"].default_value = alpha
    if emission is not None:
        p.inputs["Emission Color"].default_value = emission if len(emission) == 4 else (*emission, 1)
        p.inputs["Emission Strength"].default_value = emission_strength
    if noise > 0 or bump > 0:
        tc = n.new("ShaderNodeTexCoord")
        nz = n.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = noise_scale
        nz.inputs["Detail"].default_value = 6
        l.new(tc.outputs["Object"], nz.inputs["Vector"])
        if noise > 0:
            mix = n.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.inputs["Factor"].default_value = noise
            mix.inputs[6].default_value = col
            dark = tuple(c * 0.72 for c in col[:3]) + (1,)
            mix.inputs[7].default_value = dark
            ramp = n.new("ShaderNodeMapRange")
            ramp.inputs["To Min"].default_value = 0
            ramp.inputs["To Max"].default_value = noise
            l.new(nz.outputs["Fac"], ramp.inputs["Value"])
            l.new(ramp.outputs["Result"], mix.inputs["Factor"])
            l.new(mix.outputs[2], p.inputs["Base Color"])
            rr = n.new("ShaderNodeMapRange")
            rr.inputs["To Min"].default_value = max(0, rough - 0.15)
            rr.inputs["To Max"].default_value = min(1, rough + 0.15)
            l.new(nz.outputs["Fac"], rr.inputs["Value"])
            l.new(rr.outputs["Result"], p.inputs["Roughness"])
        if bump > 0:
            b = n.new("ShaderNodeBump")
            b.inputs["Strength"].default_value = bump
            b.inputs["Distance"].default_value = 0.02
            l.new(nz.outputs["Fac"], b.inputs["Height"])
            l.new(b.outputs["Normal"], p.inputs["Normal"])
    return m


def emission(name, color, strength=1.0, transparent_mix=None):
    m = bpy.data.materials.new(name)
    nt, n, l = _nodes(m)
    for x in list(n):
        if x.type == "BSDF_PRINCIPLED":
            n.remove(x)
    out = n["Material Output"]
    e = n.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = color if len(color) == 4 else (*color, 1)
    e.inputs["Strength"].default_value = strength
    if transparent_mix is None:
        l.new(e.outputs[0], out.inputs["Surface"])
    else:
        t = n.new("ShaderNodeBsdfTransparent")
        mx = n.new("ShaderNodeMixShader")
        mx.inputs["Fac"].default_value = transparent_mix
        l.new(t.outputs[0], mx.inputs[1])
        l.new(e.outputs[0], mx.inputs[2])
        l.new(mx.outputs[0], out.inputs["Surface"])
        m.blend_method = "BLEND"
    m["emit"] = e.name
    return m


def assign(obj, mat, slot=None):
    if slot is None:
        obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


# ----------------------------------------------------------------------------------------------
# meshes
def box(name, size, loc, mat=None, bevel=0.0, coll=None, rot=(0, 0, 0)):
    sx, sy, sz = size
    x, y, z = sx / 2, sy / 2, sz / 2
    verts = [(-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z), (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler = rot
    link(o, coll)
    if mat:
        o.data.materials.append(mat)
    if bevel > 0:
        b = o.modifiers.new("bevel", "BEVEL")
        b.width = bevel
        b.segments = 2
        b.limit_method = "ANGLE"
    return o


def mesh_from(name, verts, faces, mat=None, coll=None, smooth=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    link(o, coll)
    if mat:
        o.data.materials.append(mat)
    return o


def boxes_mesh(name, items, mat=None, coll=None):
    """One mesh holding many boxes: items = [(cx, cy, z0, sx, sy, sz, rot_z)]. Fast for thousands."""
    verts, faces = [], []
    for cx, cy, z0, sx, sy, sz, rz in items:
        c, s = math.cos(rz), math.sin(rz)
        base = len(verts)
        for dz in (0, sz):
            for dx, dy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                px, py = dx * sx / 2, dy * sy / 2
                verts.append((cx + c * px - s * py, cy + s * px + c * py, z0 + dz))
        b = base
        faces += [(b, b + 3, b + 2, b + 1), (b + 4, b + 5, b + 6, b + 7), (b, b + 1, b + 5, b + 4),
                  (b + 1, b + 2, b + 6, b + 5), (b + 2, b + 3, b + 7, b + 6), (b + 3, b, b + 4, b + 7)]
    return mesh_from(name, verts, faces, mat, coll)


def _bm_object(name, bm, loc, rot, coll, mat, smooth, scale=(1, 1, 1)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    o.location = loc
    o.rotation_euler = rot
    o.scale = scale
    link(o, coll)
    if mat:
        o.data.materials.append(mat)
    return o


def cylinder(name, r, depth, loc, mat=None, verts=24, rot=(0, 0, 0), coll=None, smooth=True, r2=None, cap=True):
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=verts, radius1=r, radius2=r if r2 is None else r2, depth=depth)
    o = _bm_object(name, bm, loc, rot, coll, mat, smooth)
    if smooth:
        _autosmooth(o)
    return o


def _autosmooth(o):
    """Smooth sides, flat caps."""
    for p in o.data.polygons:
        p.use_smooth = abs(p.normal.z) < 0.9


def sphere(name, r, loc, mat=None, seg=24, coll=None, scale=(1, 1, 1)):
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
    return _bm_object(name, bm, loc, (0, 0, 0), coll, mat, True, scale)


def torus(name, major, minor, loc, mat=None, coll=None, seg=32, seg2=12):
    verts, faces = [], []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        for j in range(seg2):
            b = 2 * math.pi * j / seg2
            rr = major + minor * math.cos(b)
            verts.append((rr * math.cos(a), rr * math.sin(a), minor * math.sin(b)))
    for i in range(seg):
        for j in range(seg2):
            a, b = i * seg2 + j, i * seg2 + (j + 1) % seg2
            c, d = ((i + 1) % seg) * seg2 + (j + 1) % seg2, ((i + 1) % seg) * seg2 + j
            faces.append((a, b, c, d))
    o = mesh_from(name, verts, faces, mat, coll, smooth=True)
    o.location = loc
    return o


def _move(o, coll):
    if coll is not None:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)


def tube_path(name, points, radius, mat=None, coll=None, res=6):
    """A bevelled poly curve through points (pipes, wires, river lines)."""
    cu = bpy.data.curves.new(name, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = res // 2
    cu.use_fill_caps = True
    sp = cu.splines.new("POLY")
    sp.points.add(len(points) - 1)
    for i, p in enumerate(points):
        sp.points[i].co = (*p, 1)
    o = bpy.data.objects.new(name, cu)
    link(o, coll)
    if mat:
        o.data.materials.append(mat)
    return o


def look_at(obj, target):
    d = Vector(target) - obj.location
    obj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def font(weight="Regular"):
    for d in FONT_DIRS:
        p = Path(d) / f"Inter-{weight}.otf"
        if p.exists():
            return bpy.data.fonts.load(str(p), check_existing=True)
    return None  # Blender's built-in font


def text(name, body, loc, size, mat, weight="Regular", align="LEFT", coll=None, rot=(math.pi / 2, 0, 0)):
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = body
    cu.size = size
    cu.align_x = align
    f = font(weight)
    if f:
        cu.font = f
    o = bpy.data.objects.new(name, cu)
    o.location = loc
    o.rotation_euler = rot
    link(o, coll)
    o.data.materials.append(mat)
    return o


def area_light(name, loc, size, energy, color, target=None, shape="RECTANGLE", size_y=None, coll=None):
    ld = bpy.data.lights.new(name, "AREA")
    ld.energy = energy
    ld.color = color[:3]
    ld.shape = shape
    ld.size = size
    if size_y:
        ld.size_y = size_y
    o = bpy.data.objects.new(name, ld)
    o.location = loc
    link(o, coll)
    if target is not None:
        look_at(o, target)
    return o


def point_light(name, loc, energy, color, radius=0.1, coll=None):
    ld = bpy.data.lights.new(name, "POINT")
    ld.energy = energy
    ld.color = color[:3]
    ld.shadow_soft_size = radius
    o = bpy.data.objects.new(name, ld)
    o.location = loc
    link(o, coll)
    return o


def spot_light(name, loc, energy, color, target, angle=1.2, blend=0.6, radius=0.1, coll=None):
    ld = bpy.data.lights.new(name, "SPOT")
    ld.energy = energy
    ld.color = color[:3]
    ld.spot_size = angle
    ld.spot_blend = blend
    ld.shadow_soft_size = radius
    o = bpy.data.objects.new(name, ld)
    o.location = loc
    link(o, coll)
    look_at(o, target)
    return o


def kelvin(k):
    """Approximate blackbody colour (linear RGB) for light colour temperatures 1500-12000 K."""
    t = k / 100
    r = 255 if t <= 66 else 329.7 * (t - 60) ** -0.1332
    g = 99.47 * math.log(t) - 161.12 if t <= 66 else 288.12 * (t - 60) ** -0.0755
    b = 255 if t >= 66 else (0 if t <= 19 else 138.52 * math.log(t - 10) - 305.04)
    c = [max(0, min(255, x)) / 255 for x in (r, g, b)]
    return tuple(x ** 2.2 for x in c) + (1.0,)


def rng(seed):
    return random.Random(seed)
