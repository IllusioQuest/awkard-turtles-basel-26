# Audience Apertus demo (T-78)

This is the optional audience moment **after** the scored five-minute pitch and its offline January/July/interrupted-job demonstration. The projector briefly shows a QR leading to the existing question form, either directly through the tunnel's `/ask` URL or at a stable Lovable URL. A visitor taps a standard question or types one short public question, and later sees a real Apertus answer. The projector then returns to the laptop's four-room view: each room's background colour shows its heat demand relative to the other three, dark server boxes show work, and small coloured tokens show questions. The heat budget is illustrative. All four workers share one physical laptop. No actual recovered laptop heat is measured.

## One laptop command

From the repository root in PowerShell, with Docker Desktop running in Linux-container mode:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-AudienceApertus.ps1
```

The script checks .NET, Docker and available RAM; downloads the Apertus Q4 GGUF, the pinned llama.cpp source archive and `cloudflared` on first use; builds the existing llama.cpp CPU recipe from that local source and a small audience image; copies the verified model once into a dedicated Docker volume; builds the .NET service; then starts the service and a temporary public Cloudflare tunnel. Building from a local source archive avoids Docker's GitHub certificate problem on this host. The read-only source mount exposes only `data/external/audience-demo/models/` during setup; inference containers see only the Docker-managed model volume, not the token or job history. It opens `http://127.0.0.1:18970/operator` for the four-panel display and prints the public `https://…trycloudflare.com/ask` URL. The first run downloads about 5 GB and compiles llama.cpp, so perform it before the presentation. Later runs reuse the model and built image automatically. The local data, model, tunnel binary, logs, question history and operator token stay under git-ignored `data/external/audience-demo/`.

Keep the laptop awake, online and powered during audience participation. A Quick Tunnel URL is temporary, changes on every start and has no uptime guarantee. If using Lovable, update its `DEMO_BASE_URL` with the new tunnel URL and republish before showing its stable QR. If using the tunnel's `/ask` page directly, regenerate the QR for the new public URL. If the tunnel fails, the existing offline pitch demo remains available; the audience segment is optional.

After the audience segment, stop the temporary public entry and release owned jobs with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Stop-AudienceApertus.ps1
```

## Lovable: publish the existing visitor page

The visitor page already exists as [`src/DigitalHeat.AudienceDemo/ask.html`](../../src/DigitalHeat.AudienceDemo/ask.html). The laptop serves that exact file at `/ask`. Lovable only gives the **same page** a stable public address for the QR. The laptop still runs Apertus and owns the job API. The one required hosting change is to set the file's single `DEMO_BASE_URL` constant to the temporary tunnel base URL when it is known.

For the shortest setup, Lovable can be skipped: the laptop command already prints a public `https://…trycloudflare.com/ask` URL for this same page. Its QR must be regenerated after every tunnel restart. Use Lovable only when the stable visitor URL is worth the extra publish step.

For the Lovable project already open: copy the entire contents of `ask.html` and paste them after this instruction in the Lovable chat:

> We are at a live DigitalHeat hackathon pitch. The projected laptop already shows the four-worker scheduler; visitors use their phones only to submit a public question and later read their own Apertus answer. We already built and tested the visitor interface locally. The HTML below is that existing page, including its styling, preset question, custom input, visitor ID, API requests, status polling and data notice. **Host this page instead of redesigning it.**
>
> Put the supplied HTML into a static `public/ask.html` file and make the site's root `/` redirect to `/ask.html`, so the stable Lovable root URL is the QR destination. If this Lovable project cannot serve a static HTML file at that path, port the supplied HTML/CSS/JavaScript mechanically into its root component while keeping the same text, layout and behavior. Remove the generated landing page and worker cards. Do not add mock jobs or a second scheduler view.
>
> Keep the separate `/present` route solely for a large projector QR that links to the published root `/`. Leave `DEMO_BASE_URL` empty until we give you the real tunnel URL; the existing “Demo starts shortly” state is intentional. Once we supply it, change only that one constant. Do not use Lovable AI, a database, login or a server-side proxy. The browser must call the laptop's `/api/jobs` endpoints directly.
>
> Here is the complete existing `ask.html` file:

