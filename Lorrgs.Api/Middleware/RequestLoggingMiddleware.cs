using System.Diagnostics;

namespace Lorrgs.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["RequestId"] = context.TraceIdentifier
        });
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next(context);
        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
            logger.LogWarning("Request failed endpoint={Endpoint} status={StatusCode}",
                context.GetEndpoint()?.DisplayName, context.Response.StatusCode);
    }
}
