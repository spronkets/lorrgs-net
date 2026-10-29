using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.Rankings.GetSpecRanking;

/// <summary>Parameters for GetSpecRanking.</summary>
public sealed record GetSpecRankingRequest(
    [property: FromRoute(Name = "specSlug")] string SpecSlug,
    [property: FromRoute(Name = "bossSlug")] string BossSlug,
    [property: FromQuery(Name = "edition")] string? Edition = null,
    [property: FromQuery(Name = "refresh")]
    [property: DefaultValue(false)] bool Refresh = false,
    [property: FromQuery(Name = "difficulty")]
    [property: DefaultValue("mythic")] string Difficulty = "mythic",
    [property: FromQuery(Name = "metric")]
    [property: DefaultValue("")] string Metric = "",
    [property: FromQuery(Name = "zoneId")] int? ZoneId = null,
    [property: FromQuery(Name = "encounterId")] int? EncounterId = null,
    [property: FromQuery(Name = "className")] string? ClassName = null,
    [property: FromQuery(Name = "specName")] string? SpecName = null,
    [property: FromQuery(Name = "difficultyId")] int? DifficultyId = null,
    [property: FromQuery(Name = "page")] int? Page = null,
    [property: FromQuery(Name = "partition")] int? Partition = null,
    [property: FromQuery(Name = "bracket")] int? Bracket = null,
    [property: FromQuery(Name = "size")] int? Size = null,
    [property: FromQuery(Name = "serverRegion")] string? ServerRegion = null,
    [property: FromQuery(Name = "serverSlug")] string? ServerSlug = null,
    [property: FromQuery(Name = "filter")] string? Filter = null,
    [property: FromQuery(Name = "hardModeLevel")] string? HardModeLevel = null,
    [property: FromQuery(Name = "externalBuffs")] string? ExternalBuffs = null,
    [property: FromQuery(Name = "includeCombatantInfo")]
    [property: DefaultValue(false)] bool IncludeCombatantInfo = false,
    [property: FromQuery(Name = "includeOtherPlayers")]
    [property: DefaultValue(true)] bool IncludeOtherPlayers = true);
