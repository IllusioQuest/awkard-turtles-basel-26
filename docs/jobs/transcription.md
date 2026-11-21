# Job: batch transcription (whisper.cpp, model `base`)

Ticket T-02 (must-have 1: one job type actually runs on our machine, with measured runtime and
energy, scaled to the 1 kW server). **Status: measured on one laptop. Not yet reviewed by the team.**

## What the job is

- German speech to text with [whisper.cpp](https://github.com/ggml-org/whisper.cpp) **v1.9.5**
  (release 2026-10-06), model `ggml-base.bin` (multilingual, 147,951,465 bytes,
  SHA1 `465707469ff3a37a2b9b8d8f89f2f99de7299dac` as published in the whisper.cpp
  [models README](https://raw.githubusercontent.com/ggml-org/whisper.cpp/master/models/README.md),
  SHA256 `60ed5bc3…a2efe` from Hugging Face; both checked after download, 2026-11-21).
- Container `jobs/transcription/` (whisper.cpp CLI + ffmpeg + `run_queue.sh`). It runs offline
  after the build; the model is mounted, not baked in.
- **Work unit: one minute of audio.** The input is split into 30 s chunks and processed as a queue;
  each chunk's transcript is written as soon as it is done.
- **Interruptible: yes.** `docker pause` (the `freeze` control in decision 0009) stops it at once;
  throttling through Docker's CPU quota also works. A crash loses at most the current chunk.
- Decoding is greedy without temperature fallback (`-nf`), so the output is deterministic.
- Test audio: Puschkin, *Der Schuß*, Kapitel 2, read by Herr Klugbeisser, LibriVox, 14:58 min,
  German. <https://archive.org/download/schuss_hk_librivox/schuss_2_puschkin_64kb.mp3>
  (accessed 2026-11-21). LibriVox: "LibriVox recordings are Public Domain in the USA"
  ([book page](https://librivox.org/der-schuss-by-alexander-sergejewitsch-puschkin/)); the 19th-century
  text is public domain too. Read-aloud standard German, not Swiss German (see Limits).

### Host contract (used by `scripts/measure_job.py` and `DigitalHeat.LiveRun`)

| Path in the work folder | Meaning |
|---|---|
| `in/chunk_NNNN.wav` | input queue, 16 kHz mono PCM |
| `out/pass_NNN/chunk_NNNN.txt` | transcript; a chunk is done once its file exists |
| `out/log.csv` | pass, chunk, audio seconds, start and end time (ns) |
| `progress.json` | `work_unit`, `units_done` (audio minutes), `chunks_done`, `chunks_total`, `pass` |

`PASSES=0` repeats the queue forever (for the live demo). `PASSES=1` processes it once (for measuring).

## Reproduce

```bash
# once: data (not committed, ~150 MB) and image
E=data/external/transcription
mkdir -p $E/models $E/audio $E/work
curl -L -o $E/models/ggml-base.bin https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin
curl -L -o $E/audio/schuss_2_puschkin_64kb.mp3 https://archive.org/download/schuss_hk_librivox/schuss_2_puschkin_64kb.mp3
docker build -t digitalheat/transcription-whisper jobs/transcription
docker run --rm --user "$(id -u):$(id -g)" -v "$PWD/$E/audio:/audio:ro,Z" -v "$PWD/$E/work:/work:Z" \
  digitalheat/transcription-whisper prepare /audio/schuss_2_puschkin_64kb.mp3

# energy meters: the CPU counter is root-only (resets on reboot); unplug the charger
sudo chmod o+r /sys/class/powercap/intel-rapl:0/energy_uj

# 60 s idle baseline + 5 runs  (about 8 min)
python3 scripts/measure_job.py energy --image digitalheat/transcription-whisper --cpus 8 --env THREADS=8 \
  --mount "$PWD/$E/models:/models:ro,Z" --mount "$PWD/$E/work:/work:Z" \
  --progress-file $E/work/progress.json --clear $E/work/out --idle-seconds 60 --reps 5 \
  --out data/raw/measurements/transcription-whisper-base/energy-<date> run

# pause experiment: 30 s idle, 30 s running, 30 s frozen, 30 s resumed
python3 scripts/measure_job.py pause --image digitalheat/transcription-whisper --name dh-transcription-pause \
  --cpus 8 --env THREADS=8 --env PASSES=0 \
  --mount "$PWD/$E/models:/models:ro,Z" --mount "$PWD/$E/work:/work:Z" \
  --progress-file $E/work/progress.json --clear $E/work/out \
  --idle-seconds 30 --pause-at 30 --pause-seconds 30 --after-seconds 30 \
  --out data/raw/measurements/transcription-whisper-base/pause-<date> run
```

## Machine and conditions (2026-11-21, 01:10 Europe/Zurich)

AMD Ryzen 7 250 (8 cores / 16 threads, 28 W default TDP, cTDP 15–30 W:
[AMD product page](https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-7-250.html),
accessed 2026-11-21), 22 GB RAM, Fedora 44, kernel 7.2.11, governor `powersave`, platform profile
`balanced`, on battery. Docker 29.9.1, `--cpus 8`, 8 whisper threads. Full details in each
`summary.json`.

## Energy method

Two software meters, sampled every second by `scripts/measure_job.py`:

1. **CPU package** from the powercap/RAPL counter (`intel-rapl:0`, domain `package-0`). Precise,
   but covers only the processor package: no RAM, display, SSD or power-supply losses.
2. **Whole laptop** from the battery (`power_now`) while discharging. Covers everything, but the
   firmware smooths it heavily (see the pause experiment) and the display and other programs are
   included.

*Gross* = all electricity used during the run (it all ends up as heat). *Net* = gross minus idle
power × runtime (what the job adds).

## Results (raw data: `data/raw/measurements/transcription-whisper-base/`)

5 runs × 14.98 audio minutes:

| Run | Runtime (s) | CPU package (W) | Whole laptop (W) | Wh per audio min, package | Wh per audio min, laptop |
|---|---|---|---|---|---|
| 0 | 68.9 | 23.2 | 30.1 | 0.0289 | 0.0384 |
| 1 | 69.0 | 22.9 | 32.9 | 0.0289 | 0.0421 |
| 2 | 68.9 | 22.9 | 32.2 | 0.0288 | 0.0411 |
| 3 | 70.3 | 23.1 | 26.1 | 0.0300 | 0.0340 |
| 4 | 69.2 | 23.1 | 27.5 | 0.0291 | 0.0353 |

| Per audio minute | Mean | Std. dev. |
|---|---|---|
| Runtime | **4.62 s** (13× faster than real time) | 0.04 s |
| Energy, CPU package, gross | 0.0291 Wh | 0.0005 Wh |
| Energy, CPU package, net | 0.0254 Wh | 0.0005 Wh |
| Energy, whole laptop, gross | **0.0382 Wh** | 0.0035 Wh |
| Energy, whole laptop, net | 0.0193 Wh | 0.0036 Wh |

Idle baseline: CPU package 3.0 W, whole laptop 14.7 W. The laptop's idle value is high (screen on,
other programs running), so its *net* figure is unreliable. It is even lower than the package net
figure, which is physically impossible for a whole-system meter. Do not use it.

**Pause experiment** (`pause-2026-11-21/`): CPU package 2.4 W idle → 20.7 W running → **2.6 W
paused** → 20.3 W resumed. Progress stayed at 6.00 audio minutes for the full 30 s pause. In a
separate check, a transcript produced with a 10 s pause was byte-for-byte identical to one without.
The battery meter showed 20.0 W *while paused*: it lags by tens of seconds, so it is used only for
averages over whole runs.

## Scaling to the 1 kW reference server

Assumption (to state in the pitch): **energy per audio minute stays the same on the server**. In
other words, a fully loaded 1 kW CPU server is about as efficient per minute of audio as this
laptop. All of its 1 kWh per server-hour becomes heat (case: 80 % recoverable).

We use the **whole-laptop gross** figure, because the 1 kW server is a whole machine. The CPU
package figure gives the optimistic end of the range.

```
audio minutes per server-hour = 1 kWh ÷ energy per audio minute
                              = 1000 Wh ÷ 0.0382 Wh  ≈ 26,200 audio min ≈ 436 audio hours
           (CPU package only: 1000 Wh ÷ 0.0291 Wh  ≈ 34,300 audio min ≈ 572 audio hours)
```

Worked example. In the coldest hour, the reference house needs 12.5 servers (case), i.e. 12.5 kWh
of electricity in that hour. That is about 12.5 × 436 ≈ **5,450 hours of audio in one hour**, for
one house. Over the year, the explorer default (4 servers, 22,661 server-hours) gives
22,661 × 436 ≈ **9.9 million audio hours per house per year**.

**What follows: with this model, demand is the bottleneck, not heat or compute.** At the batch
market price of CHF 0.13–0.30 per audio hour (`docs/research/ai-services.md`), one server-hour of
work would be worth CHF 57–131. The case break-even is CHF 0.23 per server-hour. Nobody will buy
that much audio, so `base`-quality transcription alone cannot fill a house's heat demand. We need
heavier jobs per unit of value, or a mix of services, or we state "unlimited jobs" (the case
allows it) together with this caveat.

## Limits

- One laptop, one night, one audio file (read-aloud standard German). A server CPU and a GPU will
  differ: a GPU is likely much more efficient per minute (ticket T-15).
- The model is `base`: fast but lower quality. Whisper is not trained specifically on Swiss German.
- The two meters cover different things. The true laptop figure lies between them; neither
  measures power at the wall.
- The 1 s battery samples are smoothed by the firmware.
- The `deadlineHours` field in the descriptor is still `null`: `TODO(source)` for typical
  customer deadlines (T-01).
