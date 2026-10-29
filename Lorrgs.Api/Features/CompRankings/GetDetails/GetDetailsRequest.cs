using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;

namespace Lorrgs.Api.Features.CompRankings.GetDetails;

/// <summary>Parameters for GetDetails.</summary>
public sealed record GetDetailsRequest(
    [property: FromRoute(Name = "code")] string Code,
    [property: FromRoute(Name = "fightId")] int FightId,
    [property: FromQuery(Name = "edition")]
    [property: DefaultValue("")] string Edition = "",
    [property: FromQuery(Name = "refresh")]
    [property: DefaultValue(false)] bool Refresh = false);