Paste the file contents directly below that message. No visual redesign is needed. The `/present` route can remain as the separate projector slide; it is never shown on visitors' phones.

Then:

1. In the Lovable editor, click **Publish** at the top right; select a URL ending in `lovable.app`, choose **Public**, and click **Publish**. Copy this stable URL. See [Lovable's publish guide](https://docs.lovable.dev/features/publish.md).
2. Start the laptop command and copy the printed `https://…trycloudflare.com` base URL, **without** `/ask`.
3. In the Lovable project chat, ask: “Set the single `DEMO_BASE_URL` constant in `public/ask.html` to `https://…trycloudflare.com`. Keep `/` redirecting to `/ask.html` and `/present` pointing its QR at `/`.” Check the request in preview, then click **Publish changes**. Changes are not live until republished.
4. Open the published root URL on a phone; it should show the same form as the laptop's `/ask` page. Tap the standard question and verify that the laptop shows a queued job and the phone eventually shows a real answer. Test one custom question too. Open the published `/present` route on the projector and scan its QR with a second phone; it must open the visitor form. At the end of the pitch, show `/present` briefly, then return the projector to the local four-panel view.

The standard question is “Why do servers produce heat?” The laptop's `/ask` page is the source that Lovable republishes and is also a direct public fallback through the temporary tunnel. Keep the Lovable page public only while using this audience demo.

## What is real, what is modelled

The answers come from the local `swiss-ai/Apertus-8B-Instruct-2509` Q4 GGUF via the existing pinned llama.cpp CPU image. `WorkloadPlacement` previews the heat-first site choice so the request can be pinned to the shared model volume; `WorkloadCoordinator` independently admits and owns the real Docker job, then uses the shared heat-first policy for pause/resume at zero or full useful heat demand. Job state is observed from Docker. The heat bars are **not** measurements: `config/audience-apertus-demo.json` records an illustrative four-site demand and the case's 0.8 kWh/h recoverable heat for a 1 kW reference server. One real laptop worker is shown as one *reference-server equivalent* in that separate visual scenario. This does not alter the scored house or yearly balance. The short audience question has no measured runtime or energy value; the T-33 benchmark is for a different, longer summarisation job.

Visitor traffic crosses Lovable and Cloudflare to reach the laptop, so this public interaction is **not** a privacy or legal-compliance proof. The model inference and question history are local to the laptop, but questions displayed on the projector are public. Submit no private documents or personal data. The limits are 20 active jobs, 240 printable characters per question and a 10-second cooldown per browser session (falling back to IP if the session ID is absent). The public tunnel is for this short demonstration, not production hosting.

## Presenter cue and verification

After the existing offline demo, show the Lovable `/present` QR or a QR for the tunnel's `/ask` URL and say: “Apertus is running here. Scan this and choose the ready-made question. Our software places the job where the model says heat is useful.” Show the QR briefly, then return to the laptop's four-panel screen. Point out that the **background colour means heat demand**, not server temperature; the four workers are **logical** workers on this laptop, and the heat figures are from a separate illustrative scenario. Watch the immediate queue and placement during the pitch; answers arrive on each visitor's phone later. If asked about privacy: the commercial path can keep eligible work local and applies placement policy, but this public QR experiment sends public text over Cloudflare; it does not certify compliance.

Four simultaneous submissions were verified on this laptop on 2026-11-22: all four were placed on distinct workers and returned real Apertus answers. Measured end-to-end completion times from the service's submitted and finished timestamps were 314, 375, 418 and 442 seconds. These are far longer than a live pitch beat, so show the placement immediately and let visitors receive answers after the talk. Do not wait on the projector for completion. If these times are unacceptable at the venue, use the existing one-house offline demo as the live proof and keep the QR segment optional. The new room layout still needs a visual review on the presentation display; browser automation could not verify the localhost URL on this host.
