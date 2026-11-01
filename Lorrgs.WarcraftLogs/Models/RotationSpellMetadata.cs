namespace Lorrgs.WarcraftLogs.Models;

public sealed class RotationSpellMetadata
{
    public int SpellId { get; init; }
    public required string Name { get; init; }
    public string? Icon { get; init; }
}
