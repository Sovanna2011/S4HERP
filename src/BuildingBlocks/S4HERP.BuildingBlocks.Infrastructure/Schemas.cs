namespace S4HERP.BuildingBlocks.Infrastructure;

/// <summary>
/// Database schema names. One schema per owning module — a module writes only
/// to its own schema (ADR-02).
/// </summary>
public static class Schemas
{
    public const string Org = "org";
    public const string Cfg = "cfg";
    public const string Mdm = "mdm";
    public const string Fin = "fin";
    public const string Co = "co";
    public const string Wf = "wf";
    public const string Sec = "sec";
    public const string Rpt = "rpt";
    public const string Intg = "intg";
    public const string Audit = "audit";

    public static readonly string[] All =
        [Org, Cfg, Mdm, Fin, Co, Wf, Sec, Rpt, Intg, Audit];
}

/// <summary>Column type conventions that differ from the model-wide default of decimal(19,4).</summary>
public static class ColumnTypes
{
    /// <summary>Quantities, exchange rates, ratios and percentages.</summary>
    public const string Rate = "decimal(23,6)";

    /// <summary>Financial amounts.</summary>
    public const string Amount = "decimal(19,4)";
}
