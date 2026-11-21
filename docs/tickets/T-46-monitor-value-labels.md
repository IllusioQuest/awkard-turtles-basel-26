# T-46: Explain console monitoring values

Owner: Nikita / Codex. Status: in progress.
Branch: feat/T-46-monitor-value-labels. Depends on T-42 (already integrated).

## Scope

Clarify the existing console's labels, units and explanations without changing scheduling,
observations, heat calculations, interfaces or dependencies. Separate Docker observations from
in-memory reservations and hourly model predictions. Identify configured application scope so
Minecraft readiness is not inferred from a Xonotic monitor. Explain all values in the details view.

## Coordination

Checked main, shared status, open PRs and T-45 on 2026-11-21. Jona / Codex's T-45 branch changes
the Network protocol model and paper, not this renderer. T-14 economics and T-18 homeowner
dashboard remain separate work. Their progress is reported from branch/status evidence, not
assumed live agent activity. No messages sent and no other agent's files modified.

## Validation

Build and existing gaming tests; inspect rendered January/July outputs at narrow/wide terminal
widths, with and without details. No tests for presentation. Document command and reading guide
on main. No live game/container changes.
