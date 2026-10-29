using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WarcraftLogs.GetRankings;

public sealed class GetRankingsHandler
{
    public Ok<GetRankingsResponse[]> Handle(GetRankingsRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rankings = new[]
        {
            new GetRankingsResponse(1, "Abyssal", "Mage", "Fire", 99.7, request.Season ?? "current"),
            new GetRankingsResponse(2, "Stormcaller", "Shaman", "Elemental", 98.2, request.Season ?? "current")
        };

        return TypedResults.Ok(rankings);
    }
}
