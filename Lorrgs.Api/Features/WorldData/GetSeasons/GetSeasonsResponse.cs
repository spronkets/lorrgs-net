using Lorrgs.Api.Features.WorldData.Models;

namespace Lorrgs.Api.Features.WorldData.GetSeasons;

/// <summary>Get all seasons response.</summary>
public sealed record GetSeasonsResponse(List<Season> Seasons);
