namespace Webhosting.Contracts;

public sealed record DeploymentSpec(string DeploymentId, IReadOnlyList<ComponentSpec> Components);

public sealed record ComponentSpec(
    string ComponentId, string Image, int ContainerPort, string HealthPath, int Replicas,
    double CpuLimitCores, long MemoryLimitMiB, string RoutePrefix);

public sealed record ReplicaStatus(
    string ReplicaId, string DeploymentId, string ComponentId, string NodeId,
    string State, string? Endpoint, long ObservedGeneration);

public sealed record DeploymentStatus(
    string DeploymentId, long Generation, int DesiredReplicas, int ReadyReplicas,
    IReadOnlyList<ReplicaStatus> Replicas, IReadOnlyList<string> Conditions);

public sealed record NodeHeartbeat(
    string NodeId, DateTimeOffset ObservedAtUtc, double CpuCapacityCores,
    long MemoryCapacityMiB, bool AcceptingWork, IReadOnlyList<ReplicaStatus> Replicas);

public sealed record DrainNodeRequest(string Reason, double TimeoutSeconds);

public sealed record ApiError(string Code, string Message, IReadOnlyList<string>? Details = null);

public sealed record ExecutionCommand(
    string CommandId, long LeadershipEpoch, long DeploymentGeneration, string NodeId,
    string ReplicaId, string Action, ComponentSpec? Spec);
