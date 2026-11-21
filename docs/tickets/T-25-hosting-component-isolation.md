# T-25: Separate hosting frontend and API across node pools

- **Tier:** 2 Pitch · **Area:** Webhosting privacy prototype
- **Depends on:** existing hosting gateway and nested-engine lab
- **Owner:** Nikita / Codex · **Status:** in progress (architecture review pending)

User approved component isolation first, with node-operator access limits documented.
Build a separate opt-in four-engine Linux lab: two frontend replicas and two API replicas.
Preserve the gateway URL and active/standby design. Restrict application connectivity,
use synthetic content, and verify that frontend/API routes and images are separated.
Test a failed node in each pool; do not claim zero downtime or host-operator confidentiality.
Keep the existing lab usable. T-24 owns data-locality policy; no parallel policy implementation.
Completion requires reproducible tests, code PR, and docs with topology and limitations.
