# Current architecture

See [How the browser gaming prototype works](gaming-architecture.md) for the current implementation and integration boundary. The following sections retain the experiment log and reproduction commands.

# Browser gaming lab (T-39)

Status 2026-11-21: compatibility experiment, two healthy Linux Selkies desktops. Code branch
featur-gaming. User approved browser clients, Selkies, multi-server placement and protecting active
matches. Session coordinator and CS2 support are not implemented at this milestone.

## Run and test on Windows

Use the gaming checkout (currently C:\Users\nikit\Documents\ETH\HackathonSprintd-gaming):

    .\gaming\scripts\Start-GamingLab.ps1 -Pull -IntelWsl
    .\gaming\scripts\Test-GamingLab.ps1
    .\gaming\scripts\Test-SessionGraphics.ps1 -Node session-a

Open http://localhost:18881 for A or http://localhost:18882 for B. Username player; generated
password_a/password_b are in gaming/TestResults/credentials.json. That directory is git-ignored;
never commit its credentials, game accounts or personal files. Do not expose these ports publicly.
Only loopback HTTP is configured; public deployment requires proper TLS and session authorization.
The container operator can inspect session data. Clipboard/files/mic/webcam/printing are disabled
in the supplied configuration. No Docker socket or privileged mode is given to the desktops.

Stop without deleting homes:

    .\gaming\scripts\Stop-GamingLab.ps1

Omit -IntelWsl to recreate A in software mode. B always remains the software comparison.
Each startup restores generated credentials from the local file, pins the image digest from
image.txt, and waits for both nodes' authentication challenges. Default 1280x720 at 20 FPS,
2 CPU equivalents and 3 GiB RAM per desktop are lab limits, not measured performance claims.

## Verified and unverified

Official image digest: sha256:5e877dfc8280afc2326bb24a3a57d37d2edb081e595014842aff1d6cd3569015,
Selkies reports version 2.2.1, Ubuntu 26.04. WebSockets is its default transport; WebRTC is optional.
Docker Desktop 4.94.0, Linux engine 29.8.2, WSL2; Windows Intel Iris Xe driver 31.0.101.4146.

- Both containers healthy; anonymous HTTP 401, authenticated HTML 200. Separate home volumes
  and localhost-only ports checked. These checks do not establish browser video/input.
- Browser automation unavailable in this chat. User supplied a screenshot of the streamed desktop and confirmed keyboard/input work on 2026-11-21. This verifies the user-observed desktop path, not CS2 gameplay or measured latency.
- Software baseline: OpenGL llvmpipe, Accelerated no; Vulkan CPU llvmpipe.
- Intel experiment: map /dev/dxg, mount /usr/lib/wsl/lib AND /usr/lib/wsl/drivers read-only,
  set LD_LIBRARY_PATH, GALLIUM_DRIVER=d3d12 and Intel adapter selection. Actual glxinfo -B
  and surfaceless eglinfo identify D3D12 (Intel Iris Xe), Accelerated yes, OpenGL 4.1.
  Missing the drivers mount initially caused EGL failure and an X display restart; fixed.
- VAAPI d3d12 initialization fails on the current X11 path. Hardware video encoding unverified.
  CPU encoding deliberately selected. A two-second 1280x720 X11 capture encoded with libx264
  and ffmpeg exited 0: `docker exec --user ubuntu --env DISPLAY=:20 digitalheat-gaming-session-a-1
  ffmpeg -hide_banner -loglevel error -f x11grab -video_size 1280x720 -framerate 10 -i :20
  -t 2 -c:v libx264 -preset ultrafast -f null -`.
- Vulkan enumerates only llvmpipe CPU; this image has no dzn Vulkan ICD. Accelerated OpenGL
  does not imply Vulkan support. CS2 has not been downloaded/launched; no FPS/latency claim.
- Planned node-stop isolation check was blocked by execution policy before execution. No
  fault-isolation claim is made; both nodes remain running and healthy.

Raw reports stay in gaming/TestResults (ignored). Device access probes are a lab experiment,
not a security boundary. Host Intel driver updates or compiling Mesa have not been performed.

## How work is split between servers

```mermaid
flowchart TB
    BrowserA[Browser player A] --> NodeA[Node A: game plus Selkies]
    BrowserB[Browser player B] --> NodeB[Node B: game plus Selkies]
    Heat[Heat signal per site] --> Shared[Existing C# heat scheduler]
    Shared --> Placement[Proposed session placement coordinator]
    Placement --> NodeA
    Placement --> NodeB
    Placement --> Batch[Pause batch before taking resources]
```

