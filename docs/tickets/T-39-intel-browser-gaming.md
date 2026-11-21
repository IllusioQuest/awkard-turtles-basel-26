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
