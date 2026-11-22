"""Builds the whole film world once and keys every shot from timeline.json.

Shots (cameras bound to timeline markers):
  street   establishing: snowy Basel street, Bob's cold window among warm neighbours
  cold     living room: Bob shivers, touches the cold radiator, opens the heating bill
  dc       a data centre nearby releasing heat into the night
  dive     camera dives through the floor: servers wake, heat flows, back up to a warm room
  lapse    weeks pass; 22 March 09:00 the tank is full and the servers pause
  figures  key figures appear in the basement
  rise     camera rises out of the street until the city becomes the map
  map      Basel region, Dreispitz example, 2025 time-lapse with job flows
  end      calm frame for the hand-over to the live demo
"""
from __future__ import annotations

import datetime as dt
import json
import math

import bpy
from mathutils import Vector

from . import bob as B
from . import city as C
from . import datacentre as DC
from . import interior as I
from . import look
from . import street as S
from . import util as U

HERE = U.HERE


def load():
    tl = json.loads((HERE / "timeline.json").read_text())
    data = json.loads((HERE / "data/film-data.json").read_text())
    nums = json.loads((HERE / "numbers.json").read_text())
    return tl, data, nums


class Cam:
    def __init__(self, name, lens=35):
        self.cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        self.cam.data.lens = lens
        self.cam.data.clip_start = 0.05
        self.cam.data.clip_end = 200000
        self.cam.data.sensor_width = 36
        U.link(self.cam)
        self.tgt = bpy.data.objects.new(name + " target", None)
        U.link(self.tgt)
        c = self.cam.constraints.new("TRACK_TO")
        c.target = self.tgt
        c.track_axis = "TRACK_NEGATIVE_Z"
        c.up_axis = "UP_Y"

    def key(self, t, loc, look_at, lens=None, interp=None):
        f = U.sec(t)
        self.cam.location = loc
        self.cam.keyframe_insert("location", frame=f)
        self.tgt.location = look_at
        self.tgt.keyframe_insert("location", frame=f)
        if lens:
            self.cam.data.lens = lens
            self.cam.data.keyframe_insert("lens", frame=f)

    def dof(self, focus_obj=None, fstop=2.8, dist=None):
        d = self.cam.data.dof
        d.use_dof = True
        d.aperture_fstop = fstop
        if focus_obj:
            d.focus_object = focus_obj
        if dist:
            d.focus_distance = dist


