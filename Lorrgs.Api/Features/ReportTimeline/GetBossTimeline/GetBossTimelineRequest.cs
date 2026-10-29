using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.ReportTimeline.GetBossTimeline;

/// <summary>Parameters for GetBossTimeline.</summary>
public sealed record GetBossTimelineRequest(
    [property: FromRoute(Name = "reportCode")] string ReportCode,
    [property: FromQuery(Name = "fightId")]
    [property: DefaultValue(0)] int FightId = default,
    [property: FromQuery(Name = "edition")]
    [property: DefaultValue("retail")] string Edition = "retail");
