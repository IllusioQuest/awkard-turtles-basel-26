"""2D pass: colour grade, captions, map UI and chart over the rendered frames, then encode.

  python3 video/blender/post.py --preset preview     # -> out/preview/digitalheat-intro-preview.mp4
  python3 video/blender/post.py --preset final       # -> out/final/digitalheat-intro.mp4
  python3 video/blender/post.py --preset preview --stills   # captions on the look-dev stills

Needs Pillow and ffmpeg. Missing frames (e.g. preview rendered every 2nd frame) are filled with the
previous rendered frame. Every number shown is read from numbers.json (no literals here).
"""
from __future__ import annotations

import datetime as dt
import json
import math
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
TL = json.loads((HERE / "timeline.json").read_text())
NUM = json.loads((HERE / "numbers.json").read_text())
FPS = TL["fps"]
B = {k: (v["start"], v["end"]) for k, v in TL["beats"].items()}
MK = TL["marks"]
INK, INK2, MUTED = (240, 239, 236), (195, 194, 183), (137, 135, 129)
SERIES1, HEAT = (57, 135, 229), (255, 138, 42)
FONT_DIRS = ["/usr/share/fonts/opentype/inter", "/usr/share/fonts/truetype/inter",
             str(Path.home() / "AppData/Local/Microsoft/Windows/Fonts"), "C:/Windows/Fonts",
             "/Library/Fonts", str(Path.home() / "Library/Fonts")]
FALLBACK = {"Light": ["segoeuil.ttf", "HelveticaNeue.ttc", "DejaVuSans.ttf"], "Regular": ["segoeui.ttf", "Helvetica.ttc", "DejaVuSans.ttf"],
            "Medium": ["seguisb.ttf", "segoeui.ttf", "Helvetica.ttc", "DejaVuSans.ttf"]}


def font(weight, px):
    for d in FONT_DIRS:
        for name in (f"Inter-{weight}.otf", f"Inter-{weight}.ttf"):
            p = Path(d) / name
            if p.exists():
                return ImageFont.truetype(str(p), px)
    for name in FALLBACK.get(weight, FALLBACK["Regular"]):
        for d in [""] + FONT_DIRS:
            try:
                return ImageFont.truetype(str(Path(d) / name) if d else name, px)
            except OSError:
                continue
    return ImageFont.load_default(px)


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def fade(t, a, b, fin=0.5, fout=0.5):
    """Opacity of an item visible from a to b with fades."""
    return min(smooth((t - a) / fin), smooth((b - t) / fout))


def n(key):
    return NUM[key]["display"]


def date_at(t):
    """Story date for the time-lapse ticker: evening -> first full tank, from numbers.json."""
    a, p = B["lapse"][0] + 0.6, MK["lapse_pause"] - 0.6
    d0 = dt.datetime.fromisoformat("2025-01-13T19:00")
    d1 = dt.datetime.fromisoformat(NUM["first_full_tank"]["value"])
    x = smooth((t - a) / (p - a)) if t < p else 1.0
    return d0 + (d1 - d0) * x


def warmth(t):
    """-1 cold blue ... +1 warm amber grade."""
    if t < B["dive"][0] + 3:
        return -0.55 if t < B["dc"][0] or t >= B["dc"][1] else -0.35
    if t < MK["dive_room"]:
        return -0.2
    if t < B["rise"][0]:
        return 0.35 * smooth((t - MK["dive_room"]) / 3.0)
    return 0.0


