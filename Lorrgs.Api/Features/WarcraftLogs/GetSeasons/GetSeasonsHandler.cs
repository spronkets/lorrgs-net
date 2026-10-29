using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WarcraftLogs.GetSeasons;

public sealed class GetSeasonsHandler
{
    public Ok<WarcraftLogsSeasonResponse[]> Handle(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var seasons = new[]
        {
            new WarcraftLogsSeasonResponse("season-1", "Season 1", "The War Within"),
            new WarcraftLogsSeasonResponse("season-2", "Season 2", "The War Within")
        };

        return TypedResults.Ok(seasons);
    }
}
