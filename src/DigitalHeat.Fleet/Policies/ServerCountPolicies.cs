namespace DigitalHeat.Fleet;

/// <summary>Same server count in every house (docs/model/fleet-simulation.md §4.4, "fixed: n").</summary>
public sealed class FixedServerCount(int n) : IServerCountPolicy
{
    public int ServerCount(HouseSpec house, HouseDemandSummary demand) => n;
}

/// <summary>
/// Servers for a share f of the peak heat hour (§4.4):
/// ceil(f × peak_heat_kw ÷ (max_electric_kw × heat_recovery_share)). With the case values
/// (10 kW peak, 1 kW servers, 0.8 recovery) f = 1 gives 12.5, so 13 servers.
/// </summary>
public sealed class ShareOfPeakServerCount(double f, ServerParams p) : IServerCountPolicy
{
    public int ServerCount(HouseSpec house, HouseDemandSummary demand)
    {
        double x = f * demand.PeakHeatKw / (p.MaxElectricKw * p.HeatRecoveryShare);
        // The tolerance guards float noise (e.g. 3.0000000000000004 must not become 4).
        return Math.Max(0, (int)Math.Ceiling(x - 1e-9));
    }
}

/// <summary>The house file's server_count column (§4.1, "from_file").</summary>
public sealed class FromFileServerCount : IServerCountPolicy
{
    public int ServerCount(HouseSpec house, HouseDemandSummary demand) =>
        house.ServerCount ?? throw new InvalidOperationException(
            $"House {house.HouseId} has no server_count, but servers.policy = from_file.");
}

public static class ServerCountPolicies
{
    /// <summary>Builds the policy named in the scenario: "fixed" | "share_of_peak" | "from_file".</summary>
    public static IServerCountPolicy FromConfig(ServersSection cfg, ServerParams p) => cfg.Policy switch
    {
        "fixed" => new FixedServerCount(cfg.N ?? throw new InvalidDataException("servers.n is required for policy 'fixed'.")),
        "share_of_peak" => new ShareOfPeakServerCount(
            cfg.F ?? throw new InvalidDataException("servers.f is required for policy 'share_of_peak'."), p),
        "from_file" => new FromFileServerCount(),
        _ => throw new InvalidDataException($"Unknown servers.policy '{cfg.Policy}'."),
    };
}
