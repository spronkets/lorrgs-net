using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.CompRankings.GetCompRanking;

/// <summary>Parameters for GetCompRanking.</summary>
public sealed record GetCompRankingRequest(
    [property: FromRoute(Name = "bossSlug")] string BossSlug,
    [property: FromQuery(Name = "edition")]
    [property: DefaultValue("")] string Edition = "",
    [property: FromQuery(Name = "encounterId")]
    [property: DefaultValue(0)] int EncounterId = 0,
    [property: FromQuery(Name = "difficultyId")]
    [property: DefaultValue(5)] int DifficultyId = 5,
    [property: FromQuery(Name = "page")]
    [property: DefaultValue(1)] int Page = 1,
    [property: FromQuery(Name = "refresh")]
    [property: DefaultValue(false)] bool Refresh = false,
    [property: FromQuery(Name = "role")] string[]? Roles = null,
    [property: FromQuery(Name = "spec")] string[]? Specs = null,
    [property: FromQuery(Name = "killtime_min")]
    [property: DefaultValue(0)] int MinSeconds = 0,
    [property: FromQuery(Name = "killtime_max")]
    [property: DefaultValue(0)] int MaxSeconds = 0,
    [property: FromQuery(Name = "zoneId")]
    [property: DefaultValue(0)] int ZoneId = 0);
