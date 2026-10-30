namespace Lorrgs.WarcraftLogs.Models;

public sealed record TimelineCastResult(int TargetId, string TargetName, string Kind, double Amount,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? Outcome = null);
