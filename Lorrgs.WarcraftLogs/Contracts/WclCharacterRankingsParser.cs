using System.Text.Json;

namespace Lorrgs.WarcraftLogs.Contracts;

public static class WclCharacterRankingsParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static WclCharacterRankingsPayload? Parse(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (payload.ValueKind == JsonValueKind.String)
        {
            var raw = payload.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                raw = raw.Trim();
                if (raw.StartsWith("[", StringComparison.Ordinal))
                {
                    var rankings = JsonSerializer.Deserialize<List<WclCharacterRankingEntry>>(raw, JsonOptions) ?? [];
                    return new WclCharacterRankingsPayload(0, 0, false, rankings, null);
                }

                var envelope = JsonSerializer.Deserialize<WclCharacterRankingsEnvelope>(raw, JsonOptions);
                return new WclCharacterRankingsPayload(
                    envelope?.Page ?? 0,
                    envelope?.Count ?? 0,
                    envelope?.HasMorePages ?? false,
                    envelope?.Rankings ?? [],
                    envelope?.Error);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (payload.ValueKind == JsonValueKind.Array)
        {
            var rankings = JsonSerializer.Deserialize<List<WclCharacterRankingEntry>>(payload.GetRawText(), JsonOptions) ?? [];
            return new WclCharacterRankingsPayload(0, 0, false, rankings, null);
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            var envelope = JsonSerializer.Deserialize<WclCharacterRankingsEnvelope>(payload.GetRawText(), JsonOptions);
            return new WclCharacterRankingsPayload(
                envelope?.Page ?? 0,
                envelope?.Count ?? 0,
                envelope?.HasMorePages ?? false,
                envelope?.Rankings ?? [],
                envelope?.Error);
        }

        return null;
    }
}
