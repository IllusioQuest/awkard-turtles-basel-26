namespace DigitalHeat.Core;

/// <summary>Shared EDF ordering. Deadlines use the adapter's time unit; IDs are stable ordinal strings.</summary>
public readonly record struct JobPriority(long Deadline, string Id) : IComparable<JobPriority>
{
    public int CompareTo(JobPriority other)
    {
        int due = Deadline.CompareTo(other.Deadline);
        return due != 0 ? due : StringComparer.Ordinal.Compare(Id, other.Id);
    }
}
