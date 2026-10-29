namespace Lorrgs.Api.Features.WarcraftLogs.GetSeasons;

/// <summary>A season available for Warcraft Logs rankings.</summary>
public sealed record WarcraftLogsSeasonResponse(string Id, string Name, string Expansion);
