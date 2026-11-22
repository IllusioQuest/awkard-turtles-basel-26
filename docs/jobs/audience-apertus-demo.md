# Audience Apertus demo (T-78)

This is the optional audience moment **after** the scored five-minute pitch and its offline January/July/interrupted-job demonstration. The projector briefly shows the QR at the Lovable `/present` route. A visitor scans it and lands on the Lovable `/` question form, taps a standard question or types one short public question, and later sees a real Apertus answer. The projector then returns to the laptop's four-room view: each room's background colour shows its heat demand relative to the other three, dark server boxes show work, and small coloured tokens show questions. The heat budget is illustrative. All four workers share one physical laptop. No actual recovered laptop heat is measured.

## One laptop command

From the repository root in PowerShell, with Docker Desktop running in Linux-container mode:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-AudienceApertus.ps1
```

The script checks .NET, Docker and available RAM; downloads the Apertus Q4 GGUF, the pinned llama.cpp source archive and `cloudflared` on first use; builds the existing llama.cpp CPU recipe from that local source and a small audience image; copies the verified model once into a dedicated Docker volume; builds the .NET service; then starts the service and a temporary public Cloudflare tunnel. Building from a local source archive avoids Docker's GitHub certificate problem on this host. The read-only source mount exposes only `data/external/audience-demo/models/` during setup; inference containers see only the Docker-managed model volume, not the token or job history. It opens `http://127.0.0.1:18970/operator` for the four-panel display and prints the public `https://…trycloudflare.com/ask` URL. The first run downloads about 5 GB and compiles llama.cpp, so perform it before the presentation. Later runs reuse the model and built image automatically. The local data, model, tunnel binary, logs, question history and operator token stay under git-ignored `data/external/audience-demo/`.

Keep the laptop awake, online and powered during audience participation. A Quick Tunnel URL is temporary, changes on every start and has no uptime guarantee. The Lovable page must be updated with the new URL and republished before the QR is shown. Its stable Lovable URL is the QR target. If the tunnel fails, the existing offline pitch demo remains available; the audience segment is optional.

After the audience segment, stop the temporary public entry and release owned jobs with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Stop-AudienceApertus.ps1
```

## Lovable: create the visitor page and a separate projector page

Create a **new** Lovable project and paste this prompt:

> Build two responsive routes for the DigitalHeat audience demo.
>
> **Visitor route `/` (the destination after scanning):** Headline “Swiss AI in your Swiss home.” Subline “Ask Apertus a question and watch the heat-first scheduler choose a worker.” Calm, premium look: dark navy, warm amber, soft mint, generous whitespace. Show a prominent one-click button “Why do servers produce heat?” plus a text box (maximum 240 characters) and “Send my question” button. This page is for visitors using their phones; **do not put a QR code on it**. Put the temporary laptop tunnel base URL in one clearly editable constant named `DEMO_BASE_URL`; never hard-code it in several places. On click, POST JSON `{question: text}` to `DEMO_BASE_URL + "/api/jobs"`, read the returned `id`, then poll `DEMO_BASE_URL + "/api/jobs/" + id` every two seconds. Generate one random UUID per browser session, store it in `sessionStorage`, and send it as `X-Visitor-Id` on the POST so visitors behind the same Wi-Fi share no cooldown. Show queued/running/paused/completed/failed state, worker number and final answer. If the backend URL is empty or a request fails, display a clear “Demo starts shortly”/connection message. Disable duplicate submits while one question is pending.
>
> **Presenter route `/present` (projector only):** Show a large, high-contrast QR code linking to the published visitor route `/`, with the short instruction “Scan to ask Apertus.” Build the QR target from the current site's origin plus `/`, so it remains correct after publication. No question form or answer appears on this route. It should be readable from the back of a room.
>
> On the visitor page include small truthful text: “Apertus inference runs on our presentation laptop. Four logical workers share that laptop. Prompts and answers are shown publicly. The heat display is a reference-server scenario, not a laptop measurement. Do not enter personal or confidential information.” Do not add login, database, Lovable AI, API keys, or a Lovable backend; all generation must call the laptop API.

Then:

1. In the Lovable editor, click **Publish** at the top right; select a URL ending in `lovable.app`, choose **Public**, and click **Publish**. Copy this stable URL. See [Lovable's publish guide](https://docs.lovable.dev/features/publish.md).
2. Start the laptop command and copy the printed `https://…trycloudflare.com` base URL, **without** `/ask`.
3. In the Lovable project chat, ask: “Set `DEMO_BASE_URL` to `https://…trycloudflare.com`. Keep the visitor form at `/` and the projector QR at `/present`.” Check the request in preview, then click **Publish changes**. Changes are not live until republished.
4. Open the published visitor URL on a phone, tap the standard question, and verify that the laptop shows a queued job and the phone eventually shows a real answer. Test one custom question too. Open the published `/present` route on the projector and scan its QR with a second phone; it must open the visitor form at `/`. At the end of the pitch, show `/present` briefly, then return the projector to the local four-panel view.

The standard question is “Why do servers produce heat?” The local `/ask` page remains a fallback for testing without Lovable. Keep the Lovable page public only while using this audience demo.

## What is real, what is modelled

The answers come from the local `swiss-ai/Apertus-8B-Instruct-2509` Q4 GGUF via the existing pinned llama.cpp CPU image. `WorkloadPlacement` previews the heat-first site choice so the request can be pinned to the shared model volume; `WorkloadCoordinator` independently admits and owns the real Docker job, then uses the shared heat-first policy for pause/resume at zero or full useful heat demand. Job state is observed from Docker. The heat bars are **not** measurements: `config/audience-apertus-demo.json` records an illustrative four-site demand and the case's 0.8 kWh/h recoverable heat for a 1 kW reference server. One real laptop worker is shown as one *reference-server equivalent* in that separate visual scenario. This does not alter the scored house or yearly balance. The short audience question has no measured runtime or energy value; the T-33 benchmark is for a different, longer summarisation job.

Visitor traffic crosses Lovable and Cloudflare to reach the laptop, so this public interaction is **not** a privacy or legal-compliance proof. The model inference and question history are local to the laptop, but questions displayed on the projector are public. Submit no private documents or personal data. The limits are 20 active jobs, 240 printable characters per question and a 10-second cooldown per browser session (falling back to IP if the session ID is absent). The public tunnel is for this short demonstration, not production hosting.

## Presenter cue and verification

After the existing offline demo, open the Lovable `/present` route and say: “Apertus is running here. Scan this and choose the ready-made question. Our software places the job where the model says heat is useful.” Show the QR briefly, then return to the laptop's four-panel screen. Point out that the **background colour means heat demand**, not server temperature; the four workers are **logical** workers on this laptop, and the heat figures are from a separate illustrative scenario. Watch the immediate queue and placement during the pitch; answers arrive on each visitor's phone later. If asked about privacy: the commercial path can keep eligible work local and applies placement policy, but this public QR experiment sends public text over Cloudflare; it does not certify compliance.

Four simultaneous submissions were verified on this laptop on 2026-11-22: all four were placed on distinct workers and returned real Apertus answers. Measured end-to-end completion times from the service's submitted and finished timestamps were 314, 375, 418 and 442 seconds. These are far longer than a live pitch beat, so show the placement immediately and let visitors receive answers after the talk. Do not wait on the projector for completion. If these times are unacceptable at the venue, use the existing one-house offline demo as the live proof and keep the QR segment optional. The new room layout still needs a visual review on the presentation display; browser automation could not verify the localhost URL on this host.
