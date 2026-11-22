# 0069: Pitch mode as one route of the web dashboard

- **Status:** withdrawn (2026-11-22, Ennio): the pitch app was removed from the code; the pitch uses the normal dashboard pages.
- **Date:** 2026-11-22
- **Decided by:** Claude (delegated by Ennio: "one software that combines the complete pitch flow in
  the browser; foolproof at the presentation, flexible on request")

## Context

The pitch (`docs/pitch/script.md`, `pitch-flow.md`) runs eight scenes: intro film, two household
pages, the network map (15 January, 15 July, zoom out to a globe), the QR code and the live demo with
the runner console and `/live`, a closing line. Today these are separate windows, a terminal and a
video player; switching on stage is slow and error-prone.

## Decision

A new route `/pitch` in `web/` plays the scenes in order. Every scene that shows an existing page
loads it once at start in its own stacked iframe (same origin) and is only made visible when its
turn comes, so no page loads on stage. The map page accepts `postMessage` commands from the pitch
page (`setHour`, `globe`, `fit`) and a `fit=1` URL parameter; MapLibre's globe projection does the
zoom-out, so the last map frame and the first globe frame are the same picture. The voice runner adds
its console lines to the live snapshot, and the pitch page renders them as a console, so the demo
needs no terminal window on screen. Keys: → / Space / PageDown next, ← / PageUp back, 1–8 jump,
G globe, B black, F fullscreen, H help. The scene list, runs, hours, texts and QR URL come from
`web/public/pitch/pitch.json` (built-in defaults if missing); the film is `web/public/pitch/intro.mp4`
(git-ignored; a title card replaces it when missing). One new dependency, `qrcode`, draws the QR code
offline.

## Alternatives considered

- **Slides with screenshots:** foolproof but not live; the case requires a real running job.
- **Separate windows and a terminal (today):** live, but switching is manual and slow.
- **Re-implementing the pages inside the pitch route:** duplicates code that already works.

## Consequences

- One browser tab runs the pitch; the API, the dev server and the voice runner must run (QUICKSTART).
- The map and household pages gain a small, same-origin message listener; normal use is unchanged.
- Iframes cost memory (four pages); acceptable on the demo laptop.

## Review

Fresh-context review (decision-reviewer, 2026-11-22): **go with changes**. Applied:
1. Focus: iframes ignore the mouse (`pointer-events: none`), keys pressed inside a page are
   forwarded to the pitch, and the pitch window takes focus again on every scene change.
2. Globe offline: in pitch mode the map's offline style shows bundled Natural Earth 110 m land
   (`web/public/pitch/ne_110m_land.geojson`, public domain) instead of a black background.
   Deviation: online, the pitch keeps the Carto style, because the Dreispitz scene needs streets;
   the style is chosen once when the pages preload, before the pitch, and the checks screen shows it.
3. Frames are stacked full-size and switched by opacity; each page preloads all hours it will show,
   then takes the state of the scene on screen (also after a reload mid-pitch).
4. Checks screen (key H): API, every run, intro film (by content type), voice runner, QR address,
   token, map style, config, and the strip numbers compared with the run summaries. Key V plays a
   scene's fallback recording (`fallback` in `pitch.json`).
5. Clicker keys: F5 and Ctrl+R are blocked, `.` and `B` black out, double presses within 250 ms are
   ignored, the scene is kept in the URL hash so a reload resumes there, and the film starts on the
   first key press.
6. Phone: the speak page address comes from ngrok's local API through FleetApi
   (`GET /api/voice-demo/tunnel`) plus the token from the pitch URL (`/pitch?t=…`); Q overrides it.
   Later change (2026-11-22): the presenter speaks into their own phone, so this code is shown only
   in the checks screen; the audience QR belongs to the separate audience demo (decision 0074),
   added as two optional scenes after the close. Key T opens a text field that posts to the same
   endpoint as the phone.
