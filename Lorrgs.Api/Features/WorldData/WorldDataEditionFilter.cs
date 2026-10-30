using Lorrgs.Api.Shared;
using Lorrgs.Api.Shared.Responses;

namespace Lorrgs.Api.Features.WorldData;

public sealed class WorldDataEditionFilter(ILogger<WorldDataEditionFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var edition = context.HttpContext.Request.Query["edition"].ToString();
        if (WorldDataEditions.NormalizeEdition(edition) is null)
        {
            logger.LogWarning("World data requested with missing or unsupported edition");
            return TypedResults.BadRequest(
                new ApiErrorResponse("A supported edition query parameter is required."));
        }

        return await next(context);
    }
}
