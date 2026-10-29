using System.Net;
using Lorrgs.Api.Shared;
using Lorrgs.WarcraftLogs.Services;

namespace Lorrgs.Api.Features.CompRankings.GetCompRanking;

public sealed class GetCompRankingHandler(ICompRankingService service, ILogger<GetCompRankingHandler> logger)
{
    public async Task<IResult> Handle(
        GetCompRankingRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEdition = WorldDataEditions.NormalizeEdition(request.Edition);
        if (normalizedEdition is null || request.EncounterId <= 0 || request.DifficultyId is < 1 or > 5 ||
            request.Page is < 1 or > 100 || request.MinSeconds is < 0 or > 86400 ||
            request.MaxSeconds is < 0 or > 86400 || (request.MaxSeconds > 0 && request.MaxSeconds < request.MinSeconds) ||
            (request.Roles?.Length ?? 0) + (request.Specs?.Length ?? 0) > 100)
            return TypedResults.Problem(statusCode: 400, title: "Invalid composition search",
                detail: "Provide a supported edition, encounterId, difficultyId (1–5), page (1–100), and an ordered nonnegative kill-time range in seconds.");
        CompRankingSearch search;
        try
        {
            var roleFilters = (request.Roles ?? []).Select(CompCountFilter.Parse).ToArray();
            var specFilters = (request.Specs ?? []).Select(CompCountFilter.Parse).ToArray();
            if (roleFilters.Any(filter => filter.Key is not ("tank" or "heal" or "mdps" or "rdps")))
                throw new ArgumentException("Roles must be tank, heal, mdps, or rdps.");
            search = new(request.BossSlug, normalizedEdition, request.EncounterId, request.DifficultyId, request.Page,
                request.MinSeconds, request.MaxSeconds, roleFilters, specFilters, request.Refresh);
        }
        catch (ArgumentException ex) { return TypedResults.Problem(statusCode: 400, title: "Invalid count filter", detail: ex.Message); }
        try
        {
            var result = await service.LoadAsync(search, cancellationToken);
            return result is null ? TypedResults.Problem(statusCode: 404, title: "Encounter not found") : TypedResults.Ok(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return TypedResults.Problem(statusCode: 429, title: "Warcraft Logs rate limit reached. Please retry shortly.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Composition search failed encounter={EncounterId} edition={Edition}", request.EncounterId, normalizedEdition);
            return TypedResults.Problem(statusCode: 502, title: "Unable to load composition rankings from Warcraft Logs.");
        }

    }
}
