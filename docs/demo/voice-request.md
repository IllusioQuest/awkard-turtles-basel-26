# Live demo: a spoken request on the summer reference house (T-78)

The presenter speaks into a phone (or types into the runner's terminal). One real container
transcribes the request (Whisper large-v3) and answers it (Apertus 8B), on the laptop, offline.
The **mesh heat-first scheduler** (`DigitalHeat.Orchestration.MeshPeer`, the code of the Linux peer
mesh) admits, freezes and thaws that container from the modelled July reference house. Left half of
the screen: the runner console. Right half: `/live`.

**Real:** the container, its freeze/thaw (`docker pause`, kernel cgroup freezer), the transcript and
answer, its CPU time and its energy. **Modelled:** house demand, tank, the house's other servers, and
the clock (10 s per simulated hour).

## Run

Once: `docker build -t digitalheat/voice-assistant jobs/voice-assistant`. It builds offline from the
two job images. The models must exist under `data/external/transcription/models` and
`data/external/llm/models`; the runner hardlinks them.

Run these from the repository root:

```bash
VOICE_DEMO_TOKEN=<secret> dotnet run -c Release --project src/DigitalHeat.FleetApi   # API, 127.0.0.1:5080
cd web && npm run dev                                                               # dashboard, 127.0.0.1:5173
dotnet run -c Release --project src/DigitalHeat.VoiceDemo                           # runner: left half
ngrok http 5173                                                                     # phone: https://<id>.ngrok-free.app/speak?t=<secret>
```

Laptop browser: `http://127.0.0.1:5173/live` (right half). The runner pre-loads both models into RAM
and then waits. Every request, spoken or typed, starts a fresh replay at 11:30 on 15 July, so every
run has the same timeline: the quiet hour (13:00) arrives 15 s after the start, usually during text
generation. The
LLM needs about 6 GB of free RAM.

**Fallback if the phone fails:** type the request into the runner's terminal and press Enter. It goes
through the same inbox and scheduler path, and only Whisper is skipped. Without internet,
`http://127.0.0.1:5173/speak` on the laptop also works.

## What the audience sees (measured run with an 11:00 start, 2026-11-22, Ryzen 7 250, 12 CPU cores)

The current 11:30 start gives the same sequence, half an hour shorter before the freeze.

```
11:00 INFO  inbox  voice-001 audio 276.2 kB
11:00 INFO  sched  admit  voice-001  free 0.216 ≥ need 0.020 kWh
11:00 INFO  docker start  dhw-c76b1fc5  cpus=12
dhw-c76b1fc5 | » Hey Digital Heat, how much heat does one server give off? …
dhw-c76b1fc5 | One DigitalHeat server generates about 0.8 kW of useful heat. This is enough to warm
13:07 WARN  sched  freeze voice-001  free 0.000 < need 0.020 kWh
dhw-c76b1fc5 |  about 12
14:03 INFO  sched  thaw   voice-001  free 0.108 ≥ need 0.020 kWh
dhw-c76b1fc5 |  kWh of water per day, which is sufficient for a family's daily hot water needs.
14:42 INFO  docker exit 0 dhw-c76b1fc5  cpu 243 s  0.000111 kWh
```

`free` is the useful heat of the current hour (`HeatCapacity.RemainingKwh`: hot-water demand plus
tank room). `need` is the job's declared heat per hour. The freeze and thaw are logged up to about
1 s (≈ 7 simulated minutes) after the hour boundary, because each control cycle includes the
Docker calls.

## How the freeze and thaw come about (calculation path)

Inputs: `config/reference-house.json` (case: 4 servers × 1 kW, 80 % recovery, 35 kWh tank,
12 kWh/day hot water), Basel weather 2025-07-15, `config/hot-water-profile-l.json`, and
`config/voice-demo.json` (every value with its source).

1. **Hot water over the day (labelled variant):** EU Regulation 814/2013, Annex III, load profile L
   (24 tappings, 11.655 kWh/day), scaled to the case's 12 kWh/day (× 1.0296) and binned into hours.
   The daily total is unchanged. Example: 11:00–12:00 has two tappings of 0.105 kWh, so
   2 × 0.105 × 1.0296 = 0.216 kWh. 13:00–14:00 has none (the profile is quiet from 12:45 to 14:30), so
   0 kWh. 14:00–15:00 has 0.105 × 1.0296 = 0.108 kWh. Space heat on 15 July is 0 (Core `HeatDemand`).
2. **The house's other servers** run simulated background work, scheduled heat-first on whatever the
   request leaves: this hour's demand plus the tank room spread over the rest of the hour, minus the
   request's heat. They keep the tank full (35 kWh) all day without dumping. Nothing is switched off
   by hand; in the quiet hour they simply drop to 0.
3. **Request heat, measured:** CPU time from the container's cgroup (`cpu.stat`). Energy comes from
   the CPU package counter (RAPL), attributed to the container by its share of busy CPU time (the
   Scaphandre/Kepler method; package only, no RAM or display). Heat into the house = 80 % × measured
   power. For admission the request declares 0.020 kWh/h (25.1 W measured package power of the LLM
   job, `docs/jobs/llm-summary.md`, × 0.8).
4. **Scheduler (decision 0068):** a freeze-only batch job runs only while its declared heat fits the
   useful heat (demand + tank room). Otherwise it is frozen. It thaws when the job fits again, after
   at least 3 s (`minimum_freeze_seconds`).
   - 11:30: 0.216 ≥ 0.020 → admit.
   - 13:00: tank full and no draw, 0 < 0.020 → freeze.
   - 14:00: 0.108 ≥ 0.020 → thaw; the job runs to the end.

**Reading `/live`:** "Modelled tank" stays at 35 kWh; the servers refill whatever is drawn. "Server
heat" (background plus request) follows "Demand", the hot-water profile; both drop to 0 from 13:00 to
14:00. "Job power" and "Job energy" are the measured container values. "Useful heat now" is the
`free` value from the console.

## The freeze is real

`MeshPeer` calls `docker pause` (Orchestration `DockerWorkloadRuntime.ApplyBatchLoad` →
`DockerJobExecutor`). The kernel's cgroup freezer stops every process in the container
mid-computation, and `docker unpause` continues from the same instruction. An independent probe
(2026-11-22, 0.5 s samples of `docker inspect`, `cgroup.freeze`, `cpu.stat` and the answer file):

| Time | Docker state | cgroup.freeze | CPU usage (s) | Answer bytes |
|---|---|---|---|---|
| 02:01:12.7 | running | 0 | 68.80 | 56 |
| 02:01:13.4 | paused | 1 | 77.48 | 67 |
| 02:01:22.6 | paused | 1 | 77.48 | 67 |
| 02:01:23.6 | running | 0 | 86.58 | 73 |

The CPU counter and the answer stood still for 9.2 s, then the same two processes continued.

## Speed choices (measured on this laptop, 8 s clip)

| Setting | Whisper large-v3 |
|---|---|
| Default (5-beam search, auto language, full 30 s window) | 28.6 s |
| Greedy, fixed language, window sized to the recording | **8.2 s** |
| `ggml-base` instead (more errors, docs/jobs/transcription.md) | 0.8 s |

LLM: about 3.6 s to load from RAM, about 4 tokens/s (`max_new_tokens` = 80). Keeping a model loaded
outside the scheduled container would save the load time. But then the container the scheduler
freezes would no longer do the work, so we do not do that.

## Limits

- One site and one request at a time. Mesh offers, proposals and replies run in-process through a
  loopback transport.
- The measured energy is the laptop's CPU package share (≈ 0.0001 kWh per request), not a 1 kW
  reference server. The tank and the house's other servers are modelled.
- The hot-water time profile is a labelled variant (EU profile L). The scored reference-house figures
  keep the case's flat 12 kWh/day.
- The answer comes from a local 8B model. The system prompt gives it the case figures, but read the
  answer before quoting it.