def build(preset="preview"):
    tl, data, nums = load()
    T = {k: v["start"] for k, v in tl["beats"].items()}
    E = {k: v["end"] for k, v in tl["beats"].items()}
    Mk = tl["marks"]
    sc = U.reset()
    look.setup_render(sc, preset)
    look.night_world(sc, 1.0)
    look.compositor(sc)
    sc.frame_start = 1
    sc.frame_end = U.sec(tl["duration"]) - 1

    # ---------------------------------------------------------------- world
    ctx = S.build_street(U.collection("street"))
    room = I.build(U.collection("interior"))
    dc = DC.build(U.collection("datacentre"))
    cityc = U.collection("city")
    C.build_city(cityc, exclude=[(-170, -120, 170, 130), (DC.DC_ORIGIN[0] - 190, DC.DC_ORIGIN[1] - 200, DC.DC_ORIGIN[0] + 130, DC.DC_ORIGIN[1] + 90)])
    C.ground(cityc)
    mapc = U.collection("map")
    C.map_layer(mapc, data["region"]["sites"], data["pool"]["sites"])
    snow_street = S.snowfall(ctx, (0, -1.0, 16), (90, 15), 4000, 3, 1, U.sec(E["dive"]))
    snow_dc = S.snowfall(ctx, (DC.DC_ORIGIN[0] - 10, DC.DC_ORIGIN[1] - 60, 30), (160, 80), 3500, 4, U.sec(T["dc"]) - 100, U.sec(E["dc"]), name="snowfall dc")

    rig = B.build(U.collection("bob"), loc=(-2.7, I.FY + 0.8, S.GROUND_FLOOR_Z), rot_z=math.radians(146))

    # global states at frame 1
    f1 = 1
    for name, v in (("snow", 1.0), ("night_windows", 1.0), ("fog", 0.55), ("sky", 1.0), ("moon", 1.0), ("frost", 1.0),
                    ("cold_nose", 1.0), ("lamp", 0.0), ("radiator", 0.0), ("servers_on", 0.0), ("flow", 0.0),
                    ("tank_level", 0.02), ("tank_glow", 0.0), ("basement_light", 1.0), ("paused_color", 0.0),
                    ("map", 0.0), ("map_layer", 0.0), ("day", 0.0)):
        U.ctrl_key(name, f1, v)
    for k in range(4):
        U.ctrl_key(f"server_{k}", f1, 0.0)
    _linear_ctrl("flow_phase", 1, 0.0, sc.frame_end, sc.frame_end / U.FPS * 0.45)
    _linear_ctrl("plume_phase", 1, 0.0, sc.frame_end, sc.frame_end / U.FPS * 1.4)
    _linear_ctrl("flow_dash", 1, 0.0, sc.frame_end, sc.frame_end / U.FPS * 0.6)

    # cold interior light: blue window light from the street + a dim cool fill
    for wx in (-2.4, 0.0):
        lw = U.area_light("window cold light", (wx, S.FACADE_Y - 0.8, S.GROUND_FLOOR_Z + 1.9), 1.3, 45, U.kelvin(8500), target=(wx, I.FY + 3, S.GROUND_FLOOR_Z + 0.6), size_y=1.8)
    fill = U.area_light("room fill", (-1.0, I.FY + 2.6, I.LIV["z1"] - 0.05), 2.5, 12, U.kelvin(7000), target=(-1.0, I.FY + 2.6, 0), size_y=2.5)
    U.ctrl_register("room_fill", fill.data, "energy", scale=12)
    U.ctrl_key("room_fill", 1, 1.0)
    # daylight for the time-lapse (sun through the window), off at night
    sun = bpy.data.lights.new("day sun", "SUN")
    sun.energy = 0.0
    sun.color = U.kelvin(5600)[:3]
    sun.angle = 0.03
    so = bpy.data.objects.new("day sun", sun)
    so.rotation_euler = (math.radians(58), 0, math.radians(20))
    U.link(so)
    U.ctrl_register("day", sun, "energy", scale=3.5)
    U.ctrl_register("day", sc.world.node_tree.nodes["Background"].inputs["Strength"], scale=6.0, offset=1.0)

    cams = {}

    # ---------------------------------------------------------------- 1 street
    c = Cam("cam street", 32)
    t0, t1 = T["street"], E["street"]
    c.key(t0, (-26, -1.2, 1.7), (-6, 8.0, 3.2))
    c.key(t0 + 4.0, (-17, -0.4, 1.75), (-1.5, 8.5, 3.0))
    c.key(t1, (-9.0, 0.6, 1.85), (-1.2, 8.6, 2.6))
    cams["street"] = c
    # Bob's window: cold and dim from outside (frost + no lamp): defined by state at frame 1

    # ---------------------------------------------------------------- 2 cold
    c = Cam("cam cold", 30)
    t0, t1 = T["cold"], E["cold"]
    head = rig["head"]
    c.key(t0, (-0.45, I.FY + 4.15, S.GROUND_FLOOR_Z + 1.6), (-2.5, I.FY + 0.95, S.GROUND_FLOOR_Z + 1.35))
    c.key(Mk["cold_open"], (-0.75, I.FY + 3.6, S.GROUND_FLOOR_Z + 1.62), (-2.6, I.FY + 0.9, S.GROUND_FLOOR_Z + 1.45))
    c.key(t1, (-1.35, I.FY + 2.8, S.GROUND_FLOOR_Z + 1.72), (-2.68, I.FY + 0.82, S.GROUND_FLOOR_Z + 1.62), lens=36)
    c.dof(fstop=2.8, dist=3.4)
    cams["cold"] = c
    _animate_bob_cold(rig, T, E, Mk)

    # ---------------------------------------------------------------- 3 data centre
    c = Cam("cam dc", 28)
    ox, oy, H = dc["centre"]
    t0, t1 = T["dc"], E["dc"]
    c.key(t0, (ox - 70, oy - 95, 3.0), (ox - 10, oy, H + 14))
    c.key(t1, (ox - 52, oy - 80, 5.5), (ox - 5, oy, H + 22))
    cams["dc"] = c

    # ---------------------------------------------------------------- 4 dive
    c = Cam("cam dive", 22)
    t0, t1 = T["dive"], E["dive"]
    rx = room["rad_x"]
    FY, z0 = I.FY, S.GROUND_FLOOR_Z
    c.key(t0, (-0.2, FY + 2.6, z0 + 2.2), (rx + 0.1, FY + 0.6, z0 + 0.4))
    c.key(Mk["dive_down"], (rx + 0.6, FY + 1.3, z0 + 0.9), (rx + 0.5, FY + 1.0, z0 - 1.0))
    c.key(Mk["dive_down"] + 0.9, (rx + 0.8, FY + 1.3, z0 - 0.5), (I.BAS["x0"] + 0.5, FY + 2.9, I.BAS["z0"] + 0.6))
    rack_x, rack_y = room["rack"]
    c.key(Mk["dive_alive"], (rack_x + 1.9, rack_y - 1.5, I.BAS["z0"] + 1.05), (rack_x + 0.3, rack_y, I.BAS["z0"] + 0.75))
    c.key(Mk["dive_flow"], (0.6, FY + 0.75, I.BAS["z0"] + 1.55), (-2.3, FY + 3.5, I.BAS["z0"] + 0.85))
    tx, ty = room["tank_pos"]
    c.key(Mk["dive_up"], (0.9, FY + 1.1, I.BAS["z0"] + 1.6), (tx - 0.3, ty, I.BAS["z0"] + 1.1))
    c.key(Mk["dive_up"] + 1.4, (tx + 1.0, ty - 2.2, I.BAS["z1"] - 0.35), (rx - 0.5, FY + 0.2, I.BAS["z1"] + 0.2))
    c.key(Mk["dive_room"], (rx + 1.2, FY + 1.6, z0 + 0.6), (rx, FY + 0.2, z0 + 0.6))
    c.key(Mk["dive_room"] + 2.5, (0.2, FY + 3.3, z0 + 1.35), (-1.25, FY + 1.3, z0 + 0.95))
    c.key(t1, (0.6, FY + 3.9, z0 + 1.45), (-1.3, FY + 1.3, z0 + 0.95))
    cams["dive"] = c
    # floor / slab invisible to the camera while it passes through
    fdown = U.sec(Mk["dive_down"] + 0.45)
    fup = U.sec(Mk["dive_up"] + 0.7)
    for o in (room["floor"], room["slab"]):
        _vis_cam(o, 1, True)
        _vis_cam(o, fdown, False)
        _vis_cam(o, U.sec(Mk["dive_alive"]) - 6, True)
        _vis_cam(o, fup, False)
        _vis_cam(o, U.sec(Mk["dive_room"]) - 4, True)
    # servers wake in sequence; flow; radiator warms; room warms
    a = Mk["dive_alive"]
    for k in range(4):
        U.ctrl_key(f"server_{k}", U.sec(a + 0.5 * k), 0.0)
        U.ctrl_key(f"server_{k}", U.sec(a + 0.5 * k) + 3, 1.0)
    U.ctrl_key("servers_on", U.sec(a), 0.0)
    U.ctrl_key("servers_on", U.sec(a + 2.0), 1.0)
    U.ctrl_key("basement_light", U.sec(a), 0.35)
    U.ctrl_key("flow", U.sec(Mk["dive_flow"]), 0.0)
    U.ctrl_key("flow", U.sec(Mk["dive_flow"] + 1.5), 1.0)
    # in January all server heat goes straight to the radiators: the tank stays nearly empty
    U.ctrl_key("tank_glow", U.sec(Mk["dive_flow"] + 1.0), 0.0)
    U.ctrl_key("tank_glow", U.sec(Mk["dive_flow"] + 2.5), 0.7)
    U.ctrl_key("tank_level", U.sec(Mk["dive_flow"] + 1.0), 0.02)
    U.ctrl_key("tank_level", U.sec(Mk["dive_up"]), 0.06)
    U.ctrl_key("radiator", U.sec(Mk["dive_up"] + 1.0), 0.0)
    U.ctrl_key("radiator", U.sec(Mk["dive_room"] + 2.0), 1.0)
    U.ctrl_key("frost", U.sec(Mk["dive_room"]), 1.0)
    U.ctrl_key("frost", U.sec(t1 - 1.0), 0.0)
    U.ctrl_key("cold_nose", U.sec(Mk["dive_room"]), 1.0)
    U.ctrl_key("cold_nose", U.sec(t1 - 1.0), 0.0)
    U.ctrl_key("room_fill", U.sec(Mk["dive_room"]), 1.0)
    U.ctrl_key("room_fill", U.sec(t1), 0.45)
    U.ctrl_key("lamp", U.sec(Mk["dive_room"] + 3.5), 0.0)
    U.ctrl_key("lamp", U.sec(Mk["dive_room"] + 4.0), 1.0)
    _animate_bob_warm(rig, T, E, Mk)

    # ---------------------------------------------------------------- 5 lapse
    c = Cam("cam lapse", 24)
    c.cam.data.lens = 24
    t0, t1 = T["lapse"], E["lapse"]
    p = Mk["lapse_pause"]
    c.key(t0, (0.75, FY + 4.0, z0 + 1.5), (-1.3, FY + 1.2, z0 + 1.0))
    c.key(p - 1.0 / U.FPS, (0.85, FY + 4.1, z0 + 1.52), (-1.3, FY + 1.2, z0 + 1.0))
    # cut to the basement at the pause
    c.key(p, (0.7, FY + 0.8, I.BAS["z0"] + 1.5), (-2.3, FY + 3.6, I.BAS["z0"] + 0.85), lens=22)
    c.key(t1, (0.5, FY + 1.0, I.BAS["z0"] + 1.45), (-2.3, FY + 3.6, I.BAS["z0"] + 0.85), lens=22)
    for fc in c.cam.animation_data.action.fcurves:
        for kp in fc.keyframe_points:
            if abs(kp.co[0] - (U.sec(p) - 1)) < 0.5:
                kp.interpolation = "CONSTANT"
    cams["lapse"] = c
    _animate_lapse(sc, rig, data, T, E, Mk, snow_street)

    # ---------------------------------------------------------------- 6 figures
    c = Cam("cam figures", 30)
    t0, t1 = T["figures"], E["figures"]
    c.key(t0, (-0.2, I.BAS["y0"] + 1.2, I.BAS["z0"] + 1.35), (0.0, I.BAS["y1"], I.BAS["z0"] + 1.4))
    c.key(t1, (0.5, I.BAS["y0"] + 1.5, I.BAS["z0"] + 1.4), (0.35, I.BAS["y1"], I.BAS["z0"] + 1.4))
    cams["figures"] = c
    _figures(nums, T, E)

    # ---------------------------------------------------------------- 7 rise
    c = Cam("cam rise", 30)
    t0, t1 = T["rise"], E["rise"]
    m = Mk["rise_map"]
    c.key(t0, (-1.0, -2.5, 1.7), (-1.0, 8.6, 3.0))
    c.key(t0 + 2.5, (-1.0, -8.0, 9.0), (-1.0, 9.0, 3.5))
    c.key(t0 + 4.5, (-1.0, -45.0, 90.0), (-1.0, 12.0, 0.0))
    c.key(m, (40.0, -500.0, 900.0), (60.0, 20.0, 0.0))
    c.key(t1, (300.0, -2600.0, 9000.0), (500.0, 200.0, 0.0))
    cams["rise"] = c
    U.ctrl_key("map", U.sec(m - 1.5), 0.0)
    U.ctrl_key("map", U.sec(m + 2.5), 1.0)
    U.ctrl_key("map_layer", U.sec(m), 0.0)
    U.ctrl_key("map_layer", U.sec(t1), 1.0)
    U.ctrl_key("fog", U.sec(t0 + 2), 0.55)
    U.ctrl_key("fog", U.sec(m), 0.0)

    # ---------------------------------------------------------------- 8 map + 9 end
    cams["map"] = _map_section(sc, data, T, E, Mk)
    cams["end"] = cams["map"]

    # markers bind cameras
    for name in ("street", "cold", "dc", "dive", "lapse", "figures", "rise", "map"):
        mk = sc.timeline_markers.new(name, frame=U.sec(T[name]))
        mk.camera = cams[name].cam
    sc.camera = cams["street"].cam
    # snow stops before the time-lapse ends; dc snow only around its shot
    _hide(snow_street, 1, False)
    _hide(snow_street, U.sec(T["lapse"] + 4), True)
    for b in ctx.mats.get("_banks", []):
        _hide(b, 1, False)
        _hide(b, U.sec(Mk["lapse_pause"]), True)
    _hide(snow_dc, 1, True)
    _hide(snow_dc, U.sec(T["dc"]), False)
    _hide(snow_dc, U.sec(E["dc"]), True)
    return sc, tl, data


