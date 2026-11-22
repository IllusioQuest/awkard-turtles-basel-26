# T-88: Public Apertus audience demo

- **Owner:** Codex · **Status:** in progress · **Branch:** `feat/T-78-public-apertus-demo`.
- **Depends on:** current heat-first `WorkloadCoordinator` and local Apertus measurement in T-33.

After the five-minute pitch, let audience members scan a public QR link, submit a preset or short public question, and watch four coloured logical workers on the presentation laptop. Apertus 8B Q4 runs in owned Docker containers. The existing heat-first placement and batch pause path control the jobs. A local operator view shows observed job state separately from illustrative reference-server heat. The remote page is an audience-only form.

Acceptance: one laptop command builds/starts the local service and temporary public tunnel; multiple visitor jobs enter the shared queue, are placed across eligible workers, and produce actual Apertus answers; the operator view identifies one physical laptop and does not claim measured laptop heat. Document Lovable publication, data exposure, setup requirements and limitations.
