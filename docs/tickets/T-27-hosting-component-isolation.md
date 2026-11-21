> RETIRED by user request in T-40. Webhosting is withdrawn; do not resume this feature. Preserve this ticket as historical context.

# T-27: Separate hosting frontend and API across node pools

- **Tier:** 2 Pitch · **Area:** Webhosting privacy prototype
- **Depends on:** existing hosting gateway and nested-engine lab
- **Owner:** Nikita / Codex · **Status:** retired by user request (T-40)

User approved component isolation first, with node-operator access limits documented.
Build a separate opt-in four-engine Linux lab: two frontend replicas and two API replicas.
Preserve the gateway URL and active/standby design. Restrict application connectivity,
use synthetic content, and verify that frontend/API routes and images are separated.
Test a failed node in each pool; do not claim zero downtime or host-operator confidentiality.
Keep the existing lab usable. T-24 owns data-locality policy; no parallel policy implementation.
Completion requires reproducible tests, code PR, and docs with topology and limitations.


Progress: separate frontend/API Linux images build. Internal bridge experiment blocked required
published-port ingress, so firewall revision was then approved and passed full rollout tests.
See docs/webhosting-privacy.md. No privacy/failover completion is claimed.



PR #12: https://github.com/IllusioQuest/awkard-turtles-basel-26/pull/12
All four final isolation checks pass; 17 hosting tests; both Linux images build.
Four node kills and active-gateway pause recovered at the same URL, with interruptions measured
in docs/webhosting-isolation-runtime.md. Keep open until the code PR is merged.
