# Two-node CS 1.6 compatibility lab (T-53)

2026-11-21. **In progress, not a verified game replacement.** Code is in draft
[PR #42](https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/42), branch
`feat/T-53-cs16-linux`; depends on #38, then #37. Documentation is shared directly on main.
The user approved Wine and two new nodes, then requested a reusable relay and no login on these
local test desktops. Minecraft and the existing Xonotic nodes were not changed or stopped.

## Available now

- Node D: <http://localhost:18884>; Node E: <http://localhost:18885>. No login is needed.
- Separate Linux containers, Wine prefixes and home volumes; 2 CPUs / 2 GiB RAM / 512 PIDs each.
  These are named lab limits in `gaming/deploy/cs16-lab.json`, not measured game requirements.
- Non-root execution, all capabilities dropped, no-new-privileges, no host binds/Docker socket.
  Both game nodes are on an internal network. Node-to-node TCP connectivity was checked;
  the tested public internet socket was blocked. This is not a multiplayer success test.
- A reusable standard-library TCP relay uses fixed operator-configured mappings. It exposes only
  the two loopback ports, has a read-only filesystem, no mounts, no game logic and separate limits.
  It is a local test access point, not a production HA gateway or a UDP game relay.
- Base images are digest-pinned; Wine 10 comes from Ubuntu repositories. The supplied third-party
  Windows installer is copied locally, hash-checked and never committed or executed on Windows.
  Provenance/hash details are in the ticket. A hash does not establish publisher authenticity.

Existing desktop credentials are no longer used for D/E. Never expose these unauthenticated
test ports beyond loopback. Other desktops' authentication is unchanged.

## Display repair and installer diagnosis

The user confirmed that the offset black rectangle was fixed and the installer was visible.
`desktop-background.py` writes a uniform-color PCManFM configuration **before** the desktop starts,
including persisted homes; image autostart invokes it on each new session. No scaled SVG wallpaper
is used. Both nodes' color settings were checked after recreation. This addresses the observed
wallpaper artifact; it is not a claim that every possible encoder/input issue is eliminated.

First trial: installer timed out after 180 seconds during initial Wine setup. A subsequent 32-bit
Windows command probe returned exit 0 and a Windows version, in 3642.7 ms for the whole Docker call.
That tests Wine command execution, not game/installer performance. The experimental WoW64 diagnostic
line alone is not fatal: the command returned success.

A later installer attempt failed with exit 153 / `File size limit exceeded`. The agent's global
`ulimit -f` intended to cap logs also restricted files the installer extracted. This was an
implementation mistake, removed in commit `6251f08`. The replacement `run-with-bounded-log.py`
caps captured output while continuing to drain it; it does not set an application file-size limit.
Timeout and container CPU/RAM/network restrictions remain. A Linux regression writes a 20 MiB
application file while retaining only a 1 KiB log; it passes, along with explicit timeout handling.
The retry no longer showed that file-size error at the last check. Installation is not yet verified.

GUI browser automation was stopped by the user. The user is handling the installer and any license
terms manually; no silent-acceptance flag was used. Do not repeat any accidentally shared credential
in docs, code or logs. No credential value has been added here.

The fixed retry subsequently reached its 180-second timeout while waiting for manual completion;
no installed `hl.exe` was found. The configured **installer-only** interactive window is now
600 seconds, and another trial was dispatched. This does not change job deadlines or game lifetime.
The original CS checkout was externally switched to an old local `main`; it was left intact.
Continue from `C:\Users\nikit\AppData\Local\Temp\digitalheat-cs16-runtime`, which reattaches the
saved feature branch. Browser ports and Docker-managed game data are unchanged.

### Follow-up: CRLF command-transfer failure

The next reported failure had the concrete log message `failed to open
"/home/ubuntu/cs16-installer.exe\r": c0000135`. A CR from a Windows here-string became part of
the Linux command argument. This is a launcher transport error, not evidence that a game DLL is
missing. It can also corrupt status-file/cleanup paths; earlier timeout-file values alone did not
establish that this retry actually waited for manual completion. The launcher now removes CRs
from its controlled shell payload before dispatch. A forced-CRLF PowerShell-to-Linux argv probe
verified the exact expected installer filename. A subsequent real launch cleared the expected
status record and produced an installer process, without the failed-to-open error at the check.
GUI completion, successful installation and multiplayer remain unverified. No node restart,
Minecraft change, scheduler change or authentication bypass was needed for this fix.

## Commands

Run from the feature checkout in PowerShell:

```powershell
./gaming/scripts/Build-Cs16Lab.ps1
./gaming/scripts/Start-Cs16Lab.ps1 -Installer <path-to-supplied-exe>
./gaming/scripts/Test-Cs16Lab.ps1
./gaming/scripts/Start-Cs16Installer.ps1 -Node session-d
```

Finish setup inside Node D's browser desktop, preferably to `C:\CS16`. Handle any terms personally;
leave auto-launch unchecked if offered. The installer has a bounded timeout; a dispatch acknowledgment
is not installation success. Its log/exit record are under `/home/ubuntu/cs16-install*` in that node.
`Stop-Cs16Lab.ps1` stops only this project's two nodes and relay, retaining volumes and other games.
Starting a newer image can recreate D/E; do not run that command while a game/install is active.

## Scheduler evidence and remaining work

The code branch explicitly reuses the indexed admission implementation from PR #38, not a second
scheduler. Installation, Wine startup, TCP forwarding and rendering remain outside admission.
Preflight uses the existing fixed-seed 62-case diagnostic, source `9eb95ee`, 20 warmups and 101
samples, .NET 8.0.31 Release with tiered compilation disabled. Raw results:
[scheduler preflight](../performance/scheduler-cs16-preflight-2026-11-21.json).

For the shared-domain 1,024-node/no-held-claims reserve fixture, median = 1.6022 ms,
p95 = 2.5228 ms. Three-node independent fixture: 0.0041 / 0.0052 ms. This non-dedicated laptop
had existing games/web running; **CS was not running**, so this is not the requested loaded result.
Use the same source/fixture/diagnostic for comparison under two active CS clients. The proposed
lab regression threshold is `max(2 * preflight_p95, preflight_p95 + 1 ms)`; for the stated 1,024-node
fixture that is 5.0456 ms. It is a test policy, not an SLA or a scored figure.

36 gaming tests, 46 orchestration tests, 3 relay tests, 2 Linux process-runner regression tests,
actual Docker isolation/resource/port checks and HTTP readiness passed. No game profile is declared
ready yet. Public/community compatibility, two-client playable match, actual CPU split and loaded
scheduler benchmark remain pending. No CS electrical-power/useful-heat measurement exists.

Planned first game test: two client sessions perform rendering/input on different nodes, with one
authoritative shared match host. This distributes **client workloads**, not a single server tick
simulation. Do not claim public compatibility from local success. Test a named compatible community
server without changing authentication/anti-cheat policies once client feasibility is established.

## Installed client, online mode and an exit independent of input capture

The user subsequently confirmed that CS starts. Its installed directory on D is
`C:\Games\Counter-strike 1.6 Original` (Wine path under that node's own home). Only D has the
`cs16-installed` capability in `runtime-profiles.cs16.json`; E still cannot launch that profile.
The user reports bad clicks/scrolling and inability to exit, so input compatibility remains open.
Standard `MOUSE1 +attack`, `MOUSE2 +attack2`, wheel-next/previous bindings were inspected and kept
at the user's request. A host-side scoped SIGTERM closed exactly one user-owned `hl.exe` on D;
it did not stop the desktop, Wine services, Minecraft or any other node. Subsequent scoped exit
was idempotent. CS was relaunched through the existing C# profile launcher at 800x600 windowed,
providing a visible game title bar instead of the previous fullscreen trap. Process launch is
confirmed; user verification of improved input/close behavior is pending. Wine reports software
rendering (llvmpipe); no frame/input latency measurement is claimed.

```powershell
# From the feature checkout; no global process kill or container restart:
./gaming/scripts/Stop-LabApplication.ps1 -Node session-d -ProcessName hl.exe
./gaming/scripts/Start-Application.ps1 -Application cs16 -Node session-d -Profiles ./gaming/deploy/runtime-profiles.cs16.json
```

Selkies' installed client binds **Ctrl+Shift+X** to gaming mode, which requests pointer/keyboard
capture. Three separate Escape presses within about one second release gaming mode. Click the
stream before using its shortcut; browser pointer lock needs a user gesture. A plain Escape may
be sent to the remote game rather than leaving fullscreen. The user was asked to test capture
in Chrome; local browser automation is unavailable, and exact click/scroll symptoms remain under
investigation. Do not describe controls as fixed merely because a game process exists.

The internet-server problem had a confirmed infrastructure cause: D was still on the installer's
internal-only network. In response to the user's public-server request, `Set-LabNodeNetwork.ps1`
attached D to the pre-existing normal bridge with an explicit default-gateway priority. Docker
PID/restart count were checked unchanged. No host-network mode, elevated capability, host mount,
Docker socket or new inbound game port was added. D now has normal outbound access; E remains
offline. This online phase is less network-isolated than installer mode, intentionally. The
installer launcher explicitly refuses an online target; don't disconnect a live game just to
rerun setup. Repeated online requests are idempotent. Container recreation via Compose returns
to the declared offline topology; do not use it to refresh settings during an active session.

```powershell
./gaming/scripts/Set-LabNodeNetwork.ps1 -Node session-d -Mode online
./gaming/scripts/Test-Cs16Lab.ps1 -AllowOnlineNodes
# Explicitly disconnects external game access; use only when that is intended:
./gaming/scripts/Set-LabNodeNetwork.ps1 -Node session-d -Mode offline
```

Read-only checks from D passed DNS resolution, HTTPS 200 to Valve's store, and a 99-byte UDP
A2S_INFO response (type 0x49) from `217.156.22.74:27015`. The
[server operator's page](https://play.free-arena.ro/ro/server/cs16-classic), accessed 2026-11-21,
lists this public endpoint and direct-connect instructions. This proves that endpoint's UDP
query path at test time, **not** client authentication or a playable joined match. A user can try
`connect 217.156.22.74:27015` in the CS console or add it to Favorites if the bundled master list
is empty. No Steam/authentication/anti-cheat files or master-server configuration were patched.
The scheduler code and measured preflight are unchanged; two-client play and loaded timing remain
pending. The latest controls/network work is in commit `8b38550` of draft PR #42.

User follow-up: Internet servers now work, and clicks work. Vertical aiming remains problematic
with both ordinary one-finger movement and two-finger scrolling. Saved config already contains
`+mlook`, `m_pitch 0.022`, no spring/strafe look and standard wheel bindings. Therefore do not claim
the saved mouse-look setting was missing or disable wheel bindings against the user's preference.
A live-console reset (`+mlook; m_pitch 0.022; cl_pitchup 89; cl_pitchdown 89`) was proposed for a
first-person/alive test; its result is pending. Values/behavior are from the
[Valve input implementation](https://github.com/ValveSoftware/halflife/blob/master/cl_dll/input.cpp)
and [mouse implementation](https://github.com/ValveSoftware/halflife/blob/master/cl_dll/inputw32.cpp),
accessed 2026-11-21. The supplied old client binaries do not contain the `m_rawinput` command string;
do not assume modern client raw-input options are available. No persistent input change or active
game restart was performed for this diagnostic step.

The user could not open the console. The existing config binds backtick/tilde, which may be awkward
with the client/browser keyboard layout. Prepared an opt-in `input-check` application variant that
applies the stated mouse-look defaults and adds `F8 toggleconsole`, retaining the old bindings and
standard wheel actions. Its launch plan was validated without dispatch first. The user explicitly
approved restarting **only CS on D**, because doing so disconnects its current match. Scoped graceful
exit/relaunch completed; inspection confirmed the new running `hl.exe` received the preset/F8
arguments (commit `ce0b341`). No node/Minecraft restart or network change. User verification of
vertical aiming and the F8 console is pending; launch arguments alone do not prove either works.

```powershell
./gaming/scripts/Start-Application.ps1 -Application cs16 -Node session-d -Variant input-check -Profiles ./gaming/deploy/runtime-profiles.cs16.json
```

Close the existing game explicitly before using that launcher; do not silently replace an active
match. On a laptop with media keys, the F8 key may require Fn+F8. Two-finger scrolling remains weapon
selection by user preference; one-finger movement is the aiming test.