Current A/B containers share one outer Docker engine, laptop and Intel GPU. They demonstrate
separate session homes and endpoints, not separate physical capacity or fault domains. No
single unmodified game process is split between machines. A future node agent/adapter uses
independent remote engines through authenticated control, never unauthenticated Docker TCP.

The proposed coordinator filters readiness, locality and free session capacity; uses the shared
scheduler output to rank heat preference; atomically claims a slot; confirms batch pause and
resource release; launches/adopts the session; then keeps active placement fixed. Reject/rollback
on failed preemption or launch. Reconcile existing sessions after controller restart before new
admission. Hardware inventory must avoid double-counting a GPU shared by several containers.

User decision pending: reject new sessions when every eligible site's heat target is zero, or
allow them and explicitly report unused heat. Active sessions keep running either way. Do not
implement this policy implicitly. Session failure allows reconnect; game progress persistence
is game-specific. Existing webhosting request-routing does not migrate game state or UDP flows.

## Next gates

1. User browser stream/input smoke check.
2. Choose between a software-demo fallback, an explicitly scoped Vulkan driver experiment, or
   a compatible remote/native-Linux GPU host for real CS2. Each has different cost/scope.
3. Implement reviewed session allocation and observed batch preemption after zero-heat policy
   is chosen. Coordinate with T-11/T-12 and existing runtime, without duplicating heat formulas.

Sources checked 2026-11-21: https://docs.selkies.io/latest/start/,
https://docs.selkies.io/latest/settings/, https://docs.mesa3d.org/drivers/d3d12.html,
https://devblogs.microsoft.com/commandline/d3d12-gpu-video-acceleration-in-the-windows-subsystem-for-linux-now-available/.
Decision: decisions/0021-selkies-gaming-lab.md, fresh-context verdict go with changes.

## User-confirmed browser test and summer placement (2026-11-21)

User supplied an actual browser screenshot and reported working keyboard/input. Browser desktop
smoke is now user-verified. The screenshot's black wallpaper area looked mis-sized. Requested
PCManFM-Qt wallpaper mode stretch live on A; it accepted/persisted the setting without restarting
the session. Visual correction awaits user confirmation. Repeat if needed:

    .\gaming\scripts\Repair-SessionWallpaper.ps1 -Node session-a

Summer policy clarification: the gamer and compute/heat site are distinct. A gamer at home can
stream a game executing at a hotel/spa where hot water is needed, even if no hotel guests game.
Place NEW sessions on ready, locality-permitted nodes with real capacity and useful heat demand;
also enforce measured network reachability/latency eligibility. Geography/site type alone does
not establish low latency or useful heat. There must be provisioned computing hardware and a
working heat-recovery system at that site. Do not invent a hotel/spa heat profile or hardware.
The laptop lab will use explicitly synthetic home/hotel/spa signals until sourced data exists.

```mermaid
flowchart LR
    Player[Player at home: browser] <-->|Video and input over network| Spa[Spa compute node: original game and Selkies]
    Spa -->|Recovered heat| Water[Spa water heating]
    Home[Home site: no summer heat demand] --> Placement[New-session placement]
    Demand[Spa/hotel: heat demand and free capacity] --> Placement
    Placement --> Spa
```

Existing sessions retain their node until session end, even if heat preference changes; batch
work is preempted first, with observed release before new-session launch. Subsequent sessions
can move to hotels/spas. This is placement of compute before admission, not transport of heat
between buildings or live game-state migration. Batch work can be checkpointed/requeued only
where its declared executor supports it. If every eligible site lacks useful heat or capacity,
the system cannot guarantee useful heat: user choice between waiting/rejection and explicit
waste-heat operation remains pending. The user's hotel/spa answer resolves preferred remote
placement but does not specify this exhausted-capacity fallback.

Controller implementation remains pending; these paragraphs record agreed behavior and test
requirements, not completed remote-node orchestration. GPU/Vulkan/CS2 gates remain unchanged.

## Case-source clarification: available heat reserve (2026-11-21)

User requests choosing the server whose infrastructure has the largest available heat reserve.
Read the live sprintd case page (URL below); its summer use case explicitly permits moving jobs
to hotels, spas, agriculture or businesses with heat demand. It does not define a ranking metric
called reserved heat. Proposed interpretation awaiting confirmation: remaining useful heat
absorption capacity over the next one-hour interval, not the amount of heat already in storage.

