# T-38: Review the hybrid GPU/CPU service proposal

- Owner: Nikita / Codex
- Status: done (analysis only; implementation awaits user choice)
- Scope: Read the supplied six-page engineering manual, compare with existing scheduler and official runtime documentation, identify correctness issues and recommend a first prototype.
- Result: docs/research/hybrid-gpu-cpu-review.md. Recommend checkpointed GPU frame jobs first; separate session-bound gaming. No code, dependencies, hardware configuration or accepted interface changes.
- Follow-up: user selects first service; proposed implementation then gets its own agreed scope and ADR review as needed.