class Overlay:
    def __init__(self, w, h):
        self.w, self.h = w, h
        s = h / 1080
        self.s = s
        self.f_small = font("Medium", int(26 * s))
        self.f_cap = font("Regular", int(36 * s))
        self.f_big = font("Light", int(54 * s))
        self.f_label = font("Medium", int(24 * s))
        self.f_tiny = font("Regular", int(21 * s))

    def text(self, d, xy, s, f, col, a, anchor="la"):
        if a <= 0.01:
            return
        d.text(xy, s, font=f, fill=(*col, int(255 * a)), anchor=anchor)

    def caption(self, d, t, lines, a, b, pos="bl"):
        o = fade(t, a, b, 0.6, 0.6)
        if o <= 0:
            return
        s = self.s
        x = int(96 * s)
        y = self.h - int(90 * s) - len(lines) * int(46 * s)
        for k, (txt, big) in enumerate(lines):
            self.text(d, (x, y + k * int(46 * s)), txt, self.f_cap if big else self.f_small, INK if big else INK2, o)

    def label(self, d, anchors, key, txt, sub, a, color=INK):
        if key not in anchors or a <= 0.01:
            return
        x, y = anchors[key]
        px, py = x * self.w, (1 - y) * self.h
        s = self.s
        lx, ly = px + 26 * s, py - 64 * s
        d.line([(px, py), (lx - 6 * s, ly + 18 * s)], fill=(*MUTED, int(200 * a)), width=max(1, int(2 * s)))
        self.text(d, (lx, ly), txt, self.f_label, color, a)
        if sub:
            self.text(d, (lx, ly + 30 * s), sub, self.f_tiny, INK2, a)

    def map_chrome(self, d, t, a):
        if a <= 0.01:
            return
        s = self.s
        x, y = int(48 * s), int(44 * s)
        self.text(d, (x, y), "DigitalHeat · Basel", self.f_label, INK, a)
        on = {"HEAT": True, "JOBS": t >= MK["map_zoom"], "FLOW": t >= MK["map_zoom"] + 1}
        cx = x
        for k, (name, act) in enumerate(on.items()):
            w = int(86 * s)
            col = (HEAT if name == "HEAT" else SERIES1) if act else MUTED
            d.rounded_rectangle([cx, y + 40 * s, cx + w, y + 72 * s], radius=int(16 * s), outline=(*col, int(255 * a * (1 if act else 0.5))), width=max(1, int(2 * s)))
            self.text(d, (cx + w / 2, y + 56 * s), name, self.f_tiny, col if act else MUTED, a, anchor="mm")
            cx += w + int(12 * s)

    def month_chart(self, d, t, a):
        """Monthly mean server load, 5 houses vs pool (FleetRun dreispitz-5-pool)."""
        if a <= 0.01:
            return
        s = self.s
        W, H = int(420 * s), int(170 * s)
        x0, y0 = self.w - W - int(64 * s), self.h - H - int(90 * s)
        d.rounded_rectangle([x0 - 20 * s, y0 - 52 * s, x0 + W + 20 * s, y0 + H + 44 * s], radius=int(12 * s), fill=(13, 13, 13, int(200 * a)))
        self.text(d, (x0, y0 - 40 * s), "Mean server load per month", self.f_tiny, INK2, a)
        houses = NUM["pool_month_load_houses"]["value"]
        pool = NUM["pool_month_load_pool"]["value"]
        ya, yb = MK["map_year"], MK["map_year_end"]
        prog = max(0.0, min(1.0, (t - ya) / (yb - ya)))
        mx = 0.6
        for k in range(4):
            gy = y0 + H - H * (k * 0.2) / mx
            d.line([(x0, gy), (x0 + W, gy)], fill=(44, 44, 42, int(255 * a)), width=1)
            self.text(d, (x0 - 6 * s, gy), f"{int(k * 20)} %", self.f_tiny, MUTED, a, anchor="rm")
        for series, col, name in ((houses, SERIES1, "5 houses"), (pool, HEAT, "indoor pool")):
            pts = [(x0 + W * (m + 0.5) / 12, y0 + H - H * v / mx) for m, v in enumerate(series)]
            upto = prog * 12
            vis = [p for m, p in enumerate(pts) if m + 0.5 <= upto + 0.001]
            if len(vis) >= 2:
                d.line(vis, fill=(*col, int(255 * a)), width=max(2, int(3 * s)), joint="curve")
            if vis:
                d.ellipse([vis[-1][0] - 4 * s, vis[-1][1] - 4 * s, vis[-1][0] + 4 * s, vis[-1][1] + 4 * s], fill=(*col, int(255 * a)))
        lx = x0
        for col, name in ((SERIES1, "5 houses"), (HEAT, "indoor pool")):
            d.line([(lx, y0 + H + 26 * s), (lx + 22 * s, y0 + H + 26 * s)], fill=(*col, int(255 * a)), width=max(2, int(3 * s)))
            self.text(d, (lx + 30 * s, y0 + H + 26 * s), name, self.f_tiny, INK2, a, anchor="lm")
            lx += int(150 * s)
        for m, lab in enumerate("JFMAMJJASOND"):
            self.text(d, (x0 + W * (m + 0.5) / 12, y0 + H + 8 * s), lab, self.f_tiny, MUTED, a, anchor="mt")

    def draw(self, img, t, anchors):
        ov = Image.new("RGBA", img.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(ov)
        # 1 street: place, time, temperature (case weather)
        self.caption(d, t, [("Basel · Monday, 13 January 2025 · 19:00", True), (f"{n('evening_temperature_c')} outside", False)], 1.0, B["street"][1] - 0.8)
        # 3 data centre
        self.caption(d, t, [("A data centre nearby. Its heat goes into the night.", True)], B["dc"][0] + 1.0, B["dc"][1] - 0.6)
        # 4 dive: servers and heat path (case server assumptions)
        a = MK["dive_alive"] + 1.0
        self.caption(d, t, [(f"{n('servers_per_house')} servers · {n('server_electric_kw')} each", True), ("paid computing jobs, in Bob's basement", False)], a, MK["dive_flow"] + 1.5)
        self.caption(d, t, [(f"{n('heat_recovery_share')} of their heat goes into the water", True), ("to the radiators first, the tank stores the rest", False)], MK["dive_flow"] + 1.8, MK["dive_up"] + 1.2)
        # 5 lapse: date ticker, then the pause
        o = fade(t, B["lapse"][0] + 0.3, MK["lapse_pause"] + 0.2, 0.4, 0.3)
        if o > 0:
            dd = date_at(t)
            self.text(d, (self.w - int(96 * self.s), int(80 * self.s)), f"{dd.day} {dd.strftime('%B')} 2025", self.f_big, INK, o, anchor="ra")
        self.caption(d, t, [(f"{n('first_full_tank')} 2025 · tank full ({n('tank_kwh')})", True), ("The servers pause by themselves. The house decides.", False)], MK["lapse_pause"] + 0.8, B["lapse"][1] + 0.3)
        # 6 figures: source line
        o = fade(t, B["figures"][0] + 0.8, B["figures"][1] - 0.3)
        self.text(d, (int(96 * self.s), self.h - int(60 * self.s)), f"C# year model of the case reference house · break-even {n('break_even_chf_per_server_hour')} per server-hour (electricity minus heat value) · missed deadlines not defined: unlimited jobs", self.f_tiny, MUTED, o)
        # 7/8 map
        mo = smooth((t - MK["rise_map"] - 1.5) / 1.5) * (1 - smooth((t - B["end"][1] + 0.5) / 0.5))
        self.map_chrome(d, t, mo)
        self.caption(d, t, [(f"{n('region_houses')} simulated houses in the Basel region", True), (f"{n('region_servers_per_house')} servers each · FleetRun basel-region-100 · positions synthetic", False)], MK["map_drop"] + 1.2, MK["map_zoom"] + 0.6)
        lo = fade(t, MK["map_zoom"] + 1.5, B["end"][0] + 0.5)
        self.label(d, anchors, "pool-dreispitz", "Indoor pool", f"{n('pool_servers')} servers · {n('pool_annual_heat_kwh')} heat a year", lo, HEAT)
        self.label(d, anchors, "h001", f"{n('pool_houses')} houses", "4 servers each", lo * fade(t, MK["map_zoom"] + 2.0, MK["map_year"] + 3.0), INK)
        self.label(d, anchors, "hub", "Jobs", "from customers", lo * fade(t, MK["map_zoom"] + 2.5, MK["map_year"] + 3.5), SERIES1)
        if MK["map_year"] - 0.5 <= t <= B["end"][0] + 0.5:
            o = fade(t, MK["map_year"] - 0.5, B["end"][0] + 0.5, 0.5, 0.5)
            prog = max(0.0, min(1.0, (t - MK["map_year"]) / (MK["map_year_end"] - MK["map_year"])))
            day = dt.date(2025, 1, 1) + dt.timedelta(days=int(prog * 364))
            self.text(d, (self.w - int(64 * self.s), int(52 * self.s)), day.strftime("%B 2025"), self.f_big, INK, o, anchor="ra")
            self.month_chart(d, t, o)
        self.caption(d, t, [("Summer: the work moves to the pool.", True), ("The heat stays where it is made. Example: FleetRun dreispitz-5-pool, 0 kWh dumped", False)], MK["map_year_end"] - 4.5, B["end"][0] + 1.5)
        self.caption(d, t, [("Basel 2025 · simulated", False)], B["end"][0] + 1.5, B["end"][1] - 0.2)
        return Image.alpha_composite(img.convert("RGBA"), ov).convert("RGB")


def grade(img, t):
    w = warmth(t)
    if abs(w) < 1e-3:
        return img
    r, g, b = img.split()
    if w > 0:
        r = r.point(lambda v: min(255, int(v * (1 + 0.10 * w))))
        b = b.point(lambda v: int(v * (1 - 0.14 * w)))
    else:
        w = -w
        r = r.point(lambda v: int(v * (1 - 0.10 * w)))
        b = b.point(lambda v: min(255, int(v * (1 + 0.12 * w))))
    return Image.merge("RGB", (r, g, b))


def main():
    preset = sys.argv[sys.argv.index("--preset") + 1] if "--preset" in sys.argv else "preview"
    out = HERE / "out" / preset
    anchors = json.loads((out / "anchors.json").read_text()) if (out / "anchors.json").exists() else {}
    if "--stills" in sys.argv:
        (out / "stills-post").mkdir(exist_ok=True)
        for p in sorted((out / "stills").glob("*.png")):
            f = int(p.stem)
            t = (f - 1) / FPS
            img = Image.open(p).convert("RGB")
            ov = Overlay(*img.size)
            ov.draw(grade(img, t), t, anchors.get(str(f), {})).save(out / "stills-post" / p.name)
        print("stills-post written")
        return
    frames_dir = out / "frames"
    last = int(round(TL["duration"] * FPS))
    first = Image.open(next(iter(sorted(frames_dir.glob("*.png"))))).convert("RGB")
    w, h = first.size
    name = "digitalheat-intro-preview.mp4" if preset == "preview" else "digitalheat-intro.mp4"
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{w}x{h}", "-r", str(FPS), "-i", "-",
           "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20" if preset == "preview" else "16", "-preset", "medium", "-movflags", "+faststart", str(out / name)]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    ov = Overlay(w, h)
    cur = first
    for f in range(1, last + 1):
        p = frames_dir / f"{f:04d}.png"
        if p.exists():
            cur = Image.open(p).convert("RGB")
        t = (f - 1) / FPS
        frame = ov.draw(grade(cur, t), t, anchors.get(str(f), {}))
        proc.stdin.write(frame.tobytes())
    proc.stdin.close()
    proc.wait()
    print("wrote", out / name)


if __name__ == "__main__":
    main()