available_heat_capacity_kwh = max(0, demand_kwh + tank_capacity_kwh - tank_stored_kwh
    - already_committed_recoverable_heat_kwh).

All terms must refer to the same site and interval. Commitments include running sessions and
other scheduled heat sources; track them once per site, not once independently for every node.
Rank eligible sites by this remaining amount; then choose a ready node with free compute capacity
within the site, with stable ID ties. Recheck and reserve compute plus thermal capacity atomically.
Do not count the full site reserve for every simultaneous admission. Existing shared heat
scheduler supplies the demand/storage-based load target, but its clipped 0..1 return alone cannot
rank absolute capacity after saturation. Any needed shared interface extension must be coordinated
with T-11/T-12 and reviewed before implementation; do not copy the thermal model into gaming.

A full tank has zero storage headroom, but current demand can still absorb heat. When every site's
reserve is zero, descending ranking alone supplies no meaningful winner: queue versus explicit
waste-heat execution remains a fallback choice. No queue/waste policy is silently inferred.

Source accessed 2026-11-21:
https://app.sprintd.org/events/sprintd-basel-greentech/cases/useful-load-useful-heat-can-your-computing-power-heat-a-home

## Implemented heat-capacity reservation demo

The user confirmed the available-capacity interpretation. The shared core now factors the
existing GreedyHeatScheduler numerator into HeatCapacity.RemainingKwh. The gaming reservation
model calls that helper, so the heat formula is shared with the yearly scheduler. Valid legacy
scheduler output is regression-checked. No new thermal or economic scenario is substituted.

From the gaming checkout:

    dotnet run --project gaming/demo
    dotnet test gaming/tests