# ----------------------------------------------------------------------------------------------
def _linear_ctrl(name, f0, v0, f1, v1):
    U.ctrl_key(name, f0, v0)
    U.ctrl_key(name, f1, v1)
    _all_linear_for(name)


def _all_linear_for(name):
    paths = set()
    for owner, attr, index, *_ in U.CTRL.get(name, []):
        try:
            paths.add((owner.id_data, owner.path_from_id(attr)))
        except Exception:
            pass
    for idd, path in paths:
        ad = idd.animation_data
        if not ad or not ad.action:
            continue
        for fc in ad.action.fcurves:
            if fc.data_path == path:
                for kp in fc.keyframe_points:
                    kp.interpolation = "LINEAR"
                fc.extrapolation = "LINEAR"


def _vis_cam(o, f, v):
    o.visible_camera = v
    o.keyframe_insert("visible_camera", frame=f)
    for fc in o.animation_data.action.fcurves:
        if fc.data_path == "visible_camera":
            for kp in fc.keyframe_points:
                kp.interpolation = "CONSTANT"


def _hide(o, f, v):
    o.hide_render = v
    o.keyframe_insert("hide_render", frame=f)
    for fc in o.animation_data.action.fcurves:
        if fc.data_path == "hide_render":
            for kp in fc.keyframe_points:
                kp.interpolation = "CONSTANT"


