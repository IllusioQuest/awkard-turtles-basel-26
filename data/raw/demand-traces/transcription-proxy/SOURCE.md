# Transcription demand: proxy only

No public request trace for speech-to-text APIs was found (searched 2026-11-21: vendor docs,
NVIDIA Riva benchmarks, academic serving papers). What exists is a **proxy** for when audio is
recorded (meetings), not for when transcription is requested:

- Doodle, "State of Meetings Q2 2020", >30 M users, booked meetings:
  https://doodle.com/en/resources/research-and-reports/state-of-meetings-in-q2-2020/ (accessed
  2026-11-21). Switzerland: 12 % of meetings on weekends; Germany: 57 % between 12:00 and 18:00;
  UK: Wednesday and Thursday 20 % each, Saturday 5 %, Sunday 3 %. No full hourly table.
  Rows: `cd-t1-stt-proxy-doodle-*` in `data/raw/market/compute-demand.csv`.

No hourly profile file is derived from this: building one would need invented hours. For batch
transcription (uploaded recordings with a deadline of hours), the deadline matters more than the
arrival shape; see `docs/research/compute-demand-traces.md`.
