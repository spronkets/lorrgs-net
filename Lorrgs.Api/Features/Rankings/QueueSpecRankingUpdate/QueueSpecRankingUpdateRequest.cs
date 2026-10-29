using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.Rankings.QueueSpecRankingUpdate;

/// <summary>Parameters for QueueSpecRankingUpdate.</summary>
public sealed record QueueSpecRankingUpdateRequest(
    [property: FromQuery(Name = "specSlug")]
    [property: DefaultValue("all")] string SpecSlug = "all",
    [property: FromQuery(Name = "bossSlug")]
    [property: DefaultValue("all")] string BossSlug = "all",
    [property: FromQuery(Name = "difficulty")]
    [property: DefaultValue("all")] string Difficulty = "all",
    [property: FromQuery(Name = "metric")]
    [property: DefaultValue("all")] string Metric = "all",
    [property: FromQuery(Name = "limit")]
    [property: DefaultValue(50)] int Limit = 50,
    [property: FromQuery(Name = "clear")]
    [property: DefaultValue(false)] bool Clear = false);
