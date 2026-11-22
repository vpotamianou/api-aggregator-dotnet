using ApiAggregator.Api.Statistics;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Tests.Statistics;

public sealed class StatisticsStoreTests
{
    private static StatisticsStore BuildStore(int fast = 100, int slow = 200) =>
        new(Options.Create(new StatisticsOptions { FastThresholdMs = fast, SlowThresholdMs = slow }));

    [Fact]
    public void Record_PutsSampleInCorrectBucket()
    {
        var store = BuildStore();
        store.Record("NewsAPI", 50, success: true);     // fast
        store.Record("NewsAPI", 150, success: true);    // average
        store.Record("NewsAPI", 500, success: false);   // slow + fail

        var snapshot = store.GetSnapshot().Sources.Single();
        snapshot.TotalRequests.Should().Be(3);
        snapshot.SuccessfulRequests.Should().Be(2);
        snapshot.FailedRequests.Should().Be(1);
        snapshot.Buckets.Fast.Should().Be(1);
        snapshot.Buckets.Average.Should().Be(1);
        snapshot.Buckets.Slow.Should().Be(1);
        snapshot.AverageResponseMs.Should().BeApproximately((50 + 150 + 500) / 3.0, 0.01);
    }

    [Fact]
    public void GetSnapshot_IsEmpty_WhenNothingRecorded()
    {
        var store = BuildStore();
        store.GetSnapshot().Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task Record_IsThreadSafe_UnderConcurrentLoad()
    {
        var store = BuildStore();
        const int threads = 20;
        const int recordsPerThread = 1000;

        var tasks = Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < recordsPerThread; i++)
            {
                store.Record("NewsAPI", 50, success: true);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var snapshot = store.GetSnapshot().Sources.Single();
        snapshot.TotalRequests.Should().Be(threads * recordsPerThread);
        snapshot.SuccessfulRequests.Should().Be(threads * recordsPerThread);
        snapshot.Buckets.Fast.Should().Be(threads * recordsPerThread);
    }
}