def _animate_bob_cold(rig, T, E, Mk):
    f = U.sec
    t0, t1 = T["cold"], E["cold"]
    # rest pose: arms slightly in front, holding the envelope low in the right hand
    B.k_rot(rig["arm_R"], 1, x=-0.35, y=-0.12)
    B.k_rot(rig["fore_R"], 1, x=-0.6)
    B.k_rot(rig["arm_L"], 1, x=-0.1, y=0.12)
    B.k_rot(rig["fore_L"], 1, x=-0.2)
    B.k_rot(rig["torso"], 1, x=0.04)
    B.k_rot(rig["flap"], 1, x=0.0)
    B.k_loc(rig["letter"], 1, (0, 0.0, -0.02))
    B.k_scale(rig["letter"], 1, (0.95, 1, 0.9))
    B.shiver(rig, f(t0), f(Mk["cold_react"]), amp=0.02, rate=3, seed=2)
    B.breathe_puffs(rig, f(T["street"]), f(T["dive"] + 4), every=44)
    for bt in (t0 + 1.2, t0 + 4.5, Mk["cold_read"] - 0.5, t1 - 1.5):
        B.blink(rig, f(bt))
    # radiator gag: left hand reaches down to the radiator ... cold! ... pulls back
    tt = Mk["cold_touch"]
    B.k_rot(rig["arm_L"], f(tt), x=-0.1, y=0.12)
    B.k_rot(rig["arm_L"], f(tt + 0.7), x=-0.55, y=0.45)
    B.k_rot(rig["fore_L"], f(tt + 0.7), x=-0.25)
    B.k_rot(rig["torso"], f(tt + 0.7), x=0.22)
    B.k_rot(rig["arm_L"], f(tt + 1.5), x=-0.6, y=0.5)
    B.k_rot(rig["arm_L"], f(tt + 1.62), x=-0.1, y=0.15)  # snap back
    B.k_rot(rig["fore_L"], f(tt + 1.62), x=-1.3)
    B.k_rot(rig["torso"], f(tt + 1.62), x=0.0)
    B.k_rot(rig["fore_L"], f(tt + 2.6), x=-1.4)
    B.k_rot(rig["arm_L"], f(tt + 2.6), x=-0.35, y=0.1)  # hand to chest, rubbing
    # envelope up to the chest
    to = Mk["cold_open"]
    B.k_rot(rig["arm_R"], f(to - 1.0), x=-0.35, y=-0.12)
    B.k_rot(rig["fore_R"], f(to - 1.0), x=-0.6)
    B.k_rot(rig["arm_R"], f(to), x=-0.55, y=0.18)
    B.k_rot(rig["fore_R"], f(to), x=-1.25)
    B.k_rot(rig["arm_L"], f(to), x=-0.55, y=-0.2)
    B.k_rot(rig["fore_L"], f(to), x=-1.2)
    B.k_rot(rig["head"], f(to - 0.4), x=0.0)
    B.k_rot(rig["head"], f(to + 0.3), x=0.28)  # looks down at it
    B.k_rot(rig["flap"], f(to + 0.3), x=0.0)
    B.k_rot(rig["flap"], f(to + 1.0), x=2.7)
    B.k_loc(rig["letter"], f(to + 1.0), (0, 0.0, -0.02))
    B.k_loc(rig["letter"], f(to + 2.0), (0, -0.01, 0.2))
    # reads ... reaction
    r = Mk["cold_react"]
    for s in ("L", "R"):
        B.k_scale(rig[f"brow_{s}"], f(r - 0.2), (1, 1, 1))
        rig[f"brow_{s}"].keyframe_insert("location", frame=f(r - 0.2))
    B.k_scale(rig["mouth"], f(r - 0.2), (1.0, 0.35, 0.28))
    B.k_rot(rig["head"], f(r - 0.2), x=0.28)
    B.k_rot(rig["head"], f(r + 0.15), x=-0.05)  # head snaps up
    for s in ("L", "R"):
        br = rig[f"brow_{s}"]
        loc = br.location.copy()
        br.location = (loc.x, loc.y, loc.z + 0.035)
        br.keyframe_insert("location", frame=f(r + 0.15))
    B.k_scale(rig["mouth"], f(r + 0.15), (0.55, 0.4, 0.75))  # "o"
    B.shiver(rig, f(r + 0.2), f(t1), amp=0.05, rate=2, seed=3)  # the big shiver
    # pompom wobble during the big shiver
    pp = rig["pompom"]
    for k, ff in enumerate(range(f(r + 0.2), f(t1), 3)):
        pp.location = (0.015 * (1 if k % 2 else -1), 0, 0.2)
        pp.keyframe_insert("location", frame=ff)


