"""Build the intro film in Blender and render it.

Run with Blender 4.x (no add-ons, no downloaded assets):
  blender -b --factory-startup -P video/blender/render.py -- --preset preview
  blender -b --factory-startup -P video/blender/render.py -- --preset final
or with the `bpy` module (pip install bpy==4.5.*, Python 3.11):
  python video/blender/render.py --preset preview

Options:
  --preset preview|final   960x540 low samples / 1920x1080 production (Cycles)
  --preset eevee-preview|eevee   the same with Eevee, for GPUs Cycles cannot use (Intel Iris Xe)
  --stills                 render only the look-development stills (STILLS below)
  --step N                 render every Nth frame (preview default 2: 12 fps, held by post.py)
  --frames A-B             limit to a frame range
  --save-blend             also save the built scene to out/<preset>/film.blend
Frames go to video/blender/out/<preset>/frames/####.png; existing frames are skipped (resumable).
Then run post.py to add type and encode the video.
"""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402

from film import shots  # noqa: E402
from film import util as U  # noqa: E402

# Look-development stills (seconds), one or two per beat.
STILLS = [3.0, 10.0, 18.5, 24.0, 33.0, 36.0, 43.0, 46.5, 51.0, 56.0, 63.5, 71.0, 76.0, 83.0, 92.0, 100.0, 110.0]

ANCHORS = {
    "rack": lambda d: (-3.05, 8.92 + 2.9, -0.35),
    "tank": lambda d: (-1.75, 8.92 + 5.6 - 0.65, 0.95),
    "radiator": lambda d: (-2.4, 8.92 + 0.17, 1.9),
}


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    o = {"preset": "preview", "stills": False, "step": None, "frames": None, "save_blend": False}
    i = 0
    while i < len(a):
        k = a[i]
        if k == "--preset":
            o["preset"] = a[i + 1]; i += 1
        elif k == "--stills":
            o["stills"] = True
        elif k == "--step":
            o["step"] = int(a[i + 1]); i += 1
        elif k == "--frames":
            x, y = a[i + 1].split("-"); o["frames"] = (int(x), int(y)); i += 1
        elif k == "--at":
            o["at"] = [float(x) for x in a[i + 1].split(",")]; o["stills"] = True; i += 1
        elif k == "--save-blend":
            o["save_blend"] = True
        i += 1
    if o["step"] is None:
        o["step"] = 2 if "preview" in o["preset"] else 1
    return o


def export_anchors(sc, data, out: Path):
    """2D positions (0..1, origin bottom-left) of labelled points for every frame; used by post.py."""
    from film import city as C
    pts = {k: f(data) for k, f in ANCHORS.items()}
    for s in data["pool"]["sites"]:
        x, y = C.xy(s["lat"], s["lon"])
        pts[s["id"]] = (x, y, 3.0)
    pts["hub"] = (*C.xy(47.5582, 7.5878), 4.0)
    pts["bob_house"] = (0.0, 9.0, 4.0)
    res = {}
    for f in range(sc.frame_start, sc.frame_end + 1):
        sc.frame_set(f)
        cam = sc.camera
        row = {}
        for k, p in pts.items():
            v = world_to_camera_view(sc, cam, __import__("mathutils").Vector(p))
            if v.z > 0:
                row[k] = (round(v.x, 4), round(v.y, 4))
        res[f] = row
    out.write_text(json.dumps(res, separators=(",", ":")))


def main():
    o = args()
    t = time.time()
    sc, tl, data = shots.build(o["preset"])
    print(f"built scene in {time.time() - t:.1f} s; frames {sc.frame_start}-{sc.frame_end}", flush=True)
    out = HERE / "out" / o["preset"]
    (out / "frames").mkdir(parents=True, exist_ok=True)
    if o["save_blend"]:
        bpy.ops.wm.save_as_mainfile(filepath=str(out / "film.blend"))
    if not (out / "anchors.json").exists() or (o["stills"] and not o.get("at")):
        export_anchors(sc, data, out / "anchors.json")
    if o["stills"]:
        frames = [U.sec(s) for s in o.get("at", STILLS)]
        target = out / "stills"
        target.mkdir(exist_ok=True)
    else:
        a, b = o["frames"] or (sc.frame_start, sc.frame_end)
        frames = list(range(a, b + 1, o["step"]))
        target = out / "frames"
    todo = [f for f in frames if o.get("at") or not (target / f"{f:04d}.png").exists()]
    print(f"{len(todo)} frames to render", flush=True)
    for k, f in enumerate(todo):
        t1 = time.time()
        sc.frame_set(f)
        sc.render.filepath = str(target / f"{f:04d}.png")
        bpy.ops.render.render(write_still=True)
        print(f"frame {f} ({k + 1}/{len(todo)}) {time.time() - t1:.1f} s", flush=True)
    print("done", flush=True)


if __name__ == "__main__":
    main()
