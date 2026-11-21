> Completed prototype: user-verified Xonotic browser gameplay and Intel graphics experiment merged in PR #22 (56501a7). CS2 remains deferred, not verified. Reusable runtime follow-up completed in T-40; current guide: ../gaming-profiles.md. Historical milestones below retain their original pending states.

# T-39: Intel browser gaming compatibility experiment

Owner: Nikita / Codex. Branch: featur-gaming. Status: in progress, streaming stack choice pending.
User approved browser clients, no game code changes, Linux workloads and an Intel compatibility experiment. Active sessions retain resources, new sessions follow heat placement, batch pauses first; failure permits reconnect.

Discovery on 2026-11-21: Intel Iris Xe, Docker Desktop Linux/WSL2. docker-desktop exposes /dev/dxg, /dev/dri and /usr/lib/wsl/lib; /mnt/wslg absent. A normal isolated alpine:3.22 container exposes none of those graphics paths. No accelerated rendering, encoding, Vulkan or CS2 execution proven. Steam installed; C: free space about 35.3 GB decimal at observation. Do not download CS2 before confirming storage and installation location.

Repeat Windows discovery from the gaming checkout:

    powershell -ExecutionPolicy Bypass -File gaming/scripts/Test-GamingCompatibility.ps1

Report defaults to the system temp directory. Script checks host and WSL discovery; the separate ordinary-container check used existing alpine:3.22 with no network, read-only root and dropped capabilities. Device presence is not a performance result.

Proposed next step, awaiting user choice: evaluate Selkies for browser streaming with a synthetic interactive desktop first, then explicit device/library access and real rendering/encoding probes, then original CS2 only if feasible. C# remains the orchestration language; no new heat scheduler. A streaming dependency/interface decision requires ADR and fresh limited-context review before adoption.

Sources checked 2026-11-21:
- https://github.com/selkies-project/selkies
- https://devblogs.microsoft.com/commandline/d3d12-gpu-video-acceleration-in-the-windows-subsystem-for-linux-now-available/
- https://docs.docker.com/desktop/features/gpu/

Code discovery script committed/pushed ea9c491. No drivers installed, no host configuration changed, no game downloaded. Proposed stack is not yet accepted. Existing unrelated Properties directories in the original checkout were preserved; implementation uses a separate Git worktree.

User approved Selkies and emphasized multi-server distribution. ADR 0021 reviewed: go with changes. Two session containers are the initial lab, with separate ports/homes/passwords and loopback access. New sessions/batch work distribute; one original CS2 process is not split across hosts. Streaming image downloading; Intel render node reports WSL faux_driver, hardware graphics remain unproven.

Milestone: two Selkies containers running, auth/volume/bind checks pass. Intel OpenGL accelerated after adding read-only host drivers mount; Vulkan CPU-only and VAAPI failed. CPU H.264 encode succeeded. Browser video/input awaits user; no CS2 claim. Reproduction and next decisions: docs/gaming-lab.md. Node-stop test blocked by execution policy before execution. Code f8b318c pushed on featur-gaming.

Compatibility lab review: PR #22, https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/22. Not complete: browser smoke confirmation, accelerated Vulkan/CS2 and multi-node coordinator are pending. ADR renamed to 0021 to avoid collision with the team's business-model ADR 0020. Gaming remains a user-requested experiment, separate from the team business model.

User confirmed actual streamed desktop and keyboard/input via screenshot. Applied live wallpaper stretch on A; visual follow-up pending. Summer placement explicitly prefers eligible hotel/spa heat sites independent of player location. All-sites-unavailable fallback still needs a choice. Non-disruptive repaint helper pushed d37beb2; see gaming-lab.md.

Read linked live case. User requests largest available heat reserve for execution-site selection. Proposed definition and atomic per-site reservation accounting recorded in gaming-lab.md; await confirmation of headroom versus stored heat before changing interfaces. No numeric case parameters changed.

User confirmed remaining useful heat capacity definition. Implemented shared helper and gaming-only atomic reservation model/demo in 540bbd7 (PR #22 updated). 14 gaming tests and 40 main-based Core tests pass, including legacy-output cross-check. This is reservation accounting only: actual batch preemption, launch/reconciliation and stream routing remain pending. ADR 0022 reviewed go with changes.

Fixed user-reported Windows PowerShell 5.1 NativeCommandError in graphics probe, b5bb9a3, PR #22. Tested in actual PS5.1 and PS7 with absent device paths and expected VAAPI failure; both complete. Hardware/Vulkan support unchanged.

User approved isolated Dozen build experiment after seeing CPU-only Vulkan. ADR 0023 reviewed
go with changes. Build uses official Mesa 26.0.8 source pinned by locally computed SHA256,
separate /opt prefix and pinned Selkies base; no Windows changes or desktop replacement.
C: free space is about 21 GB versus official CS2 requirement of 85 GB, so driver experiment
can proceed but a fresh CS2 install needs another storage decision. Existing Steam manifest
is not evidence of a usable Linux installation and will not be modified automatically.

Dozen experiment now passes Intel GPU enumeration and vkcube render/presentation, including desktop C. Sources, commands and limits in gaming-lab.md; ADR 0023 reviewed. User browser-C visual check requested. No game downloaded. Current C limits (4 GiB, 2 CPU equivalents) are desktop-smoke limits; a CS2 profile must be separately sized against the 8 GB RAM minimum and available host resources before launch.

Dozen experiment code pushed 2e67a0c in PR #22. Final PS5.1 build, enumeration and render pass; login diagnostic helper verifies 200 with local C credentials and 401 without/wrong credentials. User browser login unresolved, asked Incognito + correct C password field. User requested smaller game; recommended Xonotic and asked selection versus SuperTuxKart; no game install yet.

User selected Xonotic instead of CS2 and confirmed C login works. Preparing checksum-verified official original game image; no client install or code modifications. Uniform wallpaper mode applied to C, image sizing issue no longer addressed by stretch. Manual presentation launch first; automatic shared-scheduler runtime integration remains pending.

Xonotic is running on C: unmodified official release checksum verified, non-root desktop, Intel OpenGL, assets and stereo audio startup checks pass under Windows PowerShell 5.1. User browser gameplay confirmation requested; gamepad warning remains. Fixed probe quoting to reject empty-output render false positives. Multi-site automatic launch integration remains pending; game is currently manually launched.

User confirmed Xonotic works on their machine and requested a technical explanation plus cleanup BEFORE live distribution integration. Scope: behavior-preserving gaming C#/PowerShell refactors and a consolidated architecture guide. No container restart, active-session interruption, model change or new stack choice planned. Existing .vs user files retained.

Cleanup complete: existing data records moved to GamingModels.cs without contract changes; SessionPlacement validation/candidate selection named explicitly; common label lookup and PS5.1 HTTP helpers replace duplication. Added gaming/DigitalHeat.Gaming.sln, clean build with zero warnings/errors. 14 gaming + 40 Core tests pass, assignment demo unchanged, A/B/C auth and live Xonotic startup checks pass. No process/container restart. Current guide: docs/gaming-architecture.md. Live distribution integration still pending; ticket remains in progress.
