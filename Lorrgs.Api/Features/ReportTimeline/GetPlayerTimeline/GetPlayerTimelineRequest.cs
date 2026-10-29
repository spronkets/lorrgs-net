using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.ReportTimeline.GetPlayerTimeline;

/// <summary>Parameters for GetPlayerTimeline.</summary>
public sealed record GetPlayerTimelineRequest(
    [property: FromRoute(Name = "reportCode")] string ReportCode,
    [property: FromQuery(Name = "fightId")]
    [property: DefaultValue(0)] int FightId = default,
    [property: FromQuery(Name = "sourceId")] int? SourceId = null,
    [property: FromQuery(Name = "playerName")] string? PlayerName = null,
    [property: FromQuery(Name = "edition")]
    [property: DefaultValue("retail")] string Edition = "retail",
    [property: FromQuery(Name = "specId")] int? SpecId = null);
