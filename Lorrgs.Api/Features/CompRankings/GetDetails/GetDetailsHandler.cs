using System.Net;
using Lorrgs.Api.Shared;
using Lorrgs.WarcraftLogs;
using Lorrgs.WarcraftLogs.Services;

namespace Lorrgs.Api.Features.CompRankings.GetDetails;

public sealed class GetDetailsHandler(ICompRankingService service, ILogger<GetDetailsHandler> logger)
{
    public async Task<IResult> Handle(
        GetDetailsRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = WorldDataEditions.NormalizeEdition(request.Edition);
        if (normalized is null || request.FightId <= 0 || request.Code.Length > 32 || !request.Code.All(char.IsAsciiLetterOrDigit))
            return TypedResults.Problem(statusCode: 400, title: "Provide a valid report, fight ID and supported edition.");
        try { return TypedResults.Ok(await service.LoadDetailsAsync(request.Code, request.FightId, normalized, request.Refresh, cancellationToken)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (WarcraftLogsReportPrivateException) { return TypedResults.Problem(statusCode: 403, title: "This Warcraft Logs report is private or archived."); }
        catch (WarcraftLogsReportNotFoundException) { return TypedResults.Problem(statusCode: 404, title: "This Warcraft Logs report or fight is unavailable."); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        { return TypedResults.Problem(statusCode: 429, title: "Warcraft Logs rate limit reached. Please retry shortly."); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Composition detail loading failed report={ReportCode} fight={FightId}", request.Code, request.FightId);
            return TypedResults.Problem(statusCode: 502, title: "Unable to load this kill's details from Warcraft Logs.");
        }
    }
}
