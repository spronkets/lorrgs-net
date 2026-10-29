using System.Text.Json.Serialization;

namespace Lorrgs.Api.Shared.Responses;

/// <summary>A common API error response.</summary>
public sealed record ApiErrorResponse([property: JsonPropertyName("error")] string Error);
