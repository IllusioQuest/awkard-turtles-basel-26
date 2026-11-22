# T-79 — Measure the warm-process render penalty on the presentation GPU

Owner: Nikita / Claude. Status: in progress. Branch: claude/beautiful-allen-ukjq15.
Follows T-77 (renderer runtime). Diagnostic only: no change to the renderer, scheduler,
five roles, GPU budgets, concurrency (3) or quality (640x360, 32 samples).

Headless runs of the same scene with Blender 5.3.0 on software OpenGL (llvmpipe, no GPU)
showed that a warm Blender process renders its first frame in about 6.2 s and every later
frame in about 19.5 s, with identical pixels. The extra time is per-sample shadow work, and it
goes away with shadows off. T-77 evidence on the Iris Xe neither confirms nor rules this out.

Task: run `rendering/bench_frame.py` on the presentation laptop (lab stopped) and record
whether warm later frames are slower than warm first / fresh-process frames. Only if they are,
propose (ADR, user decision) recycling warm workers every K frames or a fresh process per frame.

Done when the laptop result is in `docs/performance/` and summarised in
`docs/jobs/render-warm-penalty.md`, with a recommendation.
