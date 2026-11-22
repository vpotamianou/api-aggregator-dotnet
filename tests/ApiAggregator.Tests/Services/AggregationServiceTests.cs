using ApiAggregator.Api.ExternalApis;
using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ApiAggregator.Tests.Services;

public sealed class AggregationServiceTests
{
    private static AggregationService Build(params IExternalApiClient[] clients)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new CacheOptions { AggregatedTtlSeconds = 60, FallbackTtlSeconds = 3600 });
        return new AggregationService(clients, cache, opts, NullLogger<AggregationService>.Instance);
    }

    private static Mock<IExternalApiClient> MockClient(string source, IReadOnlyList<SourceItem> items)
    {
        var mock = new Mock<IExternalApiClient>();
        mock.SetupGet(c => c.SourceName).Returns(source);
        mock.Setup(c => c.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        return mock;
    }

    private static Mock<IExternalApiClient> MockFailingClient(string source, Exception ex)
    {
        var mock = new Mock<IExternalApiClient>();
        mock.SetupGet(c => c.SourceName).Returns(source);
        mock.Setup(c => c.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);
        return mock;
    }

    [Fact]
    public async Task GetAsync_MergesItemsFromAllSources()
    {
        var a = MockClient("A", new[] { new SourceItem("A", "a1", null, null, DateTimeOffset.UtcNow, null, 0.5) });
        var b = MockClient("B", new[] { new SourceItem("B", "b1", null, null, DateTimeOffset.UtcNow.AddMinutes(-5), null, 0.9) });
        var svc = Build(a.Object, b.Object);

        var result = await svc.GetAsync(AggregationRequest.Empty, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Sources.Should().OnlyContain(s => s.Success);
    }

    [Fact]
    public async Task GetAsync_FailingSource_ReturnsPartialResponseWithFailureStatus()
    {
        var good = MockClient("Good", new[] { new SourceItem("Good", "g", null, null, null, null, 1) });
        var bad = MockFailingClient("Bad", new HttpRequestException("boom"));
        var svc = Build(good.Object, bad.Object);

        var result = await svc.GetAsync(AggregationRequest.Empty, CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Sources.Should().Contain(s => s.Source == "Bad" && !s.Success && s.Error!.Contains("HttpRequestException"));
        result.Sources.Should().Contain(s => s.Source == "Good" && s.Success);
    }

    [Fact]
    public async Task GetAsync_UsesFallbackCache_WhenSourceFailsOnSecondCall()
    {
        var items = new[] { new SourceItem("Flaky", "ok", null, null, null, null, 1) };
        var mock = new Mock<IExternalApiClient>();
        mock.SetupGet(c => c.SourceName).Returns("Flaky");
        mock.SetupSequence(c => c.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items)
            .ThrowsAsync(new HttpRequestException("second call fails"));

        var svc = Build(mock.Object);

        var first = await svc.GetAsync(new AggregationRequest("x", null, null, null), CancellationToken.None);
        first.Sources.Single().Success.Should().BeTrue();

        // Different keyword forces a fresh aggregation, but the fallback key is per-request too,
        // so use a brand new request that shares the same fallback key as the first.
        var second = await svc.GetAsync(new AggregationRequest("x", null, null, null, SortBy.Relevance), CancellationToken.None);
        var status = second.Sources.Single();
        status.Success.Should().BeFalse();
        status.Error.Should().Contain("fallback");
        second.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAsync_CachesResultForSameRequest()
    {
        var items = new[] { new SourceItem("A", "once", null, null, null, null, 1) };
        var mock = new Mock<IExternalApiClient>();
        mock.SetupGet(c => c.SourceName).Returns("A");
        mock.Setup(c => c.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var svc = Build(mock.Object);

        await svc.GetAsync(AggregationRequest.Empty, CancellationToken.None);
        await svc.GetAsync(AggregationRequest.Empty, CancellationToken.None);

        mock.Verify(c => c.FetchAsync(It.IsAny<AggregationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_SortsByDateDescending_ByDefault()
    {
        var older = new SourceItem("A", "old", null, null, DateTimeOffset.UtcNow.AddDays(-2), null, 0.5);
        var newer = new SourceItem("A", "new", null, null, DateTimeOffset.UtcNow, null, 0.5);
        var client = MockClient("A", new[] { older, newer });
        var svc = Build(client.Object);

        var result = await svc.GetAsync(AggregationRequest.Empty, CancellationToken.None);

        result.Items.Select(i => i.Title).Should().ContainInOrder("new", "old");
    }

    [Fact]
    public async Task GetAsync_FiltersByCategory()
    {
        var items = new[]
        {
            new SourceItem("A", "weather news", null, null, DateTimeOffset.UtcNow, "weather", 1),
            new SourceItem("A", "tech news", null, null, DateTimeOffset.UtcNow, "tech", 1)
        };
        var client = MockClient("A", items);
        var svc = Build(client.Object);

        var result = await svc.GetAsync(new AggregationRequest(null, null, null, "weather"), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Title.Should().Be("weather news");
    }
}
