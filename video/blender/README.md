# Intro film (T-78)

A ~112-second intro film for the pitch, rendered in **Blender 4.x purely from Python scripts**
(bpy; no add-ons, no downloaded assets; tested with Blender 4.5 LTS). It hands over to the live
demo. Ticket: [`docs/tickets/T-78-intro-film.md`](../../docs/tickets/T-78-intro-film.md).

## Commands

Preview (960×540, 4 samples, every 2nd frame rendered and held, about 2–3 h on a 4-core CPU,
much faster on a GPU):

```bash
video/blender/make_film.sh preview
```

Full resolution (1920×1080, 64 samples, motion blur, every frame; uses the GPU automatically if
Cycles finds one):

```bash
video/blender/make_film.sh final
```

On Windows (PowerShell), use the PowerShell script instead (it finds Blender under
`C:\Program Files\Blender Foundation\` automatically; otherwise pass `-Blender`):

```powershell
.\video\blender\make_film.ps1
.\video\blender\make_film.ps1 -Preset final
```

GPU: the script picks the first GPU backend Cycles finds (OptiX, CUDA, HIP, oneAPI, Metal) and prints
`Cycles device: …` at the start. Force one with `-Gpu OPTIX` (NVIDIA RTX), `-Gpu CUDA` (older NVIDIA),
`-Gpu HIP` (AMD) or `-Gpu ONEAPI` (Intel Arc); in bash set `DH_GPU=OPTIX`. Blender's own preference
setting is not used because the film runs with `--factory-startup`.

If PowerShell blocks the script: `powershell -ExecutionPolicy Bypass -File .\video\blender\make_film.ps1`.

Set `BLENDER=/path/to/blender` if `blender` is not on the PATH (bash). Without a Blender install, a Python
3.11 with the `bpy` module works too (`pip install bpy==4.5.*`, then `BLENDER=/path/to/python3.11`).
`post.py` needs Python 3 with Pillow, plus ffmpeg. Rendering is resumable: frames that already exist in
`out/<preset>/frames/` are skipped, so an interrupted run continues where it stopped. Delete that folder
after changing the scene.

Look-development stills only (one or two per beat): `blender -b --factory-startup -P video/blender/render.py -- --stills`,
then `python3 video/blender/post.py --stills` (captions on top). Single moments: `--at 10,36.5`.

## Pipeline

| Step | File | What it does |
|---|---|---|
| 1 | `prepare_data.py` | Reads repository results, runs FleetRun for the two map scenarios if needed (.NET 8), writes `numbers.json` and `data/film-data.json` |
| 2 | `render.py` + `film/` | Builds the whole world (street, Bob's house, basement, data centre, city, map) and keys all shots from `timeline.json`; renders frames |
| 3 | `post.py` | Colour grade (cold → warm), captions, map UI, monthly chart, encode to MP4 |

`film/`: `street.py` (Basel row houses, snow, lamps, cars, trolley wires), `interior.py` (living room,
basement: servers, tank, pipes, radiator, and a small green turtle on the side table), `bob.py` (character and props), `datacentre.py`,
`city.py` (procedural city, Rhine, map layer), `shots.py` (choreography), `look.py` (render settings,
night world, mist and bloom).

## Story and timing

Beats and their seconds are in [`timeline.json`](timeline.json): street (0–8 s), cold (8–21),
data centre (21–28), dive into the basement (28–48), time-lapse to the first full tank (48–58), key
figures (58–68), rise out of the street (68–80), Basel map and 2025 time-lapse (80–106), hand-over
frame (106–112). A draft voice-over cut to these beats is in [`voiceover-draft.md`](voiceover-draft.md).
**To sync with `docs/pitch/voiceover.md`:** move the beat `start`/`end` values and `marks` to the
script's timestamps and render again; nothing else holds times.

## Numbers and sources

Every number shown comes from [`numbers.json`](numbers.json), each with its source file and field;
`post.py` and the figure beat read only that file. Main sources:

- Reference house, 4 servers, Basel 2025 (C# year model, T-06): coverage 58.0 %, 0 kWh dumped,
  utilisation 64.7 %, case break-even CHF 0.23/server-hour — `data/derived/reference-house/explorer-default.json`.
  Missed deadlines are not defined for the unlimited-jobs run and are not shown as a number.
- The evening (13 January 2025, 19:00, −3.0 °C) and the first full tank (22 March 2025, 09:00):
  `data/raw/open-meteo/basel-2025-hourly.csv` and the hourly C# run `data/derived/reference-house-v1/…/hourly.csv`.
  In January all server heat goes straight to the radiators, so the tank stays nearly empty in the dive;
  it fills only in the time-lapse, following the hourly data.
- Map: FleetRun `config/fleet/scenarios/basel-region-100.json` (100 synthetic houses, Basel-Landschaft)
  and `dreispitz-5-pool.json` (T-67 variant: 5 houses + indoor pool). The glow of each site follows
  its weekly mean server load; the monthly chart shows houses 46 % → 16 % and pool 23 % → 50 % from
  January to July. These are scenario variants, not scored figures.

Illustrative, not data: Bob's house position (Gundeldingen), the street, the data centre and its
location, the city blocks, the hand-approximated Rhine and the "jobs from customers" hub at the city
centre. Canton outlines: `prototypes/network-map-draft/data/switzerland-cantons.geojson` (Natural Earth).
Font: Inter (system font, OFL); Blender's built-in font is used if Inter is missing.

## Design notes

- Colour grammar: cold blue (night, the cold house) vs. warm amber (server heat, used heat); job flows on
  the map are the dashboard's blue (`--series-1`), heat stays amber and local.
- The map uses the dashboard's dark style (`web/src/admin`), so the film hands over to the live tool
  without a style jump. The control `map` in `film/city.py` turns the realistic night city into the flat map.
- The falling server racks: the map's sites arrive as small modules that drop onto the map and settle with
  a soft bounce, then light up (`film/shots.py`, `_map_section`). The original rack animation was not found
  in the repository; point us to it and it can replace this.
- Later globe ending (after the live demo): the map is a plane in local metres; for a globe zoom-out, swap
  `ground()` for a sphere and keep the same camera language.

## Eevee presets (untested look)

For GPUs that Cycles cannot use (e.g. Intel Iris Xe): `--preset eevee-preview` / `--preset eevee`
(PowerShell: `.\video\blender\make_film.ps1 -Preset eevee-preview`). The scene builds with Eevee, but no
frame has been checked yet; expect differences in glows and transparency compared with Cycles.

## Sound (voice-over, score, effects)

```bash
pip install piper-tts numpy scipy   # Python 3.10/3.11
python video/blender/audio/make_audio.py --video path/to/film.mp4
```

Writes `out/audio/<video>-with-sound.mp4` plus the stems `voiceover.wav`, `score.wav` and `mix.wav`
(−16 LUFS). Voice lines and their start times: [`audio/voiceover.json`](audio/voiceover.json) (numbers as
in `numbers.json`); beat times from `timeline.json`. The voice is Piper (offline neural TTS) with the
"Ryan" voice, downloaded once from the rhasspy/piper GitHub release (dataset licence CC BY-NC-SA 4.0:
non-commercial, credit "Voice: Piper / RyanSpeech"); `--voice libritts` uses a CC BY 4.0 voice instead.
The score and effects (wind, cold radiator, envelope, server spin-up, water, clock, map pulses) are
synthesised in the script; no samples or music are downloaded. `--no-voice` gives music and effects
only, e.g. to record a human voice over it.
