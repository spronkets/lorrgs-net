using Microsoft.AspNetCore.Mvc;
using Lorrgs.Api.Features.WorldData.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lorrgs.Api.Features.WorldData.GetClasses;

public static class GetClassesHandler
{
    public static Ok<Dictionary<string, WowClass>> Handle(
        WorldDataService data, ILoggerFactory loggerFactory,
        [FromQuery] string? edition, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("GetClasses");
        // Return as dictionary keyed by name slug
        var classes = data.Classes
            .OrderBy(c => c.Name)
            .ToDictionary(c => c.NameSlug);
        logger.LogInformation("Found {ClassCount} classes", classes.Count);

        return TypedResults.Ok(classes);
    }
}
