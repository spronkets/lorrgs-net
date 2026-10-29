namespace Lorrgs.Api.Features.Rankings.QueueCompRankingUpdate;

/// <summary>Confirms the requested composition ranking update.</summary>
public sealed record QueueCompRankingUpdateResponse(
    string Message, string TaskId, QueueCompRankingUpdateRequest Payload);