The fixed synthetic demo has home capacity 0.5 kWh, hotel capacity 2 + 4 - 3 = 3 kWh, and spa
capacity 3 + 2 - 1 = 4 kWh for the hour. Each illustrative session reserves 2 kWh/hour, NOT a
measured gaming rate. Player 1 selects spa-a (4 available), player 2 selects hotel-a (3 available
versus spa's remaining 2), player 3 selects spa-b (remaining 2). Raising home demand to 5 kWh
places player 4 at home-a. Player 1 remains on spa-a. JSON output is snake_case.

This demonstrates that two servers at a spa SHARE the site's thermal reserve. An admission
atomically claims one node slot and its recoverable heat amount. Request-ID retries are
idempotent; changing a request under an existing ID fails. Readiness, network eligibility and
T-24 locality filter candidates before ranking. Equal reserves use stable site/node IDs.
Current UTC-hour snapshots require increasing revisions. External committed heat excludes
claims already owned by this coordinator. Allocatable slots exclude external workloads but
include this coordinator's held slots, which are subtracted internally exactly once. Held
per-hour heat rates carry into the next interval and are released only by explicit caller action
following observed termination (or cancellation of an unlaunched claim).

If no site can absorb the declared request heat with available compute slots, TryReserve returns
no assignment. There is no automatic waste-heat placement or queue service. Sessions already
held remain pinned after demand or inventory changes. The component is in-memory and local;
restart reconciliation/durable ownership must precede actual game launching. No automatic
Selkies routing, batch pause or game execution is claimed by this console demo. Independent
remote-engine control remains an adapter integration task, not delivered by two local desktops.

## Windows PowerShell diagnostic fix

User's Windows PowerShell 5.1 stopped on native ls stderr for absent /dev/dri. Fixed in b5bb9a3:
missing paths now print Not exposed; expected Linux graphics diagnostics are merged into stdout
inside the container, avoiding PowerShell NativeCommandError promotion. CRLF input is normalized
before Linux shell execution. Docker execution errors still fail explicitly, and reports are saved.
Verified with actual Windows PowerShell 5.1 on Intel node A (/dev/dri absent, D3D12 OpenGL still
works through /dev/dxg) and PowerShell 7 on software node B. Both complete and report Vulkan
llvmpipe and unsuccessful VAAPI honestly. No device or driver installation is required to fix this
script error. Rerun Test-SessionGraphics.ps1; missing DRM does not contradict the dxg OpenGL path.

## Isolated Dozen Vulkan experiment (ADR 0023)

User approved the isolated experiment. Official Mesa 26.0.8 release archive is SHA256-pinned in
`gaming/deploy/Dozen.Dockerfile`; the pinned Selkies base stays intact. Only Vulkan Dozen is built
into `/opt/digitalheat-dzn`; system OpenGL is not replaced. Distro build package versions and
Meson options are included in the output. Apt packages are not fully snapshot-pinned, so rebuilds
are source-pinned rather than byte-identical. No Windows driver was installed or updated.

The path is Vulkan -> Dozen -> D3D12 -> read-only mounted Intel Windows driver -> Iris Xe.
The runtime receives /dev/dxg, /usr/lib/wsl/lib and /usr/lib/wsl/drivers. Normal Linux DRM /dev/dri
is not required for this tested path. Device access still shares the physical host GPU.

Run in the gaming checkout:

    .\gaming\scripts\Build-DozenExperiment.ps1
    .\gaming\scripts\Test-DozenExperiment.ps1
    .\gaming\scripts\Test-DozenExperiment.ps1 -RenderSmoke
    .\gaming\scripts\Test-DozenExperiment.ps1 -FullReport
    .\gaming\scripts\Start-DozenDesktop.ps1
    .\gaming\scripts\Show-VulkanCube.ps1

Compilation is limited to two jobs with a 1200-second compile timeout and a 12 GiB free-disk
admission check. Probes have configurable 30-second default timeouts, CPU/memory limits and no
network. They are transient labelled containers and cannot control Docker. The desktop is a
separate Compose project, node C on http://localhost:18883, separate home and generated local
credentials in gaming/TestResults/dozen-credentials.json (username player). Start requires
successful enumeration/render reports matching the current experiment image. A/B are untouched.
CPU video encoding stays enabled. Do not apply the A override to run this experiment.

Repeat graphical diagnostic and stop C without deleting its home:

    .\gaming\scripts\Test-SessionGraphics.ps1 -Node session-c
    .\gaming\scripts\Stop-DozenDesktop.ps1

Observed results on this laptop:
- Mesa builds successfully from official source with the minimal Microsoft Vulkan driver option.
- Headless enumeration exits 0: vendor 0x8086, device 0x46a6, Integrated GPU,
  Microsoft Direct3D12 (Intel Iris Xe), DRIVER_ID_MESA_DOZEN, device API 1.2.335.
  Loader instance API 1.4.341 is not the physical device's supported API version.
- Transient Xvfb + vkcube renders 60 frames on the named Intel GPU and exits 0.
- vkcube also exits 0 inside actual Selkies desktop C on DISPLAY :20. This tests its X11
  presentation path, not just device discovery. User visual stream confirmation requested.
- Desktop C healthy, authenticated HTTP 200; actual Windows PowerShell 5.1 graphics/cube probes
  complete. Intel D3D12 OpenGL remains available; VAAPI still fails, so no hardware encode claim.
- VK_KHR_swapchain is present. VK_EXT_graphics_pipeline_library is not advertised;
  tessellationShader is false. Dozen warns it is non-conformant/testing-only. A cube pass does
  not establish compatibility or performance for CS2; no game launch or FPS claim is made.

Original-game gate: Valve's current Linux listing requires Vulkan graphics, strongly recommends
VK_EXT_graphics_pipeline_library, and lists 85 GB available storage. C: currently has about
20 GB free after the build. A fresh CS2 download is deferred pending a user-selected storage
location or user-freed space. Existing Windows Steam manifests were observed but not modified;
a Windows install is not assumed to be a usable Linux game installation. User Steam login and
any account/terms actions remain with the user. Intel WSL is outside the explicitly named Linux
GPU families on the current store listing, so actual compatibility remains an experiment.

Build and raw probes are recorded locally under gaming/TestResults (ignored). The 60-frame
smoke is only a rendering check, not a benchmark or a measurement of useful heat.
Sources checked 2026-11-21: https://archive.mesa3d.org/mesa-26.0.8.tar.xz,
https://docs.mesa3d.org/meson.html,
https://store.steampowered.com/app/730/CounterStrike_2/.

Final source-image build and probes were also run under actual Windows PowerShell 5.1. The
final experiment image ID is sha256:c8bf2c23e41c3142a00b5236e7bbd0f2d220fb90330b842287552523eb928c26.
The current running C may retain the earlier tested build until Start-DozenDesktop is rerun;
the driver source/options are the same. Latest image includes compile timeout and Mesa license.

User reported inability to log into C. Direct checks against the running C: correct credentials
from dozen-credentials.json -> HTTP 200; no credentials -> 401; deliberately wrong password ->
401. This proves server-side HTTP auth works, not that the user's browser login issue is solved.
C uses username player and the field password in dozen-credentials.json, distinct from A/B's
password_a/password_b. User asked to retry in Incognito to avoid cached credentials. Do not
request the password in chat or remove authentication to mask the issue. Repeat:

    .\gaming\scripts\Test-DozenDesktop.ps1

The helper verifies local credentials without printing them and is tested on Windows PowerShell
5.1. If the browser still fails, inspect a screenshot of the login/error state with password hidden.

User requested a smaller presentation game instead of CS2. Recommended Xonotic (original Linux
binary, FPS) uses OpenGL 2.1 and its official 0.8.6 download is 1182 MiB. This fits the previously
working OpenGL 4.1 path in principle, but original-game performance/input still needs testing.
SuperTuxKart is a racing alternative. Selection requested; neither game has been downloaded or
installed. Browser clients still install nothing and no game source modifications are planned.
Sources checked 2026-11-21: https://xonotic.org/download/ (size, Linux/OpenGL requirements),
https://supertuxkart.net/Download (Linux release availability).

## Selected presentation game: Xonotic

User selected Xonotic and confirmed Node C login now works. Desktop wallpaper repair now uses
PCManFM's uniform-color mode rather than image stretching, including C, without restarting a
session. It removes the mis-sized image; visual confirmation remains with the browser user.

Prepare an original, unmodified Xonotic 0.8.6 Linux release in a derived image. Official archive
https://dl.xonotic.org/xonotic-0.8.6.zip is verified against the publisher's SHA512 listing at
https://dl.xonotic.org/xonotic-0.8.6.sha512 before extraction. No Steam account, game source edits
or browser-client install. This game uses OpenGL; Dozen is not required by Xonotic itself, though
C retains the tested experimental graphics image. No game-performance claim before launch.

Commands once image build is complete:

    .\gaming\scripts\Build-Xonotic.ps1
    .\gaming\scripts\Start-DozenDesktop.ps1 -Xonotic
    .\gaming\scripts\Start-Xonotic.ps1

Optional offline local-match startup:

    .\gaming\scripts\Start-Xonotic.ps1 -StartMatch

A small sourced-as-demo-settings JSON defines 1280x720, 30 FPS cap, stormkeep and two bots;
these are configuration choices, not measured hardware throughput. The derived image records
its graphics base image ID and startup checks matching prior Vulkan enumeration/render evidence.
Start-DozenDesktop -Xonotic recreates only C to use the game image, retaining its home and login;
A/B remain untouched. Never run a recreation while protecting a real active match: this is the
initial presentation setup, not a session-migration mechanism. Game launch uses an exclusive
process lock to avoid two instances on the same desktop; stdout/stderr go to the desktop user's
xonotic-demo.log. Dispatch success alone does not prove successful gameplay.

This is still a manually launched presentation node. Automatic launch following shared heat
reservation, observed batch preemption, termination reconciliation and remote-node adapters
remain pending. Do not imply that the game is already orchestrated by the scheduler.

### Xonotic startup verification

Game image built and official checksum passed. The final image restores USER ubuntu after
installing files; launching as root was an initial setup error, corrected before handoff. The
launcher explicitly uses /opt/Xonotic as working directory (otherwise engine assets are not
found). Desktop C's initial lab home ownership was restored to uid 1000. Current C retains the
normal non-root desktop user and receives the existing display and PulseAudio runtime paths.

Verified by Test-Xonotic.ps1 under actual Windows PowerShell 5.1: one game process running,
Intel Iris Xe D3D12 OpenGL renderer, game configuration/assets loaded, 48 kHz stereo audio
initialized. stormkeep.bsp exists in the original maps archive. Actual race/gameplay FPS and
browser input are not inferred from these checks; user testing requested. SDL joystick startup
logs an undefined udev shim symbol, so gamepad support is not verified; keyboard/mouse are the
presentation input path. No game source or engine binary changed.

    .\gaming\scripts\Test-Xonotic.ps1

If already running, quit Xonotic through its own menu before requesting -StartMatch; the launch
lock prevents concurrent instances. Launch logging appends, so a duplicate request cannot erase
the running game's log. A local game can also be selected interactively from the existing menu.
Use Ctrl+Shift+X inside the browser for Selkies Gaming Mode (pointer/keyboard capture), and hold
Escape at least two seconds to leave it. Source: https://docs.selkies.io/latest/usage/.

A Windows PowerShell 5.1 quoting issue in the transient Vulkan render wrapper was found while
checking startup evidence: it returned exit 0 with empty renderer output. Fixed by passing the
probe as stdin with CRLF normalization, and now requiring actual Intel selection output even
for render mode. Repeated PS5.1 render shows the named Intel GPU and exit 0. No empty-output
render result is accepted as success.