def _animate_bob_warm(rig, T, E, Mk):
    """After the dive: Bob sits in the armchair, takes off his hat, leans back, eyes closed."""
    f = U.sec
    t = Mk["dive_room"]
    chair = (-1.25, I.FY + 1.42, S.GROUND_FLOOR_Z)
    root = rig["root"]
    # teleport while the camera is in the basement (constant keys)
    B.k_loc(root, f(Mk["dive_up"]) - 1, root.location.copy())
    B.k_rot(root, f(Mk["dive_up"]) - 1, z=root.rotation_euler.z)
    B.k_loc(root, f(Mk["dive_up"]), (chair[0], chair[1], chair[2] - 0.22))
    B.k_rot(root, f(Mk["dive_up"]), z=math.radians(200))
    for fc in root.animation_data.action.fcurves:
        for kp in fc.keyframe_points:
            if abs(kp.co[0] - f(Mk["dive_up"])) <= 1:
                kp.interpolation = "CONSTANT"
    seat = [(rig["leg_L"], -1.5), (rig["leg_R"], -1.5)]
    for leg, a in seat:
        B.k_rot(leg, f(Mk["dive_up"]) - 1, x=0.0)
        B.k_rot(leg, f(Mk["dive_up"]), x=a)
    for kn in (rig["knee_L"], rig["knee_R"]):
        B.k_rot(kn, f(Mk["dive_up"]) - 1, x=0.0)
        B.k_rot(kn, f(Mk["dive_up"]), x=1.45)
    B.k_rot(rig["torso"], f(Mk["dive_up"]), x=-0.12)
    B.k_rot(rig["arm_R"], f(Mk["dive_up"]), x=-0.5, y=-0.25)
    B.k_rot(rig["fore_R"], f(Mk["dive_up"]), x=-0.5)
    B.k_rot(rig["arm_L"], f(Mk["dive_up"]), x=-0.5, y=0.25)
    B.k_rot(rig["fore_L"], f(Mk["dive_up"]), x=-0.5)
    B.k_rot(rig["head"], f(Mk["dive_up"]), x=0.0, y=0.0, z=0.0)
    B.k_scale(rig["mouth"], f(Mk["dive_up"]), (1.0, 0.35, 0.28))
    for s in ("L", "R"):
        br = rig[f"brow_{s}"]
        br.location.z -= 0.035
        br.keyframe_insert("location", frame=f(Mk["dive_up"]))
    B.k_scale(rig["envelope"], f(Mk["dive_up"]) - 1, 1)
    B.k_scale(rig["envelope"], f(Mk["dive_up"]), 0.0001)  # bill put away
    # small shiver still when we arrive, then it fades
    B.shiver(rig, f(t), f(t + 1.6), amp=0.012, rate=4, seed=5)
    # takes the hat off (hat lifts and goes to the side table / out of frame)
    hat = rig["hat"]
    th = t + 2.8
    B.k_rot(rig["arm_L"], f(th - 0.8), x=-0.5, y=0.25)
    B.k_rot(rig["fore_L"], f(th - 0.8), x=-0.5)
    B.k_rot(rig["arm_L"], f(th), x=-2.6, y=0.35)
    B.k_rot(rig["fore_L"], f(th), x=-1.0)
    B.k_loc(hat, f(th), hat.location.copy())
    B.k_loc(hat, f(th + 0.6), (0.18, 0.05, 0.42))
    B.k_scale(hat, f(th + 0.6), 1)
    B.k_scale(hat, f(th + 0.75), 0.0001)
    B.k_rot(rig["arm_L"], f(th + 1.4), x=-0.5, y=0.25)
    B.k_rot(rig["fore_L"], f(th + 1.4), x=-0.5)
    # sinks back with a sigh: shoulders drop, head back, eyes close, content smile
    ts = th + 1.6
    B.k_rot(rig["torso"], f(ts), x=-0.12)
    B.k_rot(rig["torso"], f(ts + 1.2), x=-0.3)
    B.k_rot(rig["head"], f(ts + 1.2), x=-0.2)
    for s in ("L", "R"):
        B.k_scale(rig[f"lid_{s}"], f(ts + 0.6), (1, 1, 0.0))
        B.k_scale(rig[f"lid_{s}"], f(ts + 1.2), (1, 1, 1.0))
    B.k_scale(rig["mouth"], f(ts + 1.2), (1.25, 0.35, 0.22))


