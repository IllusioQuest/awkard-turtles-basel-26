"""Render settings, world lighting and compositor (mist, bloom) for the film."""
from __future__ import annotations

import bpy

from . import util as U

PRESETS = {
    # quick look development stills and the timing preview
    "preview": dict(res=(960, 540), samples=4, bounces=3, denoise=True, motion_blur=False),
    # final delivery
    "final": dict(res=(1920, 1080), samples=64, bounces=6, denoise=True, motion_blur=True),
}


def setup_render(scene, preset="preview"):
    p = PRESETS[preset]
    r = scene.render
    r.engine = "CYCLES"
    r.resolution_x, r.resolution_y = p["res"]
    r.resolution_percentage = 100
    r.fps = U.FPS
    r.use_motion_blur = p["motion_blur"]
    r.motion_blur_shutter = 0.4
    r.film_transparent = False
    r.use_persistent_data = True
    c = scene.cycles
    c.device = "GPU" if _has_gpu() else "CPU"
    c.samples = p["samples"]
    c.use_adaptive_sampling = True
    c.adaptive_threshold = 0.05 if preset == "preview" else 0.01
    c.use_denoising = p["denoise"]
    c.denoiser = "OPENIMAGEDENOISE"
    c.max_bounces = p["bounces"]
    c.diffuse_bounces = min(3, p["bounces"])
    c.glossy_bounces = min(3, p["bounces"])
    c.transmission_bounces = p["bounces"]
    c.transparent_max_bounces = 12
    c.caustics_reflective = False
    c.caustics_refractive = False
    c.blur_glossy = 1.0
    c.sample_clamp_indirect = 6.0
    c.use_light_tree = True
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.0
    r.image_settings.file_format = "PNG"
    r.image_settings.color_mode = "RGB"
    return scene


def _has_gpu():
    """Enable the first available GPU backend for Cycles and report it.

    The film runs Blender with --factory-startup, so the GPU chosen in Blender's preferences is not
    used; this picks one here instead. Force a backend with the environment variable DH_GPU
    (OPTIX, CUDA, HIP, ONEAPI, METAL) or DH_GPU=CPU to render on the CPU.
    """
    import os
    want = os.environ.get("DH_GPU", "").upper()
    if want == "CPU":
        print("Cycles device: CPU (DH_GPU=CPU)", flush=True)
        return False
    try:
        if "cycles" not in bpy.context.preferences.addons:
            bpy.ops.preferences.addon_enable(module="cycles")
        prefs = bpy.context.preferences.addons["cycles"].preferences
    except Exception as e:  # noqa: BLE001
        print(f"Cycles device: CPU (cannot read Cycles preferences: {e})", flush=True)
        return False
    order = [want] if want else ["OPTIX", "CUDA", "HIP", "ONEAPI", "METAL"]
    for t in order:
        try:
            prefs.compute_device_type = t
        except TypeError:
            continue
        try:
            prefs.refresh_devices()
        except AttributeError:
            prefs.get_devices()
        gpus = [d for d in prefs.devices if d.type == t]
        if gpus:
            for d in prefs.devices:
                d.use = d.type == t
            print("Cycles device: GPU via " + t + ": " + ", ".join(d.name for d in gpus), flush=True)
            return True
    print("Cycles device: CPU (no GPU found for " + ", ".join(order) + ")", flush=True)
    return False


def night_world(scene, strength=1.0):
    """Blue winter night: dark zenith, faint warm city glow at the horizon."""
    w = scene.world
    w.use_nodes = True
    nt = w.node_tree
    n, l = nt.nodes, nt.links
    n.clear()
    out = n.new("ShaderNodeOutputWorld")
    bg = n.new("ShaderNodeBackground")
    tc = n.new("ShaderNodeTexCoord")
    sep = n.new("ShaderNodeSeparateXYZ")
    l.new(tc.outputs["Generated"], sep.inputs[0])
    ramp = n.new("ShaderNodeValToRGB")
    el = ramp.color_ramp.elements
    el[0].position = 0.5
    el[0].color = U.hex_rgb("#5a4f58")
    el[1].position = 0.75
    el[1].color = U.hex_rgb("#0b1830")
    e2 = el.new(0.56)
    e2.color = U.hex_rgb("#26314c")
    l.new(sep.outputs["Z"], ramp.inputs["Fac"])
    l.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = strength
    U.ctrl_register("sky", bg.inputs["Strength"], scale=strength)
    l.new(bg.outputs[0], out.inputs["Surface"])
    w.cycles_visibility.diffuse = True
    # moon
    sun = bpy.data.lights.new("moon", "SUN")
    sun.energy = 0.18
    sun.color = U.kelvin(9500)[:3]
    sun.angle = 0.02
    o = bpy.data.objects.new("moon", sun)
    o.rotation_euler = (0.95, 0.15, 2.4)
    U.link(o)
    U.ctrl_register("moon", sun, "energy", scale=0.18)
    return o


def compositor(scene, mist_start=20, mist_depth=260, fog_hex="#1b2740", glare=True):
    scene.use_nodes = True
    vl = scene.view_layers[0]
    vl.use_pass_mist = True
    scene.world.mist_settings.start = mist_start
    scene.world.mist_settings.depth = mist_depth
    scene.world.mist_settings.falloff = "QUADRATIC"
    nt = scene.node_tree
    n, l = nt.nodes, nt.links
    n.clear()
    rl = n.new("CompositorNodeRLayers")
    comp = n.new("CompositorNodeComposite")
    mix = n.new("CompositorNodeMixRGB")
    mix.blend_type = "MIX"
    fogv = n.new("CompositorNodeMath")
    fogv.operation = "MULTIPLY"
    fogv.inputs[1].default_value = 0.0
    U.ctrl_register("fog", fogv.inputs[1], scale=1.0)
    l.new(rl.outputs["Mist"], fogv.inputs[0])
    l.new(fogv.outputs[0], mix.inputs["Fac"])
    l.new(rl.outputs["Image"], mix.inputs[1])
    mix.inputs[2].default_value = U.hex_rgb(fog_hex)
    last = mix.outputs[0]
    if glare:
        g = n.new("CompositorNodeGlare")
        g.glare_type = "FOG_GLOW" if bpy.app.version < (4, 2, 0) else "BLOOM"
        g.quality = "MEDIUM"
        g.mix = -0.75
        g.threshold = 0.9
        g.size = 7
        l.new(last, g.inputs["Image"])
        last = g.outputs["Image"]
    l.new(last, comp.inputs["Image"])
    return nt
