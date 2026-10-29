namespace Lorrgs.Api.Features.Rankings.MarkSpecRankingDirty;

/// <summary>Confirms a change to the requested ranking refresh state.</summary>
public sealed record MarkSpecRankingDirtyResponse(
    string Message, string SpecSlug, string BossSlug, string Difficulty, string Metric);