def _animate_lapse(sc, rig, data, T, E, Mk, snow_obj):
    """Days pass: light cycles, snow melts, Bob's pose changes; then the first full tank (data)."""
    f = U.sec
    t0, p = T["lapse"], Mk["lapse_pause"]
    run = data["run_up"]
    n = 9  # visible day/night cycles (stylised; the date overlay carries the real dates)
    span = p - 0.6 - (t0 + 0.6)
    for k in range(n + 1):
        a = t0 + 0.6 + span * k / n
        U.ctrl_key("day", f(a), 0.0)
        if k < n:
            U.ctrl_key("day", f(a + span / n * 0.5), 1.0)
            U.ctrl_key("night_windows", f(a), 1.0)
            U.ctrl_key("night_windows", f(a + span / n * 0.5), 0.0)
    U.ctrl_key("night_windows", f(p), 1.0)
    U.ctrl_key("snow", f(t0 + 0.5), 1.0)
    U.ctrl_key("snow", f(p - 1.0), 0.0)
    # tank level from the C# hourly run between the evening and the first full tank
    times = [dt.datetime.fromisoformat(r["t"]) for r in run]
    tA, tB = times[0], times[-1]
    for r, tt in zip(run[::4], times[::4]):
        x = (tt - tA).total_seconds() / (tB - tA).total_seconds()
        U.ctrl_key("tank_level", f(t0 + 0.6 + span * x), max(0.02, r["tank_kwh"] / 35.0))
    U.ctrl_key("tank_level", f(p), 1.0)
    U.ctrl_key("tank_glow", f(p - 0.2), 0.7)
    U.ctrl_key("tank_glow", f(p + 0.6), 1.0)
    # pause: status lights amber, fronts dim, flow stops
    U.ctrl_key("paused_color", f(p + 0.8), 0.0)
    U.ctrl_key("paused_color", f(p + 1.4), 1.0)
    for k in range(4):
        U.ctrl_key(f"server_{k}", f(p + 0.8 + 0.25 * k), 1.0)
        U.ctrl_key(f"server_{k}", f(p + 1.4 + 0.25 * k), 0.25)
    U.ctrl_key("servers_on", f(p + 0.8), 1.0)
    U.ctrl_key("servers_on", f(p + 2.0), 0.15)
    U.ctrl_key("flow", f(p + 0.8), 1.0)
    U.ctrl_key("flow", f(p + 2.2), 0.0)
    U.ctrl_key("radiator", f(p - 1.0), 1.0)
    U.ctrl_key("radiator", f(p), 0.6)
    # Bob's poses during the lapse (constant keys): reading / asleep / away / tea
    root, head = rig["root"], rig["head"]
    seated = root.location.copy()
    away = (seated.x + 30, seated.y, seated.z)
    poses = ["read", "sleep", "away", "read", "sleep", "tea", "away", "read", "sleep"]
    for k, pose in enumerate(poses):
        a = f(t0 + 0.6 + span * k / n)
        B.k_loc(root, a, away if pose == "away" else seated)
        B.k_rot(head, a, x={"read": 0.3, "sleep": -0.35, "tea": 0.05, "away": 0.0}[pose], z={"read": 0.0, "sleep": 0.25, "tea": 0.0, "away": 0.0}[pose])
        B.k_rot(rig["fore_R"], a, x=-1.5 if pose in ("read", "tea") else -0.5)
        for s in ("L", "R"):
            B.k_scale(rig[f"lid_{s}"], a, (1, 1, 1.0 if pose == "sleep" else 0.0))
    B.k_loc(root, f(p), seated)
    for o in (root, head, rig["fore_R"], rig["lid_L"], rig["lid_R"]):
        for fc in o.animation_data.action.fcurves:
            for kp in fc.keyframe_points:
                if f(t0) <= kp.co[0] <= f(p):
                    kp.interpolation = "CONSTANT"


