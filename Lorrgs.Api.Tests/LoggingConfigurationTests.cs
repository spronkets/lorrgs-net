using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Lorrgs.Api.Tests;

public sealed class LoggingConfigurationTests
{
    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    [InlineData("<script>alert('forged')</script>")]
    public void JsonLoggingEncodesEveryFieldInOneRecord(string input)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddDebug().AddApplicationLogging());
        using var provider = services.BuildServiceProvider();
        Assert.IsType<ConsoleLoggerProvider>(Assert.Single(provider.GetServices<ILoggerProvider>()));
        var formatter = Assert.Single(provider.GetServices<ConsoleFormatter>(),
            item => item.Name == ConsoleFormatterNames.Json);
        var scopes = new LoggerExternalScopeProvider();
        using var textScope = scopes.Push($"scope{input}");
        using var structuredScope = scopes.Push(new Dictionary<string, object?> { ["ScopeValue"] = input });
        var exception = new InvalidOperationException($"outer{input}", new Exception($"inner{input}"));
        var state = new List<KeyValuePair<string, object?>>
        {
            new("ReportCode", input),
            new("{OriginalFormat}", "Report {ReportCode}")
        };
        var entry = new LogEntry<List<KeyValuePair<string, object?>>>(
            LogLevel.Error, $"category{input}", new EventId(42), state, exception,
            (values, _) => $"Report {values[0].Value}");
        using var output = new StringWriter();

        formatter.Write(in entry, scopes, output);

        var text = output.ToString();
        var record = Assert.Single(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        record = record.TrimEnd('\r');
        Assert.DoesNotContain(record, character => character is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029');
        Assert.DoesNotContain("<script>", record);
        using var document = JsonDocument.Parse(record);
        var root = document.RootElement;
        Assert.Equal($"Report {input}", root.GetProperty("Message").GetString());
        Assert.Equal(input, root.GetProperty("State").GetProperty("ReportCode").GetString());
        Assert.Equal(exception.ToString(), root.GetProperty("Exception").GetString());
        Assert.Equal($"category{input}", root.GetProperty("Category").GetString());
        Assert.Equal($"scope{input}", root.GetProperty("Scopes")[0].GetString());
        Assert.Equal(input, root.GetProperty("Scopes")[1].GetProperty("ScopeValue").GetString());
    }
}
