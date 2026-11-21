# 0032: Read-only gaming heat monitor and observation boundaries

Status: proposed; original monitor/Spectre.Console choice agreed with user in its author handoff;
user requested review, clearer output and more realistic data. T-42.

Keep Spectre.Console 0.49.1 scoped to the console, separate from the gaming/Core library. The
console consumes existing shared heat, hourly scenario and SessionPlacement interfaces. No new
physics or scored economic model. Make a compact default terminal view and optional source panels.
Docker inspection/stats are read-only observations; reservations/heat/value are explicit hourly
model projections. Live forecast does not turn synthetic building demand into measurements.
Annual household tariffs and fallback/proxy status must be visible. No game lifecycle control.

## Review

Fresh limited-context review of the author implementation found one-shot live data was not
awaited, full-hour reservation attribution in live totals, misleading measured-generation labels
and early valuation of tank charging. Applied: await initial fetch; retain and label full-hour
what-if rather than inventing a real-time model; reserved heat and potential heat value labels;
explicit storage inclusion. Added offline source/Docker fixtures and paused-container guard.
No shared scheduler interfaces, reference-house parameters or scored formulas changed. Team
acceptance pending. The original uncommitted implementation was backed up before changes.