def _figures(nums, T, E):
    """Key figures as quiet light on the basement's north wall, appearing one by one."""
    coll = U.collection("figures")
    y = I.BAS["y1"] - 0.02
    z0 = I.BAS["z0"]
    items = [
        ("Reference house · Basel 2025 · simulated", None, 0.08, "Medium"),
        (nums["servers_per_house"]["display"], "servers", 0.3, "Light"),
        (nums["coverage"]["display"], "of the year's heat demand", 0.3, "Light"),
        (nums["dumped_kwh"]["display"], "heat dumped", 0.3, "Light"),
        (nums["utilisation"]["display"], "server utilisation", 0.3, "Light"),
    ]
    t0, t1 = T["figures"], E["figures"]
    xs = [-1.05, -1.05, 0.55, -1.05, 0.55]
    zs = [z0 + 2.02, z0 + 1.5, z0 + 1.5, z0 + 0.86, z0 + 0.86]
    for k, (big, small, size, weight) in enumerate(items):
        m = U.emission(f"figure {k}", U.hex_rgb("#f3e6d2"), 0.0)
        e = m.node_tree.nodes[m["emit"]].inputs["Strength"]
        U.ctrl_register(f"fig_{k}", e, scale=2.2)
        x = xs[k]
        o = U.text(f"figure big {k}", big, (x, y, zs[k]), size, m, weight=weight, rot=(math.pi / 2, 0, 0))
        o.data.align_x = "LEFT"
        if small:
            ms = U.emission(f"figure small {k}", U.hex_rgb("#c9bfae"), 0.0)
            U.ctrl_register(f"fig_{k}", ms.node_tree.nodes[ms["emit"]].inputs["Strength"], scale=1.4)
            U.text(f"figure small {k}", small, (x, y, zs[k] - 0.14), 0.075, ms, weight="Regular", rot=(math.pi / 2, 0, 0))
        for ob in [o] + ([bpy.data.objects[f"figure small {k}"]] if small else []):
            _hide(ob, 1, True)
            _hide(ob, U.sec(t0) - 1, False)
            _hide(ob, U.sec(t1) + 12, True)
        ta = t0 + 0.6 + k * 1.3
        U.ctrl_key(f"fig_{k}", 1, 0.0)
        U.ctrl_key(f"fig_{k}", U.sec(ta), 0.0)
        U.ctrl_key(f"fig_{k}", U.sec(ta + 0.9), 1.0)
        U.ctrl_key(f"fig_{k}", U.sec(t1 - 0.2), 1.0)
        U.ctrl_key(f"fig_{k}", U.sec(t1 + 0.3), 0.0)


