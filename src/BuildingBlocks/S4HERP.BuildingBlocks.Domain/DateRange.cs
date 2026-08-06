namespace S4HERP.BuildingBlocks.Domain;

/// <summary>
/// Half-open validity interval [<see cref="From"/>, <see cref="To"/>).
/// Open-ended validity uses <see cref="OpenEnded"/> instead of null so that
/// overlap and containment predicates translate to plain SQL comparisons.
/// </summary>
public readonly record struct DateRange(DateOnly From, DateOnly To)
{
    /// <summary>The sentinel used for "no end date".</summary>
    public static readonly DateOnly OpenEnded = new(9999, 12, 31);

    public static DateRange StartingAt(DateOnly from) => new(from, OpenEnded);

    public bool Contains(DateOnly date) => date >= From && date < To;

    public bool Overlaps(DateRange other) => From < other.To && other.From < To;

    public bool IsValid => From < To;
}
