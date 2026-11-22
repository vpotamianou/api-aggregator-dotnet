namespace ApiAggregator.Api.Services;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";
    public int AggregatedTtlSeconds { get; set; } = 60;
    public int FallbackTtlSeconds { get; set; } = 3600;
}
