namespace DigitalHeat.Fleet;

public sealed record FleetClient(string ClientId, double LatDeg, double LonDeg, string? Canton, double Weight);

/// <summary>Reads config/fleet/clients/*.csv: client_id, lat_deg, lon_deg, canton, weight (§6). '#' lines are comments.</summary>
public sealed class ClientsFile
{
    private readonly double[] _cumulative;
    public IReadOnlyList<FleetClient> Clients { get; }

    public ClientsFile(IReadOnlyList<FleetClient> clients)
    {
        if (clients.Count == 0 || clients.Any(c => c.Weight < 0) || clients.Sum(c => c.Weight) <= 0)
            throw new InvalidDataException("Clients need at least one entry with positive total weight.");
        Clients = clients;
        double total = clients.Sum(c => c.Weight), acc = 0;
        _cumulative = clients.Select(c => acc += c.Weight / total).ToArray();
    }

    public static ClientsFile Read(string path)
    {
        var t = CsvTable.Read(path);
        bool hasCanton = t.Has("canton");
        return new ClientsFile(t.Rows.Select(r => new FleetClient(t.Str(r, "client_id"), t.Num(r, "lat_deg"), t.Num(r, "lon_deg"),
            hasCanton && t.Str(r, "canton").Length > 0 ? t.Str(r, "canton") : null, t.Num(r, "weight"))).ToList());
    }

    /// <summary>Weighted draw; uses one uniform.</summary>
    public FleetClient Draw(Rng rng)
    {
        double u = rng.NextDouble();
        int i = Array.FindIndex(_cumulative, c => u < c);
        return Clients[i < 0 ? Clients.Count - 1 : i];
    }
}
