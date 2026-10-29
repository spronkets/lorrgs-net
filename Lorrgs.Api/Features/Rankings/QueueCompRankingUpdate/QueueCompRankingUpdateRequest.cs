using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.Rankings.QueueCompRankingUpdate;

/// <summary>Parameters for QueueCompRankingUpdate.</summary>
public sealed record QueueCompRankingUpdateRequest(
    [property: FromQuery(Name = "bossSlug")]
    [property: DefaultValue("all")] string BossSlug = "all",
    [property: FromQuery(Name = "limit")]
    [property: DefaultValue(50)] int Limit = 50,
    [property: FromQuery(Name = "clear")]
    [property: DefaultValue(false)] bool Clear = false);
