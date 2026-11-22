# T-78: Live voice request on the summer reference house (pitch demo)

- **Owner:** Ennio / Claude · **Status:** done · **Branch:** `feat/T-78-voice-request-demo`
- **Depends on:** T-02 (Whisper), T-33 (Apertus LLM), T-72 (`/live`), PR #52 (mesh heat-first scheduler); decision 0068.

## Goal

Pitch demo (must-have 2, live part): the presenter speaks a request into a phone; one real
container (Whisper large-v3 transcription, then the Apertus 8B answer) is admitted, paused and
resumed by the mesh heat-first scheduler (`MeshPeer`, the code of the Linux peer mesh) on the
modelled July reference house, and the answer appears on screen. Split screen: runner console
left, `/live` right.

## Scope

- `jobs/voice-assistant`: one image built offline from the two existing job images.
- `src/DigitalHeat.VoiceDemo`: in-process single-site `MeshPeer` on the local Docker, house replay,
  console log, `/live` snapshot. No changes to Orchestration or Core.
- FleetApi `POST /api/voice-demo/requests` (inbox upload); web `/speak` (phone page) and a request
  card on `/live`.
- `config/voice-demo.json`: all demo settings with unit and source; `config/hot-water-profile-l.json` (EU 814/2013 profile L).
- Mesh rule change (decision 0068): freeze batch work whose heat cannot be used or stored.
- Measured container CPU time and energy (cgroup + RAPL).

## Done when

- A spoken request runs to an answer with exactly one heat pause and resume, deterministic timeline.
- Runbook `docs/demo/voice-request.md` with the calculation path; typed fallback in the runner's terminal.

## Status

End-to-end verified locally: admit 11:00, freeze in the quiet hour of profile L, thaw at the 14:30
draw, measured CPU time and energy at exit. Freeze verified with an independent cgroup probe; typed
fallback verified; the user ran the phone path through ngrok. Decision 0068 reviewed (go with
changes, applied).
