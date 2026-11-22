# What the team still has to do

Exact steps for us humans, in order. Decisions already taken are marked **decided** with the
reason; the few open ones are in the last section. Script: [`script.md`](script.md). Scenes and
numbers: [`pitch-flow.md`](pitch-flow.md). Commands for everything: [`QUICKSTART.md`](../../QUICKSTART.md).

## 1. Intro film (owner: whoever has the rendered MP4)

The film is rendered from `feat/T-78-intro-film` (`video/blender/`). We use **0:00–0:48** only
(**decided**: the next two beats show year-model numbers that contradict the dashboard).

1. Put the rendered film (without sound) next to the repo, e.g. `~/pitch/film.mp4`.
2. Music and effects only, no voice:
   ```bash
   git switch feat/T-78-intro-film
   pip install piper-tts numpy scipy
   python video/blender/audio/make_audio.py --video ~/pitch/film.mp4 --no-voice
   git switch main
   ```
   Result: `video/blender/out/audio/film-with-sound.mp4`.
3. Cut to 48 s with a one-second fade (video and sound):
   ```bash
   ffmpeg -i video/blender/out/audio/film-with-sound.mp4 -t 48 \
     -vf "fade=t=out:st=47:d=1" -af "afade=t=out:st=46.5:d=1.5" \
     -c:v libx264 -crf 18 -c:a aac ~/pitch/intro-48s-novoice.mp4
   ```

## 2. Record the voice-over for the film (owner: speaker A)

**Decided:** a human voice instead of the TTS voice. It is warmer, matches the live speakers, and
avoids the TTS voice's non-commercial licence (CC BY-NC-SA). Fallback: run step 2 above without
`--no-voice` and cut that file instead.

Record in a quiet room, phone close to the mouth, while watching the muted 48-second cut. Lines and
when they start (film time):

| Start | Line |
|---|---|
| 0:01 | Basel. A January evening. Minus three degrees. |
| 0:08 | This is Bob. His house needs heat. |
| 0:15 | And this is what it costs. |
| 0:22 | A few streets away, a data centre throws its heat into the night. |
| 0:28 | What if that computing ran where the heat is needed? |
| 0:32 | Four servers in Bob's basement do paid work. |
| 0:35 | Their heat goes into the water: first to the radiators, and the tank keeps the rest. |
| 0:42 | The room warms up. |

Save it as one WAV (`~/pitch/vo.wav`) that starts at film time 0:00, then mix:

```bash
ffmpeg -i ~/pitch/intro-48s-novoice.mp4 -i ~/pitch/vo.wav \
  -filter_complex "[0:a]volume=0.6[m];[m][1:a]amix=inputs=2:duration=first[a]" \
  -map 0:v -map "[a]" -c:v copy -c:a aac ~/pitch/intro-48s.mp4
```

## 3. Demo laptop (owner: operator O)

Do this the evening before and again one hour before the pitch.

1. Fresh `git pull` on `main`; `dotnet build src/DigitalHeat.sln`.
2. Fleet runs the pitch shows (each about 1–25 s):
   ```bash
   for r in reference-1-server reference real-house-1-server dreispitz-5-pool-1-server; do
     dotnet run -c Release --project src/DigitalHeat.FleetRun -- run config/fleet/scenarios/$r.json
     dotnet run -c Release --project src/DigitalHeat.FleetRun -- post data/derived/fleet/$r
   done
   ```
3. Voice demo once: models and images as in `QUICKSTART.md` §4 (about 8 GB, needs internet once).
4. Close every other program (the LLM needs about 6 GB RAM). Laptop on power. Screen sleep off.
   Notifications off (Do Not Disturb).
5. Start, in four terminals: FleetApi with `VOICE_DEMO_TOKEN`, `npm run dev` in `web/`, the voice
   runner, `ngrok http 5173` (QUICKSTART §4).
6. Copy the cut film (`~/pitch/intro.mp4`, steps 1–2) to `web/public/pitch/intro.mp4`. Open
   `http://127.0.0.1:5173/pitch?t=<token>` in Chrome, press **H**: every line green (fix red ones),
   **H** again, **F** for fullscreen. The pitch starts with **→** (QUICKSTART §4b lists all keys).
7. Phone hotspot ready as internet fallback for ngrok (venue Wi-Fi often blocks it).
8. Test the full chain once with the operator's phone: open `/speak` (QR in the checks screen,
   key **H**), ask, see freeze and thaw.
9. Optional audience demo (after the pitch, decision 0074): on the laptop that runs it,
   `scripts/Start-AudienceApertus.ps1`, then set the printed tunnel URL as `DEMO_BASE_URL` in the
   Lovable page and republish (`docs/jobs/audience-apertus-demo.md`). The pitch app shows its
   operator view at `http://127.0.0.1:18970/operator` (scene 10), so it must run on the pitch laptop,
   or change `url` of scene `audience` in `web/public/pitch/pitch.json`.

## 4. Rehearsal (all)

1. Two full run-throughs with a stopwatch; target 4:50.
2. One run-through where the phone fails (operator presses **T** and types the question).
3. One run-through where the laptop fails: play the screen recording (step 5).

## 5. Fallback recording (owner: operator O)

Record one complete good run of scenes 2–7 as a screen video, and, if you like, one per scene
for key **V** (set `fallback` per scene in `web/public/pitch/pitch.json`) (GNOME: `Ctrl+Shift+Alt+R`; macOS:
`Cmd+Shift+5`). Keep it on the desktop and on a USB stick.

## 6. Submission

**Open:** the organisers' submission form and deadline are not in the repo. Ask them, then upload:

| What | File or link |
|---|---|
| Code | the GitHub repository link, `main` |
| Paper | `docs/paper/latex/main.pdf` |
| Assumptions and sources | `docs/model/fleet-simulation.md` §16, `SOURCES.md` |
| How to run | `QUICKSTART.md` |
| Pitch video (if asked) | the fallback recording, or film + recording |

Before uploading: `dotnet test src/DigitalHeat.sln` passes (one known unrelated failure, see
QUICKSTART §1), and the repository is public or shared with the jury.

## 7. Decisions left to the team

- **Who is A, B and O.**
- **Submission form:** where and by when (ask the organisers).
