"""Sound for the intro film: voice-over, score and sound design, mixed and put onto the video.

  python video/blender/audio/make_audio.py --video path/to/film.mp4
  -> video/blender/out/audio/{voiceover.wav, score.wav, mix.wav} and <video>-with-sound.mp4

Everything is generated here: the voice with Piper (offline neural TTS, voice downloaded once from
the rhasspy/piper GitHub release into video/blender/.cache/voices), the score and effects with numpy
(additive synthesis, filtered noise, convolution reverb). No music samples, no downloaded sounds.
Timing comes from ../timeline.json (beats, marks) and voiceover.json (lines).
Needs: Python 3.10/3.11 with `pip install piper-tts numpy scipy`, plus ffmpeg.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tarfile
import tempfile
import wave
from pathlib import Path

import numpy as np
from scipy import signal

HERE = Path(__file__).resolve().parent
FILM = HERE.parent
TL = json.loads((FILM / "timeline.json").read_text())
VO = json.loads((HERE / "voiceover.json").read_text())
MK = TL["marks"]
BEAT = {k: (v["start"], v["end"]) for k, v in TL["beats"].items()}
SR = 48000
DUR = TL["duration"]
N = int(DUR * SR)
RNG = np.random.default_rng(20261122)

VOICES = {
    # Piper voices from https://github.com/rhasspy/piper/releases/tag/v0.0.2
    "ryan": ("voice-en-us-ryan-high.tar.gz", "en-us-ryan-high.onnx", None, "RyanSpeech, CC BY-NC-SA 4.0"),
    "libritts": ("voice-en-us-libritts-high.tar.gz", "en-us-libritts-high.onnx", 14, "LibriTTS, CC BY 4.0"),
}
RELEASE = "https://github.com/rhasspy/piper/releases/download/v0.0.2/"


# ---------------------------------------------------------------------------------------------- helpers
def tt(d):
    return np.arange(int(d * SR)) / SR


def place(buf, sig, at, gain=1.0, pan=0.0):
    """Add mono or stereo `sig` into stereo `buf` at time `at` (s) with equal-power pan (-1..1)."""
    i = int(at * SR)
    if i >= buf.shape[0] or i + len(sig) <= 0:
        return
    if sig.ndim == 1:
        a = (pan + 1) * np.pi / 4
        sig = np.stack([sig * np.cos(a), sig * np.sin(a)], 1)
    j0 = max(0, -i)
    j1 = min(len(sig), buf.shape[0] - i)
    buf[i + j0:i + j1] += gain * sig[j0:j1]


def env(n, a, r, hold=None):
    """Attack/release envelope over n samples (a, r in seconds)."""
    e = np.ones(n)
    na, nr = min(n, int(a * SR)), min(n, int(r * SR))
    if na:
        e[:na] = np.linspace(0, 1, na) ** 1.5
    if nr:
        e[-nr:] *= np.linspace(1, 0, nr) ** 1.5
    return e


def sos(kind, f, order=2):
    f = np.clip(np.asarray(f, dtype=float) / (SR / 2), 1e-4, 0.999)
    return signal.butter(order, f if f.ndim else float(f), btype=kind, output="sos")


def filt(x, kind, f, order=2):
    return signal.sosfilt(sos(kind, f, order), x, axis=0)


def noise(d):
    return RNG.standard_normal(int(d * SR))


def pink(d):
    x = noise(d)
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, x) * 4


def sweep_noise(d, f0, f1, q=1.5, blocks=60):
    """Band-passed noise whose centre moves from f0 to f1 (block-wise, overlap-added)."""
    n = int(d * SR)
    out = np.zeros(n)
    x = noise(d)
    hop = max(1, n // blocks)
    win = np.hanning(2 * hop)
    for k in range(0, n, hop):
        fc = f0 * (f1 / f0) ** (k / max(1, n - 1))
        lo, hi = fc / (1 + 1 / q), fc * (1 + 1 / q)
        seg = x[max(0, k - hop):k + hop]
        y = signal.sosfilt(sos("bandpass", [max(20, lo), min(SR / 2 - 100, hi)]), seg)
        w = win[:len(y)]
        out[max(0, k - hop):max(0, k - hop) + len(y)] += y * w
    return out


def osc(freq, d, shape="sine", harmonics=8):
    """freq: number or array (Hz) per sample."""
    n = int(d * SR)
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n,))
    ph = 2 * np.pi * np.cumsum(f) / SR
    if shape == "sine":
        return np.sin(ph)
    if shape == "soft":  # warm, few harmonics (pad/strings-ish)
        return sum(np.sin(k * ph) / k ** 1.6 for k in range(1, harmonics + 1))
    if shape == "square":
        return sum(np.sin(k * ph) / k for k in range(1, harmonics * 2, 2))
    raise ValueError(shape)


def note(name):
    names = {"C": -9, "C#": -8, "Db": -8, "D": -7, "D#": -6, "Eb": -6, "E": -5, "F": -4, "F#": -3, "Gb": -3,
             "G": -2, "G#": -1, "Ab": -1, "A": 0, "A#": 1, "Bb": 1, "B": 2}
    p, o = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[p] + 12 * (o - 4)) / 12)


def bell(f, d=3.0, bright=1.0):
    t = tt(d)
    parts = [(1, 1.0, 2.2), (2.0, 0.35 * bright, 3.5), (3.01, 0.18 * bright, 5), (4.2, 0.1 * bright, 7), (5.4, 0.05 * bright, 9)]
    s = sum(a * np.sin(2 * np.pi * f * m * t) * np.exp(-t * dec / d * 2) for m, a, dec in parts)
    return s * env(len(t), 0.003, 0.2)


def piano(f, d=2.5):
    t = tt(d)
    s = sum((0.6 ** k) * np.sin(2 * np.pi * f * (k + 1) * t * (1 + 0.0004 * k * k)) * np.exp(-t * (1.6 + 0.9 * k)) for k in range(6))
    return s * env(len(t), 0.004, 0.3)


def pad(freqs, d, a=1.5, r=2.0, bright=900, detune=0.004):
    """Soft chord: detuned warm oscillators, low-passed, slow envelope; stereo."""
    n = int(d * SR)
    L = np.zeros(n)
    R = np.zeros(n)
    for f in freqs:
        for k, dt_ in enumerate((-detune, 0, detune)):
            lfo = 1 + 0.0015 * np.sin(2 * np.pi * (0.13 + 0.05 * k) * tt(d) + k)
            s = osc(f * (1 + dt_) * lfo, d, "soft", 6)
            (L if k != 2 else R)[:] += s * (1.0 if k == 1 else 0.7)
            if k == 1:
                R += s
    st = np.stack([L, R], 1) / (3 * max(1, len(freqs)))
    st = filt(st, "lowpass", bright, 2)
    return st * env(n, a, r)[:, None]


def reverb(x, seconds=2.6, wet=0.25, pre=0.02, bright=6000):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = np.stack([RNG.standard_normal(n), RNG.standard_normal(n)], 1) * np.exp(-t * 6.9 / seconds)[:, None]
    ir = filt(ir, "lowpass", bright)
    ir[: int(pre * SR)] = 0
    ir /= np.sqrt((ir ** 2).sum(0))
    y = np.stack([signal.fftconvolve(x[:, c], ir[:, c])[: len(x)] for c in range(2)], 1)
    return x * (1 - wet) + y * wet * 1.4


# ---------------------------------------------------------------------------------------------- voice
def ensure_voice(name):
    archive, onnx, speaker, lic = VOICES[name]
    d = FILM / ".cache" / "voices" / name
    model = d / onnx
    if not model.exists():
        d.mkdir(parents=True, exist_ok=True)
        tgz = d / archive
        print(f"downloading voice {name} ...", flush=True)
        subprocess.run(["curl", "-sL", "--fail", "-o", str(tgz), RELEASE + archive], check=True)
        with tarfile.open(tgz) as tf:
            for m in tf.getmembers():
                if m.isfile() and Path(m.name).name in (onnx, onnx + ".json", "MODEL_CARD"):
                    m.name = Path(m.name).name
                    tf.extract(m, d)
        tgz.unlink()
    return model, speaker, lic


def read_wav(p):
    with wave.open(str(p)) as w:
        sr = w.getframerate()
        x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(float) / 32768
        if w.getnchannels() == 2:
            x = x.reshape(-1, 2).mean(1)
    return x, sr


def tts(text, model, speaker, length):
    with tempfile.TemporaryDirectory() as td:
        out = Path(td) / "l.wav"
        cmd = [sys.executable, "-m", "piper", "-m", str(model), "-f", str(out), "--length-scale", f"{length:.3f}",
               "--sentence-silence", "0.32", "--noise-scale", "0.55", "--noise-w-scale", "0.7"]
        if speaker is not None:
            cmd += ["-s", str(speaker)]
        subprocess.run(cmd, input=text.encode(), check=True, capture_output=True)
        x, sr = read_wav(out)
    x = signal.resample_poly(x, SR, sr)
    nz = np.flatnonzero(np.abs(x) > 0.01)
    return x[max(0, nz[0] - 200): nz[-1] + 2000] if len(nz) else x


def voiceover(voice):
    model, speaker, lic = ensure_voice(voice)
    buf = np.zeros((N, 2))
    report = []
    for ln in VO["lines"]:
        say = ln.get("say", ln["text"])
        room = ln["until"] - ln["at"]
        length = 1.06
        x = tts(say, model, speaker, length)
        while len(x) / SR > room and length > 0.86:
            length -= 0.04
            x = tts(say, model, speaker, length)
        x = filt(x, "highpass", 75)
        report.append((ln["at"], len(x) / SR, room, length, ln["text"]))
        place(buf, x, ln["at"], 1.0, 0.0)
    # gentle presence + a little room, then level
    buf = reverb(buf, 0.9, wet=0.10, bright=7000)
    buf /= np.max(np.abs(buf)) + 1e-9
    return buf * 0.9, report, lic


# ---------------------------------------------------------------------------------------------- score + sound design
def score():
    mus = np.zeros((N, 2))   # music (gets reverb, ducked under the voice)
    sfx = np.zeros((N, 2))   # effects (dry-ish)
    b = BEAT

    # ---- winter outside: wind + sparse bells over a cold, high pad -------------------------------
    w = pink(b["dc"][1] + 1)
    w = filt(w, "bandpass", [180, 1400])
    gust = 0.55 + 0.45 * np.sin(2 * np.pi * tt(len(w) / SR) * 0.11) * np.sin(2 * np.pi * tt(len(w) / SR) * 0.037 + 1)
    wind = w * gust
    inside = np.ones(len(w))
    i0, i1 = int(b["cold"][0] * SR), int(b["dc"][0] * SR)
    inside[i0:i1] = 0.25  # muffled behind the window
    wind_mix = np.where(inside < 1, filt(wind, "lowpass", 500) * 0.5, wind)
    place(sfx, wind_mix * env(len(w), 2.5, 2.0), 0.0, 0.10, -0.2)
    place(sfx, np.roll(wind_mix, 9000) * env(len(w), 2.5, 2.0), 0.0, 0.08, 0.3)
    D = note
    place(mus, pad([D("D4"), D("A4"), D("E5"), D("F5")], b["dc"][0] + 1.5, a=4, r=3, bright=1800), 0.0, 0.32)
    for at, n in ((1.8, "A5"), (5.6, "E5"), (9.4, "D6"), (12.8, "F5"), (16.0, "C6"), (19.5, "A5")):
        place(mus, bell(D(n), 4.0, 0.7), at, 0.10, RNG.uniform(-0.5, 0.5))
    # snow sparkle (tiny high ticks) on the street
    for k in range(40):
        at = RNG.uniform(0.2, b["street"][1])
        place(sfx, bell(RNG.uniform(3500, 6500), 0.25, 0.2), at, 0.012, RNG.uniform(-0.8, 0.8))

    # ---- Bob: light humour ------------------------------------------------------------------------
    touch = MK["cold_touch"] + 0.72
    place(sfx, bell(1480, 0.6, 1.5) * 0.6 + bell(2210, 0.6, 1.0) * 0.3, touch, 0.16, -0.3)           # tink: cold radiator
    place(sfx, sweep_noise(0.22, 900, 4200, q=2) * env(int(0.22 * SR), 0.01, 0.12), touch + 0.8, 0.22, -0.2)  # hand snaps back
    op = MK["cold_open"]
    tear = sweep_noise(0.7, 2500, 5000, q=1.2) * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 23 * tt(0.7))))
    place(sfx, tear * env(len(tear), 0.02, 0.2), op + 0.35, 0.10, -0.2)                             # envelope tears open
    slide = filt(noise(0.9), "bandpass", [1500, 6000]) * env(int(0.9 * SR), 0.1, 0.4)
    place(sfx, slide, op + 1.05, 0.06, -0.2)                                                         # letter slides out
    rx = MK["cold_react"]
    for k, (n0, n1) in enumerate(((D("F3"), D("E3")), (D("E3"), D("C3")))):                         # "uh-oh"
        d = 0.42 if k == 0 else 0.75
        f = np.linspace(n0, n1, int(d * SR))
        s = osc(f, d, "square", 4) * env(int(d * SR), 0.03, 0.25)
        place(mus, filt(s, "lowpass", 1400), rx + 0.15 + k * 0.45, 0.09, 0.1)
    for k in range(int((b["cold"][1] - rx - 0.4) * 11)):                                            # teeth chatter
        at = rx + 0.5 + k / 11 + RNG.uniform(-0.01, 0.01)
        place(sfx, filt(noise(0.03), "bandpass", [1800, 4200]) * env(int(0.03 * SR), 0.001, 0.02), at, 0.035, -0.1)

    # ---- data centre: rumble, fan hum, rising heat ----------------------------------------------
    d0, d1 = b["dc"]
    dd = d1 - d0 + 1.2
    rum = filt(pink(dd), "lowpass", 160) * 0.7 + osc(50, dd) * 0.08 + osc(100, dd) * 0.05 + osc(D("D2"), dd, "soft", 5) * 0.12
    place(sfx, rum * env(int(dd * SR), 0.5, 1.0), d0, 0.28)
    fanb = filt(pink(dd), "bandpass", [300, 2400]) * (1 + 0.2 * np.sin(2 * np.pi * 7.3 * tt(dd)))
    place(sfx, fanb * env(int(dd * SR), 0.6, 1.0), d0, 0.05, 0.2)
    rise = sweep_noise(dd, 250, 1800, q=2.0) * env(int(dd * SR), 2.5, 1.0)
    place(sfx, rise, d0, 0.08, -0.1)
    place(mus, pad([D("D2"), D("A2"), D("D3"), D("F3")], dd + 1, a=1.0, r=2.0, bright=700), d0 - 0.3, 0.45)

    # ---- dive: whoosh down, servers wake, water flows, warmth arrives ---------------------------
    dv = MK["dive_down"]
    wd = sweep_noise(1.3, 3000, 180, q=1.5) * env(int(1.3 * SR), 0.5, 0.5)
    place(sfx, wd, dv - 0.2, 0.30)
    place(sfx, filt(noise(0.08), "lowpass", 180) * env(int(0.08 * SR), 0.002, 0.07), dv + 0.95, 0.5)  # soft landing thump
    al = MK["dive_alive"]
    pause = MK["lapse_pause"]
    for k in range(4):                                                                               # 4 servers spin up
        at = al + 0.5 * k
        dur = pause + 2.5 - at
        f = 85 + 115 * (1 - np.exp(-tt(dur) / 0.7))
        stop = int((pause + 0.8 + 0.25 * k - at) * SR)
        f[stop:] = f[stop] * np.exp(-np.arange(len(f) - stop) / SR / 0.5) + 20
        amp = np.clip(tt(dur) / 0.6, 0, 1)
        amp[stop:] *= np.exp(-np.arange(len(amp) - stop) / SR / 0.45)
        whirr = osc(f, dur, "soft", 4) * 0.25 + filt(noise(dur), "bandpass", [600, 3500]) * 0.25
        place(sfx, whirr * amp, at, 0.06, -0.5 + 0.12 * k)
        place(sfx, bell(1760 + 110 * k, 0.4, 0.3), at + 0.05, 0.05, -0.45)                       # LED blip
    fl = MK["dive_flow"]
    fd = MK["dive_up"] + 2.0 - fl
    water = filt(pink(fd), "bandpass", [250, 1200]) * (0.7 + 0.3 * np.sin(2 * np.pi * 1.7 * tt(fd)) * np.sin(2 * np.pi * 0.43 * tt(fd)))
    for k in range(int(fd * 6)):                                                                     # small bubbles
        place(sfx, np.sin(2 * np.pi * np.linspace(RNG.uniform(500, 900), RNG.uniform(900, 1500), int(0.05 * SR)) * tt(0.05)) * env(int(0.05 * SR), 0.003, 0.04),
              fl + RNG.uniform(0, fd), 0.02, RNG.uniform(-0.6, 0.6))
    place(sfx, water * env(len(water), 1.0, 1.5), fl, 0.10, -0.2)
    up = MK["dive_up"]
    place(sfx, sweep_noise(1.6, 200, 2600, q=1.5) * env(int(1.6 * SR), 0.6, 0.6), up + 0.2, 0.22)
    # warm music: Bb -> F (add9), then piano motif while Bob relaxes
    place(mus, pad([D("Bb2"), D("F3"), D("D4"), D("A4")], up - fl + 1.5, a=1.5, r=1.5, bright=1100), fl, 0.40)
    room = MK["dive_room"]
    place(mus, pad([D("F2"), D("C3"), D("A3"), D("G4"), D("C5")], b["lapse"][1] - room + 1.5, a=2.0, r=2.5, bright=1600), room - 0.8, 0.45)
    for k, n in enumerate(["C5", "A4", "F4", "G4", "A4", "C5", "D5", "C5"]):
        place(mus, piano(D(n), 2.8), room + 0.6 + k * 0.62 + (0.3 if k >= 4 else 0), 0.17, -0.15 + 0.05 * k)
    place(sfx, filt(noise(0.06), "bandpass", [700, 2500]) * env(int(0.06 * SR), 0.002, 0.05), room + 3.4, 0.10, 0.2)  # hat off
    sigh = filt(pink(1.2), "bandpass", [300, 900]) * env(int(1.2 * SR), 0.3, 0.8)                  # contented breath out
    place(sfx, sigh, room + 4.4, 0.05, 0.1)

    # ---- time-lapse: accelerating clock, day swells, then the pause ------------------------------
    l0 = b["lapse"][0]
    t = l0 + 0.3
    while t < pause - 0.3:
        x = (t - l0) / (pause - l0)
        place(sfx, filt(noise(0.012), "bandpass", [2500, 6000]) * env(int(0.012 * SR), 0.0005, 0.01), t, 0.07, 0.35)
        t += 0.5 - 0.42 * x
    span = pause - 0.6 - (l0 + 0.6)
    for k in range(9):
        peak = l0 + 0.6 + span * (k + 0.5) / 9
        place(mus, pad([D("C5"), D("G5"), D("A5")], 0.55, a=0.25, r=0.28, bright=3000), peak - 0.27, 0.10, -0.3 + 0.07 * k)
    place(mus, bell(D("C6"), 3.5, 0.6) + bell(D("G6"), 3.5, 0.4) * 0.5, pause + 0.2, 0.16, 0.0)    # tank full
    place(sfx, filt(noise(0.03), "highpass", 1500) * env(int(0.03 * SR), 0.001, 0.02), pause + 0.8, 0.2, -0.4)  # relay click

    # ---- figures: one soft note per figure, calm pad ----------------------------------------------
    f0, f1 = b["figures"]
    place(mus, pad([D("D3"), D("A3"), D("F4"), D("C5")], 3.3, a=1.2, r=1.2, bright=1300), f0 - 0.2, 0.38)
    place(mus, pad([D("Bb2"), D("F3"), D("D4"), D("A4")], 3.3, a=1.2, r=1.2, bright=1300), f0 + 2.9, 0.38)
    place(mus, pad([D("F2"), D("C3"), D("A3"), D("E4"), D("G4")], 4.6, a=1.2, r=2.0, bright=1400), f0 + 6.0, 0.40)
    for k, n in enumerate(["A4", "C5", "E5", "G5", "A5"]):
        place(mus, piano(D(n), 3.0), f0 + 0.6 + k * 1.3 + 0.45, 0.14, -0.3 + 0.15 * k)

    # ---- rise: build-up whoosh, the map arrives -------------------------------------------------------
    r0, r1 = b["rise"]
    place(sfx, sweep_noise(r1 - r0, 160, 3200, q=1.3) * np.linspace(0.2, 1, int((r1 - r0) * SR)) * env(int((r1 - r0) * SR), 1.5, 1.2), r0, 0.16)
    place(mus, pad([D("C3"), D("G3"), D("E4"), D("G4")], MK["rise_map"] - r0 + 1, a=2.0, r=1.0, bright=1200), r0, 0.36)
    for k, n in enumerate(["C6", "E6", "G6", "C7"]):
        place(mus, bell(D(n), 3.0, 0.5), MK["rise_map"] + k * 0.11, 0.06, -0.4 + 0.27 * k)

    # ---- map: gentle arpeggio, sites settling, the year --------------------------------------------
    m0 = MK["rise_map"]
    bpm = 100.0
    step = 60 / bpm / 2
    prog = [("Dm", ["D3", "A3", "D4", "F4", "A4", "F4", "D4", "A3"]), ("Bb", ["Bb2", "F3", "Bb3", "D4", "F4", "D4", "Bb3", "F3"]),
            ("F", ["F2", "C3", "F3", "A3", "C4", "A3", "F3", "C3"]), ("C", ["C3", "G3", "C4", "E4", "G4", "E4", "C4", "G3"])]
    chords = {"Dm": ["D3", "A3", "F4"], "Bb": ["Bb2", "F3", "D4"], "F": ["F2", "C3", "A3"], "C": ["C3", "G3", "E4"]}
    t = m0 + 1.0
    end = b["end"][0] + 0.2
    k = 0
    while t < end:
        name, notes = prog[(k // 8) % 4]
        if k % 8 == 0:
            place(mus, pad([D(x) for x in chords[name]], 8 * step + 1.2, a=0.6, r=1.0, bright=1000), t, 0.22)
        x = min(1, (t - m0) / 4)
        place(mus, piano(D(notes[k % 8]) * 2, 1.6), t, 0.07 * x, -0.35 + 0.7 * ((k % 8) / 7))
        t += step
        k += 1
    for rank in range(100):                                                                         # sites settle
        at = MK["map_drop"] + 0.04 * rank + 0.45
        f = D(["D5", "F5", "A5", "C6", "D6"][RNG.integers(5)])
        pan = RNG.uniform(-0.6, 0.6)
        place(sfx, bell(f, 0.5, 0.2), at, 0.015, pan)
        place(sfx, filt(noise(0.02), "lowpass", 900) * env(int(0.02 * SR), 0.001, 0.015), at, 0.02, pan)
    place(sfx, sweep_noise(1.8, 2400, 300, q=1.5) * env(int(1.8 * SR), 0.6, 0.8), MK["map_zoom"] - 0.2, 0.12)
    ya, yb = MK["map_year"], MK["map_year_end"]
    summer = filt(pink(yb - ya), "bandpass", [3000, 9000]) * np.sin(np.pi * np.clip((tt(yb - ya) / (yb - ya) - 0.25) / 0.5, 0, 1)) ** 2
    place(mus, np.stack([summer, np.roll(summer, 777)], 1), ya, 0.025)                              # summer shimmer
    for m in range(12):
        place(sfx, bell(D("A6"), 0.3, 0.1), ya + (yb - ya) * m / 12, 0.012, 0.6)                     # month ticks

    # ---- end: resolve and leave room for the live demo ---------------------------------------------
    e0, e1 = b["end"]
    place(mus, pad([D("F2"), D("C3"), D("A3"), D("G4"), D("C5")], e1 - e0 + 0.6, a=1.0, r=3.5, bright=1300), e0 - 0.3, 0.45)
    place(mus, bell(D("F5"), 5.0, 0.5) + bell(D("C6"), 5.0, 0.3) * 0.5, e0 + 0.2, 0.10)

    mus = reverb(mus, 3.2, wet=0.38)
    sfx = reverb(sfx, 1.2, wet=0.12, bright=8000)
    return mus, sfx


# ---------------------------------------------------------------------------------------------- mix
def duck(vo, depth_db=11.0):
    e = np.abs(vo).max(1)
    e = signal.sosfilt(sos("lowpass", 3.0), e)
    e = e / (e.max() + 1e-9)
    g = 1 - (1 - 10 ** (-depth_db / 20)) * np.clip(e * 4, 0, 1)
    return g[:, None]


def write_wav(p, x):
    x = np.clip(x, -1, 1)
    with wave.open(str(p), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--video", required=True)
    ap.add_argument("--voice", default="ryan", choices=list(VOICES))
    ap.add_argument("--no-voice", action="store_true", help="music and effects only")
    ap.add_argument("--out", default=str(FILM / "out" / "audio"))
    a = ap.parse_args()
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    mus, sfx = score()
    if a.no_voice:
        vo = np.zeros((N, 2))
        lic = None
    else:
        vo, report, lic = voiceover(a.voice)
        for at, d, room, ls, text in report:
            flag = "  <-- longer than its slot" if d > room + 0.05 else ""
            print(f"{at:6.1f}s  {d:4.1f}s of {room:4.1f}s  (speed {ls:.2f})  {text}{flag}")
        write_wav(out / "voiceover.wav", vo)
    bed = mus * duck(vo) + sfx * duck(vo, 9.0)
    write_wav(out / "score.wav", bed / (np.abs(bed).max() + 1e-9) * 0.9)
    mix = vo * 0.95 + bed * 0.55
    fade = np.ones(N)
    fade[-int(0.8 * SR):] = np.linspace(1, 0, int(0.8 * SR))
    mix = mix * fade[:, None]
    raw = out / "mix-raw.wav"
    write_wav(raw, mix / (np.abs(mix).max() + 1e-9) * 0.89)
    final = out / "mix.wav"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(raw), "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-ar", str(SR), str(final)], check=True)
    raw.unlink()
    v = Path(a.video)
    dest = out / f"{v.stem}-with-sound.mp4"
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(v), "-i", str(final), "-map", "0:v:0", "-map", "1:a:0",
                    "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-shortest", "-movflags", "+faststart", str(dest)], check=True)
    print("wrote", dest)
    if lic:
        print("voice licence:", lic)


if __name__ == "__main__":
    main()
