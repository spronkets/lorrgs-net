public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public int DefaultTtlSeconds { get; set; } = 86400;

    public int CleanupIntervalMinutes { get; set; } = 60;
}