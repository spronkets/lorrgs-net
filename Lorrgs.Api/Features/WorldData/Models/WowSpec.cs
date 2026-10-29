namespace Lorrgs.Api.Features.WorldData.Models;

/// <summary>
/// WoW Spec (Frost Mage, Holy Priest, etc.)
/// </summary>
public class WowSpec
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameSlug { get; set; } = "";
    public string FullName { get; set; } = ""; // "Frost Mage"
    public string FullNameSlug { get; set; } = ""; // "frost-mage"

    public int ClassId { get; set; }
    public WowClass? WowClass { get; set; }

    public int RoleId { get; set; }
    public WowRole? Role { get; set; }
}
