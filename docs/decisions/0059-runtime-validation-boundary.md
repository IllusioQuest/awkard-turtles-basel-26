# 0059 - Adapter-specific runtime validation

Status: proposed. T-66 follow-up to user-approved native runtime / ADR 0058.

The generic WorkloadCoordinator currently calls DockerWorkloadRuntime.Validate
both when loading journals and before recording a dispatch. Native GPU bindings
have a logical device identity but no Linux device path/NVIDIA Docker ID, so
this rejects them even when an actual native GPU render passed.

Extract shared request/profile/logical binding checks into RuntimeAttemptValidation.
Add IWorkloadRuntime.ValidateAttempt with a default that delegates to the unchanged
strict Docker validator. Coordinator calls this hook. Docker keeps all device,
port and volume syntax restrictions; existing implementations use the default.
Native overrides the hook with common validation, then its Preflight checks the
trusted executable/profile/artifact/capability requirements. Do not fake a Docker
GPU mapping or weaken Docker checks. No scheduler rank, heat or ownership changes.

This retains old DTO names (ContainerProfile, RuntimeNode.DockerContext) for now;
renaming them across all modules would be unrelated churn. They are adapter hints,
not platform branches in placement. Tests cover native logical GPU acceptance,
Docker mapping rejection, identity mismatch and ambiguous native launch fencing.

## Review
Fresh limited-context verdict: go with changes. Default Docker validation remains strict. Applied the requested native AllowStop guard before process lookup/mutation and added a protected-attempt regression test. No policy change.
