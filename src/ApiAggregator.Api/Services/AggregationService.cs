using ApiAggregator.Api.ExternalApis;
using ApiAggregator.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.Services;

public sealed class AggregationService : IAggregationService
{
    private readonly IReadOnlyList<IExternalApiClient> _clients;
    private readonly IMemoryCache _cache;
    private readonly CacheOptions _cacheOptions;
    private readonly ILogger<AggregationService> _logger;

    public AggregationService(
        IEnumerable<IExternalApiClient> clients,
        IMemoryCache cache,
        IOptions<CacheOptions> cacheOptions,
        ILogger<AggregationService> logger)
    {
        _clients = clients.ToArray();
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<AggregatedResponse> GetAsync(AggregationRequest request, CancellationToken cancellationToken)
    {
        var finalKey = BuildFinalCacheKey(request);
        if (_cache.TryGetValue(finalKey, out AggregatedResponse? cached) && cached is not null)
        {
            return cached;
        }

        var fetchTasks = _clients
            .Select(c => FetchWithFallbackAsync(c, request, cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(fetchTasks);

        var allItems = results.SelectMany(r => r.Items);
        var filtered = ApplyFilter(allItems, request);
        var sorted = ApplySort(filtered, request).ToArray();

        var statuses = results.Select(r => r.Status).ToArray();
        var response = new AggregatedResponse(sorted, statuses, DateTimeOffset.UtcNow);

        _cache.Set(finalKey, response, TimeSpan.FromSeconds(_cacheOptions.AggregatedTtlSeconds));
        return response;
    }

    private async Task<SourceResult> FetchWithFallbackAsync(
        IExternalApiClient client, AggregationRequest request, CancellationToken cancellationToken)
    {
        var fallbackKey = BuildFallbackKey(client.SourceName, request);
        try
        {
            var items = await client.FetchAsync(request, cancellationToken);
            _cache.Set(fallbackKey, items, TimeSpan.FromSeconds(_cacheOptions.FallbackTtlSeconds));
            return new SourceResult(items, new SourceStatus(client.SourceName, true, null, items.Count));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Source {Source} failed. Attempting fallback cache.", client.SourceName);
            if (_cache.TryGetValue(fallbackKey, out IReadOnlyList<SourceItem>? stale) && stale is not null)
            {
                return new SourceResult(stale, new SourceStatus(client.SourceName, false, "served from fallback cache: " + ex.GetType().Name, stale.Count));
            }
            return new SourceResult(Array.Empty<SourceItem>(), new SourceStatus(client.SourceName, false, ex.GetType().Name + ": " + ex.Message, 0));
        }
    }

    private static IEnumerable<SourceItem> ApplyFilter(IEnumerable<SourceItem> items, AggregationRequest request)
    {
        if (request.From is not null)
            items = items.Where(i => i.PublishedAt is null || i.PublishedAt >= request.From);
        if (request.To is not null)
            items = items.Where(i => i.PublishedAt is null || i.PublishedAt <= request.To);
        if (!string.IsNullOrWhiteSpace(request.Category))
            items = items.Where(i => string.Equals(i.Category, request.Category, StringComparison.OrdinalIgnoreCase));
        return items;
    }

    private static IEnumerable<SourceItem> ApplySort(IEnumerable<SourceItem> items, AggregationRequest request) =>
        (request.Sort, request.Descending) switch
        {
            (SortBy.Date, true) => items.OrderByDescending(i => i.PublishedAt ?? DateTimeOffset.MinValue),
            (SortBy.Date, false) => items.OrderBy(i => i.PublishedAt ?? DateTimeOffset.MinValue),
            (SortBy.Relevance, true) => items.OrderByDescending(i => i.Relevance),
            (SortBy.Relevance, false) => items.OrderBy(i => i.Relevance),
            (SortBy.Source, true) => items.OrderByDescending(i => i.Source, StringComparer.OrdinalIgnoreCase),
            (SortBy.Source, false) => items.OrderBy(i => i.Source, StringComparer.OrdinalIgnoreCase),
            _ => items
        };

    private static string BuildFinalCacheKey(AggregationRequest r) =>
        $"agg:{r.Keyword}:{r.From:O}:{r.To:O}:{r.Category}:{r.Sort}:{r.Descending}";

    private static string BuildFallbackKey(string source, AggregationRequest r) =>
        $"fallback:{source}:{r.Keyword}:{r.From:O}:{r.To:O}:{r.Category}";

    private readonly record struct SourceResult(IReadOnlyList<SourceItem> Items, SourceStatus Status);
}
