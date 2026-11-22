# Confirmed worker shutdown and frame retry

T-86, ADR [0072](../decisions/0072-node-shutdown-frame-retry.md).
Implementation `6119140`, [PR #77](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/77).
Code awaits human merge. Documentation is on main.

## Use the demo

Start the presentation lab normally. At `http://127.0.0.1:18950/ui`, refresh with
Ctrl+Shift+R to load the new button, press Render, then **Power off GPU 3** while
frames are rendering. This is a hard stop of that logical node's controller and
owned Blender process. The graph/card retains it as Powered off. Completed frames
remain available; unfinished work moves to the other workers through MeshPeer.
The button is unavailable before rendering and after the one supported shutdown.
Restart render starts a new render with the surviving nodes; it does not power the
worker back on. Stop and relaunch the lab to restore all five nodes.

```powershell
$lab = (Get-Content rendering/TestResults/latest-presentation-lab.txt -Raw).Trim()
./rendering/Stop-RenderLab.ps1 -LabDirectory $lab
./rendering/Start-PresentationRenderLab.ps1 -Port 18950 -Frames 48 -BlenderExecutable '<existing Blender executable>'
```

All three GPU workers share the laptop's physical GPU. Shutting one off removes
one process/controller; it does not turn off a physical GPU. Minecraft is outside
this action. Only the requested button and powered-off state were added to the UI.

## Execution and heat path

1. Save shutdown intent and verified target identity before stopping anything.
   Temporarily hold survivor dispatch; their already running frames may finish.
2. Stop the verified controller tree, then reconcile durable native launch and warm
   process records. Match executable, PID and start time; confirm termination.
   A launch intent without a provable PID keeps retries held. No health timeout
   can authorize takeover. Keep the stopped node's original journals unchanged.
3. Withdraw the peer durably from surviving MeshPeer offer selection. Recover
   completed frames from released-success records, not just the last status poll.
   PNG bytes and result metadata were published to shared storage before success;
   check scene, recipe, build, frame number and image hash before reuse.
4. Persist the exact retry IDs, original deadlines and replacement heat needs.
   Cancel old unfinished IDs on survivors. Register new needs while dispatch is
   held, then resume. A registration failure leaves the saved plan held; Resume
   repeats it without allocating a second retry or another heat budget.
5. Keep completed and accepted in-flight work on surviving nodes. Split only the
   remaining unstarted frames using their existing heat weights. MeshPeer still
   performs placement and hard heat/resource admission. Shared hourly heat
   accounting and deadlines remain intact. Completed frames on the stopped node
   are included in the final manifest/video and served from shared storage.

For the observed shutdown, GPU3 had completed one frame. After accounting for completed
and already accepted work on survivors, the weighted split of unstarted work plus
those survivor contributions produced needs of 28 and 19 frames. Conserved total:
`28 + 19 + 1 = 48`. Heat labels are `frames * 0.8 kWh`: 22.4, 15.2 and 0.8 kWh,
38.4 kWh total. This is the existing **synthetic lab accounting assumption**, not
measured energy consumed by this short render. See ADR0069 and the presentation
profile for its source. Retrying interrupted work can consume extra real energy;
we have not measured that overhead. Finished and in-flight commitments are not
removed to make room, including at an hour boundary.

## Verified results (22 November 2026)

- `dotnet test src/DigitalHeat.Orchestration.Tests -c Release`: 148 passed.
  Covers PID-less launch rejection, descriptor changes, idempotent proof, stale
  offers after withdrawal/restart, missed-poll artifact recovery/corruption,
  saved-plan replay after partial registration, unchanged retry deadlines, and
  redistributed heat conservation across an hour boundary.
- Real run `5d4f95fca8ad`: power-off during frame 5 after GPU3 completed one frame.
  Controller and warm Blender exited, port 18954 closed. Frame5 retried once under
  `...-retry1-005`, with the original deadline. GPU1/2/3 contributions 28/19/1;
  completed preview retained and final MP4 contains exactly 48 encoded frames.
  Active workflow 76.9981 s; endpoint-to-completion observation 79.4530 s.
- New run `900d591aa803` with GPU3 still off: origin controller restarted during
  rendering, recovered paused, then explicitly resumed. Completed48 frames 29/19,
  active workflow 65.1650 s; final MP4 also contains48encoded frames. New runs reset
  per-run need without requiring the off peer to respond.
- Chrome hard-refresh visual check: requested button and Powered off label visible
  in the existing layout. Restored all five nodes in a fresh idle lab after tests.

Machine-readable observations and method:
[`node-shutdown-2026-11-22.json`](../performance/node-shutdown-2026-11-22.json).
Timings are individual observations, not a matched speed comparison. Full raw
journals/images/videos remain locally under `rendering/TestResults/` and are not
committed. To reproduce: start a fresh 48-frame lab, request shutdown once GPU3 has
one finished and one running frame, then inspect the saved session, native
ownership records, shared PNGs and final video. Repeat with GPU3 off, restart only
the origin controller using its recorded configuration, and press Resume.

## Boundaries for cobuilders

The local adapter is enabled by the presentation profile on the UI/origin only.
Remote workers and other launchers default to disabled. Remote power control,
consensus, replica takeover and artifact replication are not implemented here.
An unconfirmed or ambiguous remote outage keeps the existing stop-and-wait rule.
Never treat missing HTTP responses as proof the old process cannot execute.
Do not relaunch the stopped controller against its old uncertain execution journal;
restore through a fresh lab. Original ownership IDs never become retry IDs.
Keep original queue-inclusive deadlines, survivor heat commitments and current
logical-frame generations when extending this workflow.
