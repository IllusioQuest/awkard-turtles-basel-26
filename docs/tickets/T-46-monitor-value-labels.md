# T-46: Transfer monitoring data into the admin web dashboard

Owner: Nikita / Codex. Status: implementation checked; preparing code PR.
Branch: feat/T-46-monitor-value-labels. Depends on T-42 (already integrated).

## Scope

User superseded the console work: integrate data directly into existing /admin, retire the terminal
entry point/dependency, and keep existing monitoring logic in its original folder. Transfer strict
snake_case inputs and snapshots through a thin read-only local API, without new heat/scheduling
formulas. Separate observations, assumptions and model values; explain units and sources. No game
control, no Minecraft readiness inferred from the configured Xonotic desktop monitor. ADR 0036.

## Coordination

Checked main, shared status, open PRs and T-45 on 2026-11-21. Jona / Codex's T-45 branch changes
the Network protocol model and paper, not the monitor. T-14 economics and T-18 homeowner
dashboard remain separate work. Their progress is reported from branch/status evidence, not
assumed live agent activity. No external messages sent. Existing admin logic is preserved with
only a monitoring component import/render; household and other agent-owned model files untouched.

## Validation

Clean API build; 42 gaming/monitor tests, frontend build/lint, API Host/Origin/input/content-type
smoke and read-only Docker snapshot, browser controls/July values/value-guide checks. No presentation
unit tests. See [web monitoring guide](../monitoring-web.md) for commands, field meanings, source
and failure boundaries. Documentation on main; code feature PR pending. No live game changes.
