namespace ApiAggregator.Api.Statistics;

public sealed class StatisticsOptions
{
    public const string SectionName = "Statistics";
    public int FastThresholdMs { get; set; } = 100;
    public int SlowThresholdMs { get; set; } = 200;
}
