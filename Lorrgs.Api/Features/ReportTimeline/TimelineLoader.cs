using System.Net;
using Lorrgs.WarcraftLogs;
using Lorrgs.WarcraftLogs.Services;

namespace Lorrgs.Api.Features.ReportTimeline;

public sealed class TimelineLoader(IReportTimelineService service, ILogger<TimelineLoader> logger)
{
    internal async Task<IResult> Load(string reportCode, int fightId, int? sourceId,
        string? playerName, string edition, bool boss, CancellationToken cancellationToken, int? specId)
    {
        if (!IsValid(reportCode, fightId, sourceId, playerName, edition, boss))
        {
            logger.LogWarning("Invalid timeline request");
            return TypedResults.Problem(statusCode: 400, detail: "Provide a valid report, fight, player and edition.");
        }
        try
        {
            if (boss)
                return TypedResults.Ok(await service.LoadBossAsync(reportCode, fightId, edition, false, cancellationToken)
                    ?? throw new InvalidOperationException("The boss timeline could not be loaded."));
            return TypedResults.Ok(await service.LoadAsync(reportCode, fightId, sourceId, playerName, edition,
                cancellationToken, specId));
        }
        catch (WarcraftLogsReportPrivateException)
        {
            logger.LogWarning("Timeline report is private");
            return TypedResults.Problem(statusCode: 403, detail: "This Warcraft Logs report is private. Make it public to load its timeline.");
        }
        catch (WarcraftLogsReportNotFoundException)
        {
            logger.LogWarning("Timeline report missing");
            return TypedResults.Problem(statusCode: 404, detail: "This Warcraft Logs report was not found; it may have been deleted or its code may be invalid.");
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning("Timeline report or player missing");
            return TypedResults.Problem(statusCode: 404, detail: ex.Message);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Timeline rate limited");
            return TypedResults.Problem(statusCode: 429, detail: "Warcraft Logs rate limit reached. Retry after the cooldown.");
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException)
        {
            logger.LogError("Timeline loading failed exceptionType={ExceptionType}", ex.GetType().FullName);
            return TypedResults.Problem(statusCode: 502, detail: "Unable to load this timeline from Warcraft Logs.");
        }
    }

    private static bool IsValid(string reportCode, int fightId, int? sourceId, string? playerName, string edition, bool boss) =>
        !string.IsNullOrWhiteSpace(reportCode) && reportCode.Length <= 32 &&
        reportCode.All(char.IsAsciiLetterOrDigit) && fightId > 0 &&
        (boss || sourceId is > 0 || !string.IsNullOrWhiteSpace(playerName)) &&
        new[] { "retail", "anniversary", "mistsofpandaria", "era" }.Contains(edition);

}
