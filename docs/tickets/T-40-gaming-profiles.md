# T-40: Reusable gaming profiles and retire webhosting

Owner: Nikita / Codex. Status: in progress. Branch: featur-gaming, PR #22.

User requested reusable application/node/hardware structure and removal of the webhosting feature. Keep the verified Xonotic browser demo working. ADR 0026 records the interface boundary.

Done criteria: validated snake_case profiles, C# Docker-context launch adapter, existing Xonotic command preserved, obsolete webhosting projects removed, meaningful adapter/profile tests and existing gaming/Core tests passing, documentation on main, reviewed code merged with user's explicit authorization.

Not included: automatic reservation-to-launch coordinator, seamless migration, public streaming ingress or real remote hardware verification. Do not claim profile declarations are power measurements.
