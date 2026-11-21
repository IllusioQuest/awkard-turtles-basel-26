# T-75 — Compact renderer cards and identify job provider

Owner: Nikita/Codex. Status: in review. Branch: fix/T-75-compact-render-map.

User requested removal of bottom Physical hosts text, a graphic label identifying
the initial job provider, and smaller worker cards fitting a single desktop row.
Removed the footer field and its DOM assignment, labelled the actual origin peer
from the existing network snapshot, and reduced card minimum width/padding/gaps.
Narrow screens still wrap. Other text and all scheduler/runtime settings retained.

Live at http://localhost:18950/ui after refresh. Verified served HTML, JavaScript
syntax (`node --check`) and diff whitespace. Visual browser preview unavailable;
layout requires user confirmation. No rendering restart or additional workload.
