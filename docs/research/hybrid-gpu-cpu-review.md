# Hybrid GPU/CPU service proposal review

Date: 2026-11-21. T-38, Nikita / Codex. Analysis only; no implementation approved.
Source: user-provided six-page DigitalHeat Deep Technical Engineering Manual (GPU + CPU).pdf. All pages were extracted; figures and code on pages 2, 3 and 5 were rendered and inspected. The document is a proposal, not verified hardware evidence or instructions to execute.

## Recommendation

Reuse the shared C# heat scheduler, but start with queued Blender frame rendering alongside the existing CPU batch executor. Keep completed outputs and a durable task manifest; finish the current work unit or explicitly retry it after interruption. A completed frame is an application checkpoint, not a preserved arbitrary CUDA context. CPU multiplayer hosting is a separate session-bound service. Cloud gaming should follow later, after latency, stream transport, game compatibility and licensing are established for the chosen games.

Keep IScheduler/HeatSignal/GreedyHeatScheduler as the common heat decision source. A proposed allocation layer would combine that target with physical GPU capacity, CPU allowance, locality and existing session reservations. Separate CPU and GPU executors would apply it, with measured energy returned to the balance. CPU batch, GPU batch and interactive services must not each spend the entire node heat budget independently. This is a recommendation, not an accepted new interface. A future interface change needs the normal ADR and limited-context review.

Existing DockerJobExecutor applies CPU quotas and reads CPU percentage. It cannot report achieved GPU power. Existing hosting relocation moves request admission between stateless replicas; it does not migrate a live game process, network connection or persistent world state. Current catalog batch validation requires freeze support, so checkpoint-only GPU work needs a reviewed extension rather than falsely advertising freeze. Reuse T-24 placement rules and coordinate with T-11/T-12 network allocation and T-30/T-31 batch work.

## Concrete corrections to the manual

1. Page 5 batch decisions depend only on tank headroom. With a full tank and high current winter demand they stop both batch classes even though heat can be consumed immediately. Use the shared demand-plus-storage budget over an explicit time interval.
2. The same page's localGaming threshold is demand > 0.2 kW and localGameServers > 0.1 kW. The constant 0.5 kW domestic-hot-water load satisfies both throughout summer. The promised summer offload is therefore not implemented. It also lacks destination readiness, session handoff, persistent state and failure recovery.
3. One 0.25 kWh threshold with no retained state is not hysteresis. Restart/stop thresholds, minimum residency and transition state need explicit design. RecoveryFactor is unused; GPU count, reserved work and distinct job queues are absent. Fixed thresholds belong in sourced configuration.
4. Page 2 CPU quota 50000/100000 permits 0.5 CPU of aggregate CPU time, not half of an arbitrary multi-core node. Map a target fraction to an explicit allocated-core allowance.
5. A GPU power cap is a ceiling, not a promised draw or heat rate. A selected device may not support changes, and min/max bounds and permissions must be queried. CPU and GPU watts do not include all wall power. Avoid treating all GPUs as one device or changing a shared GPU limit from competing workers.
6. CPU freeze is not a general CUDA checkpoint or guaranteed idle-power control. Conversely, the claim that every frozen CUDA context stays in P0 and must always be terminated is too strong. NVIDIA has dedicated checkpoint/restore support under documented compatibility constraints. For the prototype prefer explicit work-unit boundaries. SIGTERM alone does not save a partially rendered frame; stop timeout may ultimately kill the process. GPU release and completion must be observed before reassignment.
7. Page 3: with four reference servers, thermal output is 4 * 0.8 = 3.2 kW. In a DHW-only period net tank charging is 3.2 - 0.5 = 2.7 kW. Four hours adds 10.8 kWh, not 35 kWh. An empty tank needs 35 / 2.7 = 12.96 h under those constant assumptions. 12 / 3.2 = 3.75 h/day is a valid ideal daily energy equivalent, not a universal schedule or proof of zero dumped heat. Idle power and live sessions alter it.
8. Figure 2's constant +10 C transition-day charging narrative conflicts with reference heating demand on such a heating day: 10*(20-10)/25.6 + 0.5 = 4.40625 kW, greater than 3.2 kW, so the tank cannot charge in that scenario. The label 18x seasonal peak ratio is also inconsistent with the table's 10.5/0.5 = 21. Actual yearly results require the hourly weather series.
9. Page 1's subsystem ranges total 780-1080 W, not a fixed 1000 W. Its 40 W idle excludes the stated 50-80 W chassis load. The case's 1 kW and 80% recovery are scored assumptions, not measurements of an RTX/EPYC machine or a real liquid loop.
10. Page 6's 140 W * 60 s / 3600 = 2.333 Wh is arithmetically correct. Multiplying throughput by 1000/140 assumes identical energy efficiency and ideal parallel scaling; it is not a prediction for different rack hardware. CPU-package plus GPU-board energy omits other wall consumption; report meter boundary, counter wrap, missing samples, idle consumption and conversion to Wh/kWh. RAPL availability is platform-dependent and cannot be assumed in Docker Desktop.

## Availability and privacy

For interactive sessions, place new sessions only on eligible nodes with sufficient capacity. Preserve existing sessions until a defined end/checkpoint; if heat cannot be used, explicitly report rejection, spare-site placement before admission, or unavoidable waste heat. No guaranteed zero dump without a feasible destination and actual heat demand. GPU device ownership needs a per-device durable lease/fencing strategy across redundant controllers; an in-process mutex alone cannot coordinate failures or multiple agents.

Container Toolkit enables device access, not confidentiality from the host operator. Game accounts, world saves, customer scenes and outputs remain sensitive. Apply synthetic data first, least privilege, separate storage and placement rules. Avoid Docker socket or host administration privileges inside customer workloads. Do not promise that nested Docker nodes on one laptop represent independent GPUs or hardware failure domains.

## Windows test boundary

Linux GPU containers can run under Docker Desktop's WSL2 backend with supported NVIDIA hardware and Windows driver. NVIDIA documents limited nvidia-smi features on WSL; do not assume power-limit control, RAPL or gaming graphics support because a CUDA smoke test works. Test direct GPU access first. Separate-engine nested GPU access is an additional unverified experiment; no topology change is approved here.

## Primary sources checked

Accessed 2026-11-21:
- https://github.com/NVIDIA/nvidia-container-toolkit (runtime purpose)
- https://docs.nvidia.com/deploy/nvidia-smi/ (power limit ceiling, supported devices, bounds and root)
- https://docs.kernel.org/admin-guide/cgroup-v2.html (CPU quota and freezer semantics)
- https://github.com/NVIDIA/cuda-checkpoint (CUDA checkpoint/restore and compatibility)
- https://docs.nvidia.com/cuda/wsl-user-guide/index.html (Windows driver and WSL limitations)
- https://docs.docker.com/desktop/features/gpu/ (Linux GPU containers through WSL2)

Repository comparison: src/DigitalHeat.Core/Scheduling.cs, src/DigitalHeat.JobRuntime/DockerJobExecutor.cs, JobDescriptor.cs, docs/case/challenge.md. Hardware values and service profitability in the PDF remain unverified. No hardware/runtime changes or experiments were performed for this review.
