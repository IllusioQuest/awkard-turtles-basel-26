# Audience Apertus demo (T-78)

This is the optional audience moment **after** the scored five-minute pitch and its offline January/July/interrupted-job demonstration. A visitor scans the published Lovable page, taps a standard question or types one short public question, and sees a real Apertus answer. The laptop screen shows four coloured logical workers, the actual Docker job state and an illustrative heat budget. All four workers share one physical laptop. No actual recovered laptop heat is measured.

## One laptop command

From the repository root in PowerShell, with Docker Desktop running in Linux-container mode:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-AudienceApertus.ps1
```

The script checks .NET, Docker and available RAM; downloads the Apertus Q4 GGUF, the pinned llama.cpp source archive and `cloudflared` on first use; builds the existing llama.cpp CPU recipe from that local source and an audience image; builds the .NET service; then starts the service and a temporary public Cloudflare tunnel. Building from a local source archive avoids Docker's GitHub certificate problem on this host. It opens `http://127.0.0.1:18970/operator` for the four-panel display and prints the public `https://…trycloudflare.com/ask` URL. The first run downloads about 5 GB and compiles llama.cpp, so perform it before the presentation. Later runs reuse the model and built image automatically. The local data, model, tunnel binary, logs, question history and operator token stay under git-ignored `data/external/audience-demo/`.

Keep the laptop awake, online and powered during audience participation. A Quick Tunnel URL is temporary, changes on every start and has no uptime guarantee. The Lovable page must be updated with the new URL and republished before the QR is shown. Its stable Lovable URL is the QR target. If the tunnel fails, the existing offline pitch demo remains available; the audience segment is optional.

After the audience segment, stop the temporary public entry and release owned jobs with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Stop-AudienceApertus.ps1
```

## Lovable: create and publish the QR page

Create a **new** Lovable project and paste this prompt:

> Build a single responsive public question page for the DigitalHeat audience demo. Headline: “Swiss AI in your Swiss home.” Subline: “Ask Apertus a question and watch the heat-first scheduler choose a worker.” Calm, premium look: dark navy, warm amber, soft mint, generous whitespace. Show a large one-click button “Why do servers produce heat?” plus a text box (maximum 240 characters) and “Send my question” button. Put the temporary laptop tunnel base URL in one clearly editable constant named `DEMO_BASE_URL`; never hard-code it in several places. On click, POST JSON `{question: text}` to `${DEMO_BASE_URL}/api/jobs`, read the returned `id`, then poll `${DEMO_BASE_URL}/api/jobs/${id}` every two seconds. Generate one random UUID per browser session, store it in `sessionStorage`, and send it as `X-Visitor-Id` on the POST so visitors behind the same Wi-Fi share no cooldown. Show queued/running/paused/completed/failed state, worker number and final answer. If the backend URL is empty or a request fails, display a clear “Demo starts shortly”/connection message. Disable duplicate submits while one question is pending. Add a QR code of **this published Lovable page URL**, large enough to show on a projector. Include small truthful text: “Apertus inference runs on our presentation laptop. Four logical workers share that laptop. Prompts and answers are shown publicly. The heat display is a reference-server scenario, not a laptop measurement. Do not enter personal or confidential information.” Do not add login, database, Lovable AI, API keys, or a Lovable backend; all generation must call the laptop API.

Then:

1. In the Lovable editor, click **Publish** at the top right; select a URL ending in `lovable.app`, choose **Public**, and click **Publish**. Copy this stable URL. See [Lovable's publish guide](https://docs.lovable.dev/features/publish.md).
2. Start the laptop command and copy the printed `https://…trycloudflare.com` base URL, **without** `/ask`.
3. In the Lovable project chat, ask: “Set `DEMO_BASE_URL` to `https://…trycloudflare.com`. Keep both question controls and the QR target unchanged.” Check the request in preview, then click **Publish changes**. Changes are not live until republished.
4. Open the published Lovable URL on a phone, tap the standard question, and verify that the laptop shows a queued job and the phone shows a real answer. Test one custom question too. Show the Lovable page's QR on the projector at the end of the pitch, then return the projector to the local four-panel view.

The standard question is “Why do servers produce heat?” The local `/ask` page remains a fallback for testing without Lovable. Keep the Lovable page public only while using this audience demo.

## What is real, what is modelled

The answers come from the local `swiss-ai/Apertus-8B-Instruct-2509` Q4 GGUF via the existing pinned llama.cpp CPU image. The same `WorkloadCoordinator` and heat-first placement code that owns Docker jobs selects the four sites and controls pause/resume at zero or full useful heat demand. Job state is observed from Docker. The heat bars are **not** measurements: `config/audience-apertus-demo.json` records an illustrative four-site demand and the case's 0.8 kWh/h recoverable heat for a 1 kW reference server. One real laptop worker is shown as one *reference-server equivalent* in that separate visual scenario. This does not alter the scored house or yearly balance. The short audience question has no measured runtime or energy value; the T-33 benchmark is for a different, longer summarisation job.

Visitor traffic crosses Lovable and Cloudflare to reach the laptop, so this public interaction is **not** a privacy or legal-compliance proof. The model inference and question history are local to the laptop, but questions displayed on the projector are public. Submit no private documents or personal data. The limits are 20 active jobs, 240 printable characters per question and a 10-second cooldown per browser session (falling back to IP if the session ID is absent). The public tunnel is for this short demonstration, not production hosting.

## Presenter cue and verification

After the existing offline demo, say: “Apertus is running here. Scan this and choose the ready-made question. Our software places the job where the model says heat is useful.” Show the QR briefly, then the four-panel screen. Point out that the coloured panels are four **logical** workers on this laptop, and the heat figures are from a separate illustrative scenario. If asked about privacy: the commercial path can keep eligible work local and applies placement policy, but this public QR experiment sends public text over Cloudflare; it does not certify compliance.

Before the presentation, verify four simultaneous submissions on the **actual laptop** and time the slowest real answer. If memory pressure or latency is unacceptable, run the existing one-house offline demo as the live proof and use the audience QR only with a smaller verified load. The present implementation has been compiled, but four concurrent Apertus completions have not yet been measured on this host.
