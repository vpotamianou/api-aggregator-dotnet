using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.Statistics;

public sealed class StatisticsStore : IStatisticsStore
{
    private readonly ConcurrentDictionary<string, SourceAccumulator> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly StatisticsOptions _options;

    public StatisticsStore(IOptions<StatisticsOptions> options)
    {
        _options = options.Value;
    }

    public void Record(string source, double elapsedMs, bool success)
    {
        var acc = _sources.GetOrAdd(source, _ => new SourceAccumulator());
        acc.Add(elapsedMs, success, _options.FastThresholdMs, _options.SlowThresholdMs);
    }

    public StatisticsSnapshot GetSnapshot()
    {
        var items = _sources
            .Select(kvp => kvp.Value.ToReadOnly(kvp.Key))
            .OrderBy(s => s.Source, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new StatisticsSnapshot(items, DateTimeOffset.UtcNow);
    }

    private sealed class SourceAccumulator
    {
        private long _total;
        private long _success;
        private long _failed;
        private long _sumMsFixed; // scaled by 100 for sub-ms precision
        private long _fast;
        private long _average;
        private long _slow;

        public void Add(double elapsedMs, bool success, int fastMax, int slowMin)
        {
            Interlocked.Increment(ref _total);
            if (success) Interlocked.Increment(ref _success);
            else Interlocked.Increment(ref _failed);

            Interlocked.Add(ref _sumMsFixed, (long)(elapsedMs * 100));

            if (elapsedMs < fastMax) Interlocked.Increment(ref _fast);
            else if (elapsedMs > slowMin) Interlocked.Increment(ref _slow);
            else Interlocked.Increment(ref _average);
        }

        public SourceStatistics ToReadOnly(string source)
        {
            var total = Interlocked.Read(ref _total);
            var sumFixed = Interlocked.Read(ref _sumMsFixed);
            var avg = total == 0 ? 0 : (sumFixed / 100.0) / total;

            return new SourceStatistics(
                Source: source,
                TotalRequests: total,
                SuccessfulRequests: Interlocked.Read(ref _success),
                FailedRequests: Interlocked.Read(ref _failed),
                AverageResponseMs: Math.Round(avg, 2),
                Buckets: new PerformanceBuckets(
                    Fast: Interlocked.Read(ref _fast),
                    Average: Interlocked.Read(ref _average),
                    Slow: Interlocked.Read(ref _slow)));
        }
    }
}
