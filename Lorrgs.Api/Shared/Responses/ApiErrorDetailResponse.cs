using System.Text.Json.Serialization;

namespace Lorrgs.Api.Shared.Responses;

public sealed record ApiErrorDetailResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("detail")] string Detail);
