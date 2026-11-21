# 0026: Profile-driven gaming runtime and retirement of webhosting

Status: proposed; user authorized reusable gaming structure, removal of unused webhosting, and integration into main. T-40.

Use the existing C# gaming library, without new packages, for snake_case application, node and hardware profiles. An application defines executable, argv, working directory, Unix user, environment and required capability names. A node defines its Docker context, Compose project/service, browser URL and hardware profile. Hardware profiles describe declared capabilities, not measured power or independently verified capacity. Compose remains responsible for provisioning and resource/device limits.

A Docker CLI adapter resolves exactly one running container by project/service labels in the selected context. It validates profile compatibility, passes arguments through ProcessStartInfo.ArgumentList, and uses a fixed Linux wrapper with shell-quoted arguments and flock to avoid duplicate launches. A container-wide lock protects the desktop from two simultaneous applications; existing Xonotic lock is retained for compatibility. Launch is dispatch only, not proof of healthy gameplay. No stop, migrate, pause or automatic reservation release operation is added.

Keep Start-Xonotic.ps1 as a compatibility wrapper around a generic profile launcher. Existing display/rendering checks remain available. The running game must remain untouched while testing. Remote engines are selected through preconfigured Docker contexts; no exposed Docker TCP endpoint, socket mount, password or SSH key in profiles. Browser URLs are configuration, not a failover/routing promise.

Heat reservations remain in SessionPlacement using the shared Core model. This change does not connect launches to reservations or implement distributed orchestration. That bridge needs trusted inventory, launch confirmation, failure reconciliation and durable ownership before claiming automatic multi-server distribution.

Delete the tracked webhosting tree and retire its active ticket references. Preserve shared Core, JobRuntime, network simulation, hardware research contracts and historical decision/measurement records. Do not delete other worktrees, Docker volumes, caches or running sessions. The gaming prototype is an additional experiment and changes no scored figures.

## Review

Fresh-context verdict: go with changes. Applied: every application acquires desktop-wide and legacy Xonotic locks in that order, protecting the already-running old launcher. Profiles are explicitly trusted operator configuration, with strict snake_case fields, validated environment names, absolute executable paths and required references. Docker argv uses ArgumentList; the fixed inner shell quotes each profile value. Disposable offline Linux-container smoke verifies literal shell metacharacters and existing legacy-lock exclusion. All tracked feature references checked before retirement; shared research contracts remain. User approval covers the general direction and merge; team acceptance remains pending.
