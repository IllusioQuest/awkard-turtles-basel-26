# T-76 — Distinguish GPU workers visually and by work budget

Owner: Nikita/Codex. Status: in progress. Branch: fix/T-76-gpu-worker-shades.
Depends on PR #67 compact renderer map.

User approved related colour shades plus larger configurable work budgets
1.5 / 1.0 / 0.5. Retain total heat budget, heat-first mesh and all runtime roles.
Use stable GPU 1/2/3 identity labels and accent-derived shades in graphic/cards;
keep activity-state indicators separate. Budget changes influence admission,
not GPU percentage caps or guaranteed unequal frame counts.

Existing CPU weights remain 1/1. Total weight remains 5; synthetic 500 kWh
quota partitions as CPU100/100 and GPU150/100/50. Example GPU1:
500 kWh * 1.5 / 5 = 150 kWh. These are lab assumptions, not measured heat.
Preserve prior output and restart renderer lab only, then validate a full run.
