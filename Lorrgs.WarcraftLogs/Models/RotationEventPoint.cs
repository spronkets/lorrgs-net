namespace Lorrgs.WarcraftLogs.Models;

public class RotationEventPoint
{
    public long Timestamp { get; set; }
    public required string Ability { get; set; }
    public int? SpellId { get; set; }
    public string? EventType { get; set; }
}