def _map_section(sc, data, T, E, Mk):
    coll = U.collection("map sites")
    c = Cam("cam map", 35)
    t0, t1 = T["map"], E["map"]
    end0, end1 = T["end"], E["end"]
    sites = data["region"]["sites"]
    pts = [C.xy(s["lat"], s["lon"]) for s in sites]
    cx = (min(p[0] for p in pts) + max(p[0] for p in pts)) / 2
    cy = (min(p[1] for p in pts) + max(p[1] for p in pts)) / 2
    w = max(max(p[0] for p in pts) - min(p[0] for p in pts), (max(p[1] for p in pts) - min(p[1] for p in pts)) * 16 / 9)
    alt = w * 1.05  # 35 mm lens: width ~ 1.03 x distance
    pool = next(s for s in data["pool"]["sites"] if s["id"].startswith("pool"))
    px, py = C.xy(pool["lat"], pool["lon"])
    hub = C.xy(47.5582, 7.5878)  # Basel city centre: where the jobs come from (illustrative)
    # region overview -> Dreispitz -> year -> back to region
    c.key(t0, (300.0, -2600.0, 9000.0), (500.0, 200.0, 0.0))
    c.key(Mk["map_drop"] + 1.5, (cx, cy - alt * 0.32, alt * 0.95), (cx, cy, 0.0))
    c.key(Mk["map_zoom"], (cx, cy - alt * 0.3, alt * 0.97), (cx, cy, 0.0))
    c.key(Mk["map_year"], (px - 200, py - 2300, 3300), (px - 300, py + 250, 0.0))
    c.key(Mk["map_year_end"], (px - 120, py - 2050, 3000), (px - 300, py + 250, 0.0))
    c.key(end0, (px - 100, py - 2000, 2950), (px - 300, py + 250, 0.0))
    c.key(end1, (cx, cy - alt * 0.3, alt * 0.9), (cx, cy, 0.0))
    # region houses: server modules settle onto the map (falling-rack nod), then glow with load
    dl = data["region"]["daily_load"]
    order = sorted(range(len(sites)), key=lambda i: math.hypot(*pts[i]))
    for rank, i in enumerate(order):
        s = sites[i]
        x, y = pts[i]
        ctrl = f"site_{s['id']}"
        disc, halo = C.site_marker(coll, s["id"], x, y, 70.0, C.HEAT, ctrl)
        piv = bpy.data.objects.new(f"drop {s['id']}", None)
        piv.location = (x, y, 0)
        U.link(piv, coll)
        for o in (disc, halo):
            o.parent = piv
            o.location = (0, 0, o.location.z)
        ta = Mk["map_drop"] + 0.04 * rank
        B.k_loc(piv, 1, (x, y, 2500))
        B.k_loc(piv, U.sec(ta), (x, y, 2500))
        B.k_loc(piv, U.sec(ta + 0.45), (x, y, 0))
        B.k_loc(piv, U.sec(ta + 0.6), (x, y, 120))
        B.k_loc(piv, U.sec(ta + 0.75), (x, y, 0))
        B.k_scale(piv, U.sec(ta + 0.4), (0.6, 0.6, 30))
        B.k_scale(piv, U.sec(ta + 0.5), (1.2, 1.2, 0.5))
        B.k_scale(piv, U.sec(ta + 0.8), (1, 1, 1))
        jan = sum(dl[s["id"]][:31]) / 31
        U.ctrl_key(ctrl, 1, 0.0)
        U.ctrl_key(ctrl, U.sec(ta + 0.45), 0.0)
        U.ctrl_key(ctrl, U.sec(ta + 0.8), 0.25 + jan)
        U.ctrl_key(ctrl, U.sec(Mk["map_zoom"]), 0.25 + jan)
        U.ctrl_key(ctrl, U.sec(Mk["map_year"]), 0.12)  # dimmed while we look at Dreispitz
        U.ctrl_key(ctrl, U.sec(end0), 0.12)
        mean = sum(dl[s["id"]]) / len(dl[s["id"]])
        U.ctrl_key(ctrl, U.sec(end1 - 1.0), 0.25 + mean)
    # Dreispitz: 5 houses + pool, sized by servers; glow = weekly mean load; arcs = jobs placed
    daily = data["pool"]["daily"]
    ya, yb = Mk["map_year"], Mk["map_year_end"]
    for s in data["pool"]["sites"]:
        x, y = C.xy(s["lat"], s["lon"])
        is_pool = s["id"].startswith("pool")
        ctrl = f"ds_{s['id']}"
        C.site_marker(coll, f"dreispitz {s['id']}", x, y, 55.0 if is_pool else 22.0, C.HEAT, ctrl, height=3.0)
        arc = C.flow_arc(coll, f"jobs to {s['id']}", hub, (x, y), 380.0 if is_pool else 300.0, f"arc_{s['id']}", radius=7.0 if is_pool else 4.0)
        U.ctrl_key(ctrl, 1, 0.0)
        U.ctrl_key(f"arc_{s['id']}", 1, 0.0)
        U.ctrl_key(ctrl, U.sec(Mk["map_zoom"]), 0.0)
        U.ctrl_key(f"arc_{s['id']}", U.sec(Mk["map_zoom"] + 1.0), 0.0)
        load = daily[s["id"]]["load"]
        weeks = [sum(load[i:i + 7]) / len(load[i:i + 7]) for i in range(0, 364, 7)]
        for wk, v in enumerate(weeks):
            tt = ya + (yb - ya) * (wk + 0.5) / len(weeks)
            U.ctrl_key(ctrl, U.sec(tt), 0.2 + 1.4 * v)
            U.ctrl_key(f"arc_{s['id']}", U.sec(tt), 0.08 + 1.6 * v)
        U.ctrl_key(ctrl, U.sec(end0), 0.2 + 1.4 * weeks[-1])
        U.ctrl_key(ctrl, U.sec(end1), 0.4)
        U.ctrl_key(f"arc_{s['id']}", U.sec(end0 + 1.0), 0.08 + 1.6 * weeks[-1])
        U.ctrl_key(f"arc_{s['id']}", U.sec(end1 - 1.0), 0.0)
    # hub marker (where jobs enter): small cool ring
    C.site_marker(coll, "job hub", hub[0], hub[1], 30.0, C.SERIES1, "hub", height=4.0)
    U.ctrl_key("hub", 1, 0.0)
    U.ctrl_key("hub", U.sec(Mk["map_zoom"]), 0.0)
    U.ctrl_key("hub", U.sec(Mk["map_zoom"] + 1.0), 0.8)
    U.ctrl_key("hub", U.sec(end1 - 1.0), 0.0)
    return c
