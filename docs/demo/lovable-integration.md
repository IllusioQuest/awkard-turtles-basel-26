# Lovable phone page → voice demo on the laptop

The QR code in the pitch opens **https://swiss-heat.lovable.app** (with `?t=<token>`). That page
records the juror's question and sends it to the laptop, where the scheduler runs it as a real job
(`docs/demo/voice-request.md`). The laptop is reached through an ngrok tunnel to the API
(`DigitalHeat.FleetApi`, port 5080).

## The URL to enter in Lovable

```
https://<your-static-domain>.ngrok-free.app   (newer accounts: .ngrok-free.dev)
```

This is the **API base URL**. Get it once:

1. ngrok dashboard → **Domains** → claim the free static domain (every account has one). It looks
   like `something-random.ngrok-free.app` and never changes.
2. Enter `https://<that domain>` in Lovable as the API base URL (for example as a constant or an
   environment value named `VOICE_API_BASE`).
3. On the demo laptop, start the tunnel to the API (not to the web app):
   ```bash
   ngrok http --url=<that domain> 5080
   ```

## What the Lovable page must do

| | |
|---|---|
| Send a recording | `POST {VOICE_API_BASE}/api/voice-demo/requests`, body = the raw audio blob, header `Content-Type` = the recorder's type without codecs (`audio/webm`, `audio/mp4` on iPhone, `audio/ogg`, `audio/wav`) |
| Send typed text | same URL, body = the text, `Content-Type: text/plain` (max 500 characters) |
| Headers on every request | `ngrok-skip-browser-warning: true` (otherwise ngrok returns its warning page), and `X-Voice-Token: <t>` where `t` is the `t` parameter of the page's own URL |
| Answer | `200 {"id": "..."}`; `401` wrong token, `415` unsupported type, `413` over 20 MB |
| Show progress (optional) | `GET {VOICE_API_BASE}/api/live-household` every second: `voice.stage` (`transcribing`, `answering`, `done`), `voice.transcript`, `voice.answer`, `processStatus` (`running`, `frozen`) |

The API allows these browser origins (CORS): `https://swiss-heat.lovable.app`, any
`https://*.lovable.app`, Lovable previews `https://*.lovableproject.com`, plus any listed in the
environment variable `VOICE_DEMO_ORIGINS` (comma-separated) when the API starts.

## Prompt to paste into Lovable

> Build a single mobile page "Ask Bob's house a question". A large round button records audio with
> MediaRecorder (prefer `audio/webm;codecs=opus`, fall back to `audio/mp4`); tapping again stops and
> sends. Also offer a small "Type instead" text field. Send to
> `${VOICE_API_BASE}/api/voice-demo/requests` with `fetch`, method POST, body = the Blob (or the text),
> headers `Content-Type` = the blob type without the `;codecs` part (or `text/plain`),
> `ngrok-skip-browser-warning: true` and `X-Voice-Token` = the `t` query parameter of the current page
> URL. `VOICE_API_BASE` is `https://<your-static-domain>.ngrok-free.app`. After sending, show
> "Job scheduled" and poll `${VOICE_API_BASE}/api/live-household` (same two extra headers) every second;
> show `voice.transcript` once it is not empty, "Paused, waiting for useful heat" while
> `processStatus` is `frozen`, and `voice.answer` as it grows. Dark theme, large type, no login.

## Check before the pitch

```bash
curl -s -X POST "https://<domain>/api/voice-demo/requests" \
  -H "Content-Type: text/plain" -H "X-Voice-Token: <token>" -H "ngrok-skip-browser-warning: true" \
  --data "How much heat does one server give off?"
```

Expect `{"id":"…"}` and, in the pitch app, the request in the console. In the pitch app, key **H**
shows "Public API tunnel … → 5080" in green. The QR address comes from `qrUrl` in
`web/public/pitch/pitch.json` (`https://swiss-heat.lovable.app/?t={token}`); `{token}` is filled from
the pitch URL `http://127.0.0.1:5173/pitch?t=<token>`.

Fallbacks: no internet or Lovable down → key **T** in the pitch app (same endpoint, local), or
`http://127.0.0.1:5173/speak` on the laptop.
