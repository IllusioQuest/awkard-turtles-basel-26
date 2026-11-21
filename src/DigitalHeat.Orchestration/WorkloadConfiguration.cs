using System.Text.Json;

namespace DigitalHeat.Orchestration;

public sealed record WorkloadDefinition(WorkloadRequest Request, ContainerProfile Profile);
public sealed record WorkloadConfiguration(string FleetId, WorkloadInventory Inventory, RuntimeNode[] RuntimeNodes, WorkloadDefinition[] Workloads)
{
    public static WorkloadConfiguration Load(string path)
    {
        var config = JsonSerializer.Deserialize<WorkloadConfiguration>(File.ReadAllText(path), WorkloadJson.Options)
            ?? throw new InvalidDataException("Empty workload configuration.");
        if (string.IsNullOrWhiteSpace(config.FleetId) || config.Inventory is null || config.RuntimeNodes is null || config.Workloads is null
            || config.RuntimeNodes.Any(n => n is null) || config.Workloads.Any(w => w is null || w.Request is null || w.Profile is null)
            || config.RuntimeNodes.Select(n => n.NodeId).Distinct().Count() != config.RuntimeNodes.Length
            || config.Workloads.Select(w => w.Request.Id).Distinct().Count() != config.Workloads.Length
            || config.Inventory.Nodes.Any(n => config.RuntimeNodes.All(r => r.NodeId != n.Id))) throw new InvalidDataException("Invalid fleet/configuration identity.");
        foreach (var request in config.Workloads.Select(w => w.Request)) WorkloadPlacement.ValidateRequest(request);
        foreach (var group in config.RuntimeNodes.GroupBy(n => n.DockerContext))
            if (config.Inventory.Nodes.Where(n => group.Any(r => r.NodeId == n.Id)).Select(n => n.DomainId).Distinct().Count() > 1)
                throw new InvalidDataException("One Docker context cannot pretend to be multiple independent physical resource domains.");
        return config;
    }
}
