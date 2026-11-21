# T-73 — Restart and five-worker render presentation

Owner: Nikita/Codex. Status: in progress. Branch: feat/T-73-five-worker-render.
Depends on PR #64 journal fix. User confirmed three concurrent local GPU workers
sharing Iris Xe and two CPU workers: scene preparation and final video assembly.
GPU work weights 1.1/1/.9; increase CPU allowance within laptop budget. Add Restart
that preserves output and reconciles current jobs before a fresh run.

ADR 0065 reviewed go with changes. Native role restrictions, opt-in shared-GPU
alias group, matching physical identity/concurrency, ordered hash-validated
assembly manifest and assembly-aware restart are being implemented. One physical
GPU, not three hardware devices. One synthetic 500kWh total is partitioned once
as 100/100/110/100/90kWh logical quotas, not measured recovered heat. Actual GPU
percentages are neither set nor fabricated. Verify all roles contribute, complete
MP4, restart/pause, sustained journal behavior before replacing presentation app.
