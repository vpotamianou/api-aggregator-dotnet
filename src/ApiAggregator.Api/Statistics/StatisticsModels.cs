namespace ApiAggregator.Api.Statistics;

public sealed record PerformanceBuckets(long Fast, long Average, long Slow);

public sealed record SourceStatistics(
    string Source,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    double AverageResponseMs,
    PerformanceBuckets Buckets);

public sealed record StatisticsSnapshot(
    IReadOnlyList<SourceStatistics> Sources,
    DateTimeOffset GeneratedAt);
