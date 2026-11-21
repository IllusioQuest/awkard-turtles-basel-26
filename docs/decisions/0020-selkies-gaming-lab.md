# 0020: Selkies browser gaming compatibility lab and node placement

Status: proposed; user approved Selkies and multi-server distribution, 2026-11-21.
Scope T-39. Browser-only clients; original games unchanged; Linux containers. First prove desktop video/input, then inspect real Intel graphics capabilities, then CS2 if feasible. No CS2 download or performance claim yet.

Use official Selkies desktop image pinned by digest after pull. Start one software-rendered baseline on loopback, with a generated password, no Docker socket, no privileged mode. Use HTTP only on loopback to avoid certificate bypass; public deployments require authenticated TLS. Current Selkies defaults to WebSockets, not WebRTC; use default transport first and defer Internet traversal. Limit clipboard/upload/download/mic/webcam where supported. Customer desktop is not a hostile multi-tenant sandbox.

Then two session nodes with independent containers, ports, homes and credentials (same Docker engine simulates nodes; no independent hardware claim). One gaming session owns a single node slot, not one game split across GPUs/servers. Existing sessions remain pinned. New session placement reuses shared IScheduler/GreedyHeatScheduler and T-24 locality; node readiness/capacity gates admission. Synthetic heat inputs explicitly labelled. No duplicate thermal model or scored changes. Batch work pauses before reserving resources; do not claim physical power reduction from CPU quota.

C# controller is a single local lab coordinator, not HA. Reservations must be atomic and idempotent; readiness before admission; fail closed on unavailable or invalid nodes. Session failure releases reservation only after observed termination; client reconnect means a new session, not recovered game progress. Controller restart must reconcile actual node state or conservatively reject admission. Browser gets assigned node URL; public multi-user authentication/session authorization is deferred, and bind remains loopback.

Real separate-engine deployment needs authenticated per-node agent/API and coordinated GPU ownership, not exposed Docker TCP sockets. This phase supplies replaceable node inventory/adapter boundary and tests distribution, capacity, active-session pinning, heat changes and failure. No automatic game-frame splitting; CPU batch and separate sessions distribute independently. Streaming and game stay together for latency and device ownership.

Intel WSL access via /dev/dxg and /usr/lib/wsl/lib is experimental; /dev/dri alone need not work. Probe actual OpenGL/Vulkan/VAAPI and report software fallback. Never change Windows driver/WSL install silently. CS2 graphics and licensing feasibility remain separate gates.

Review: pending fresh-context review. References: https://docs.selkies.io/latest/start/ and /settings/, shared Scheduling.cs and JobPlacement.cs. Reproduction/docs on main; code on featur-gaming.
