using System.Text.Json;

namespace Lorrgs.Api;

public static class LoggingConfiguration
{
    public static ILoggingBuilder AddApplicationLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        return logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
        });
    }
}
