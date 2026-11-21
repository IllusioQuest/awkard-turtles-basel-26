namespace DigitalHeat.Core;

/// <summary>Heat-first site order with load balancing (decision 0053): the site with the larger free
/// share of its hourly heat budget ranks higher, then more free heat in kWh, then lower ordinal IDs.
/// Work therefore fills sites to an equal share of their heat need instead of all going to the
/// largest site. Only sites with free heat may be candidates; the share is never a reason to exceed it.</summary>
public readonly record struct HeatShareRank(double FreeShare, double FreeHeatKwh, string SiteId, string NodeId = "")
    : IComparable<HeatShareRank>
{
    /// <summary>Rank from the hour's budget (<see cref="HeatCapacity.RemainingKwh"/> at hour start) and
    /// the heat already committed this hour. A site without budget gets share 0.</summary>
    public static HeatShareRank From(double budgetKwh, double committedKwh, string siteId, string nodeId = "")
    {
        if (!double.IsFinite(budgetKwh) || !double.IsFinite(committedKwh) || budgetKwh < 0 || committedKwh < 0)
            throw new ArgumentOutOfRangeException(nameof(budgetKwh));
        double free = Math.Max(0, budgetKwh - committedKwh);
        return new(budgetKwh > 0 ? free / budgetKwh : 0, free, siteId, nodeId);
    }

    public int CompareTo(HeatShareRank other)
    {
        int share = FreeShare.CompareTo(other.FreeShare);
        if (share != 0) return share;
        int heat = FreeHeatKwh.CompareTo(other.FreeHeatKwh);
        if (heat != 0) return heat;
        int site = string.CompareOrdinal(other.SiteId, SiteId);
        return site != 0 ? site : string.CompareOrdinal(other.NodeId, NodeId);
    }
}
