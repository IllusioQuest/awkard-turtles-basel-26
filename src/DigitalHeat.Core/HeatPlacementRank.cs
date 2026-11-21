namespace DigitalHeat.Core;

/// <summary>Shared heat-first ordering after eligibility checks; lower ordinal IDs break ties.</summary>
public readonly record struct HeatPlacementRank(double UsefulHeatKwh, string SiteId, string NodeId = "") : IComparable<HeatPlacementRank>
{
    public int CompareTo(HeatPlacementRank other)
    {
        int heat = UsefulHeatKwh.CompareTo(other.UsefulHeatKwh);
        if (heat != 0) return heat;
        int site = string.CompareOrdinal(other.SiteId, SiteId);
        return site != 0 ? site : string.CompareOrdinal(other.NodeId, NodeId);
    }
}
