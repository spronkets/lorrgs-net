namespace Lorrgs.Api.Features.Rankings.QueueSpecRankingUpdate;

/// <summary>Confirms the requested spec ranking update.</summary>
public sealed record QueueSpecRankingUpdateResponse(
    string Message, string TaskId, QueueSpecRankingUpdateRequest Payload);
