using Lorrgs.Api.Features.WorldData;
using Lorrgs.Api.Features.WorldData.GetSpecs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Lorrgs.Api.Tests.Features.WorldData;

public sealed class WorldDataLoggingTests
{
    private static readonly WorldDataService Data = WorldDataService.Instance;

    [Theory]
    [InlineData("GetBoss", "BossSlug")]
    [InlineData("GetSpec", "SpecSlug")]
    [InlineData("GetZone", "ZoneSlug")]
    [InlineData("GetSeason", "SeasonSlug")]
    [InlineData("GetSpell", "SpellId")]
    [InlineData("GetTrinket", "TrinketId")]
    [InlineData("GetClassSpecs", "ClassSlug")]
    [InlineData("GetRoleSpecs", "RoleSlug")]
    [InlineData("GetZoneBosses", "ZoneSlug")]
    [InlineData("GetBossSpells", "BossSlug")]
    [InlineData("GetSpecSpells", "SpecSlug")]
    public void MissingEntitiesLogOnlyTheValueType(string feature, string parameter)
    {
        using var logs = new RecordingLoggerFactory();
        object missing = parameter.EndsWith("Id", StringComparison.Ordinal) ? -1234567 : "unverified-request-value";

        var result = Invoke(feature, parameter, missing, logs);

        HttpResultAssertions.Status(404, result);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("not found", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(missing.ToString()!, entry.Message);
        Assert.All(entry.Properties, property => Assert.Equal("{OriginalFormat}", property.Key));
        Assert.Null(entry.Exception);
    }

    public static IEnumerable<object[]> FoundEntities()
    {
        var boss = Data.Bosses.First();
        var spec = Data.Specs.First();
        var zone = Data.Zones.First();
        var season = Data.Seasons.First();
        var spell = Data.Spells.First();
        var trinket = Data.Trinkets.First();
        var wowClass = Data.Classes.First();
        var role = Data.Roles.First();
        yield return ["GetBoss", "BossSlug", boss.NameSlug, "Boss", boss.Id, boss.Name];
        yield return ["GetSpec", "SpecSlug", spec.FullNameSlug, "Spec", spec.Id, spec.FullName];
        yield return ["GetZone", "ZoneSlug", zone.Slug, "Zone", zone.Id, zone.Name];
        yield return ["GetSeason", "SeasonSlug", season.Slug, "Season", season.Id, season.Name];
        yield return ["GetSpell", "SpellId", spell.Id, "Spell", spell.Id, spell.Name];
        yield return ["GetTrinket", "TrinketId", trinket.Id, "Trinket", trinket.Id, trinket.Name];
        yield return ["GetClassSpecs", "ClassSlug", wowClass.NameSlug, "Class", wowClass.Id, wowClass.Name];
        yield return ["GetRoleSpecs", "RoleSlug", role.NameSlug, "Role", role.Id, role.Name];
        yield return ["GetZoneBosses", "ZoneSlug", zone.Slug, "Zone", zone.Id, zone.Name];
        yield return ["GetBossSpells", "BossSlug", boss.NameSlug, "Boss", boss.Id, boss.Name];
        yield return ["GetSpecSpells", "SpecSlug", spec.FullNameSlug, "Spec", spec.Id, spec.FullName];
    }

    [Theory]
    [MemberData(nameof(FoundEntities))]
    public void FoundEntitiesLogCatalogIdentifiersAndNames(
        string feature, string parameter, object value, string entity, int id, string name)
    {
        using var logs = new RecordingLoggerFactory();

        HttpResultAssertions.Status(200, Invoke(feature, parameter, value, logs));

        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(id, entry.Properties.Single(property => property.Key == $"{entity}Id").Value);
        Assert.Equal(name, entry.Properties.Single(property => property.Key == $"{entity}Name").Value);
        Assert.DoesNotContain(entry.Properties, property => property.Key.EndsWith("Slug", StringComparison.Ordinal));
        Assert.DoesNotContain("unverified-edition", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void UnmatchedSpecFiltersLogOnlyTheResultCount()
    {
        using var logs = new RecordingLoggerFactory();

        var response = GetSpecsHandler.Handle(Data, logs,
            new GetSpecsRequest { RoleId = -1234567, ClassId = -7654321 }, default);

        Assert.Empty(response.Value!.Specs);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(0, entry.Properties.Single(property => property.Key == "SpecCount").Value);
        Assert.DoesNotContain(entry.Properties, property => property.Key is "RoleId" or "ClassId");
        Assert.DoesNotContain("-1234567", entry.Message);
        Assert.DoesNotContain("-7654321", entry.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unverified-edition")]
    public async Task InvalidEditionLogsNoRequestValues(string edition)
    {
        using var logs = new RecordingLoggerFactory();
        var filter = new WorldDataEditionFilter(logs.CreateLogger<WorldDataEditionFilter>());
        var context = new DefaultHttpContext();
        context.Request.QueryString = QueryString.Create("edition", edition);
        var invocation = EndpointFilterInvocationContext.Create(context);

        var result = await filter.InvokeAsync(invocation, _ => throw new InvalidOperationException("Unexpected handler call"));

        HttpResultAssertions.Status(400, Assert.IsAssignableFrom<IResult>(result));
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("World data requested with missing or unsupported edition", entry.Message);
        Assert.All(entry.Properties, property => Assert.Equal("{OriginalFormat}", property.Key));
        Assert.Null(entry.Exception);
    }

    private static IResult Invoke(string feature, string parameter, object value, ILoggerFactory logs)
    {
        var prefix = $"Lorrgs.Api.Features.WorldData.{feature}.{feature}";
        var assembly = typeof(WorldDataService).Assembly;
        var requestType = assembly.GetType($"{prefix}Request", throwOnError: true)!;
        var request = Activator.CreateInstance(requestType)!;
        requestType.GetProperty(parameter)!.SetValue(request, value);
        requestType.GetProperty("Edition")!.SetValue(request, "unverified-edition");
        return (IResult)assembly.GetType($"{prefix}Handler", throwOnError: true)!
            .GetMethod("Handle")!.Invoke(null, [Data, logs, request, CancellationToken.None])!;
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);
        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();
        public void Dispose() { }
    }

    private sealed class RecordingLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Add(new(logLevel, formatter(state, exception),
                ((IEnumerable<KeyValuePair<string, object?>>)state!).ToList(), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message,
        List<KeyValuePair<string, object?>> Properties, Exception? Exception);
}
