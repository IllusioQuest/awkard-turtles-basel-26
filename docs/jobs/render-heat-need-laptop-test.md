# Per-run heat need: presentation laptop verification

Tested main cdd7ac4 on2026-11-22 following the user-supplied restart instructions.
Stopped the previous lab with its scoped Stop script and preserved all files.
Published a fresh main build with Start-PresentationRenderLab.ps1, port18950,
48frames, prefixdemo-, three native GPU workers sharing Intel Iris Xe.
No source code, policy, concurrency, budgets or quality was changed.

The new backend exposes heat_need_frames24/16/8 and displayed-need values
19.2/12.8/6.4kWh (frames multiplied by abstract0.8kWh per job-hour).
This is synthetic per-run need, not measured recovered heat. Browser visual
inspection was blocked by Computer Use URL confidence; API values were verified.

Run1 passed:48frames, GPU1/2/3 counts24/16/8, active workflow153.7926seconds,
CPU video assembly48frames, downloadable MP4 verified (119440bytes).
When GPU3 reached8, total progress was23/48 and GPU1/GPU2 were still active:
the whole run did not stop when GPU3 completed its share.

Run2 failed near startup at12.9302 active seconds with 'An owned job failed or
expired; inspect retained logs'. GPU2 frame002 native record is released with
exit_code1 and no completion marker. Other early frame attempts succeeded.
No evidence establishes the process-exit cause; do not label it a heat stall,
driver crash, or successful repeated-run test. Existing warm-process log files
can be overwritten by the next process generation, limiting diagnosis.

The app was left on the failed second run, held by its normal failure path;
all outputs, the first complete video and failed attempt journals remain.
Evidence: docs/performance/render-heat-need-laptop-2026-11-22.json.
