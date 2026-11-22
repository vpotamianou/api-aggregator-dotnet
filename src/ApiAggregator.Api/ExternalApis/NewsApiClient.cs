using System.Text.Json.Serialization;
using ApiAggregator.Api.Models;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.ExternalApis;

public sealed class NewsApiClient : IExternalApiClient
{
    public const string ClientName = "news";
    public string SourceName => "NewsAPI";

    private readonly HttpClient _http;
    private readonly NewsApiOptions _options;

    public NewsApiClient(HttpClient http, IOptions<NewsApiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(AggregationRequest request, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(request.Keyword) ? _options.DefaultQuery : request.Keyword;
        var from = request.From?.ToString("yyyy-MM-dd");
        var to = request.To?.ToString("yyyy-MM-dd");

        var url = $"v2/everything?q={Uri.EscapeDataString(query)}&sortBy=publishedAt&pageSize={_options.PageSize}&apiKey={_options.ApiKey}";
        if (from is not null) url += $"&from={from}";
        if (to is not null) url += $"&to={to}";

        var payload = await _http.GetFromJsonAsync<NewsResponse>(url, cancellationToken);
        if (payload?.Articles is null) return Array.Empty<SourceItem>();

        return payload.Articles
            .Select(a => new SourceItem(
                Source: SourceName,
                Title: a.Title ?? "(no title)",
                Description: a.Description,
                Url: a.Url,
                PublishedAt: a.PublishedAt,
                Category: a.Source?.Name,
                Relevance: 0.75))
            .ToArray();
    }

    private sealed record NewsResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("articles")] IReadOnlyList<Article>? Articles);

    private sealed record Article(
        [property: JsonPropertyName("source")] SourceBlock? Source,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt);

    private sealed record SourceBlock([property: JsonPropertyName("name")] string? Name);
}
