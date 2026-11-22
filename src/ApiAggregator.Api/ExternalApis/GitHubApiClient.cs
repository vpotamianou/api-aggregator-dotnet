using System.Text.Json.Serialization;
using ApiAggregator.Api.Models;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.ExternalApis;

public sealed class GitHubApiClient : IExternalApiClient
{
    public const string ClientName = "github";
    public string SourceName => "GitHub";

    private readonly HttpClient _http;
    private readonly GitHubApiOptions _options;

    public GitHubApiClient(HttpClient http, IOptions<GitHubApiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(AggregationRequest request, CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(request.Keyword) ? _options.DefaultQuery : request.Keyword;
        var url = $"search/repositories?q={Uri.EscapeDataString(query)}&sort=stars&order=desc&per_page={_options.PageSize}";

        var payload = await _http.GetFromJsonAsync<RepoSearchResponse>(url, cancellationToken);
        if (payload?.Items is null) return Array.Empty<SourceItem>();

        return payload.Items
            .Select(r => new SourceItem(
                Source: SourceName,
                Title: r.FullName ?? r.Name ?? "(no name)",
                Description: r.Description,
                Url: r.HtmlUrl,
                PublishedAt: r.UpdatedAt,
                Category: r.Language ?? "repository",
                Relevance: NormalizeStars(r.Stargazers)))
            .ToArray();
    }

    private static double NormalizeStars(int stars) =>
        Math.Min(1.0, Math.Log10(Math.Max(1, stars)) / 6.0);

    private sealed record RepoSearchResponse([property: JsonPropertyName("items")] IReadOnlyList<Repo>? Items);

    private sealed record Repo(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("full_name")] string? FullName,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
        [property: JsonPropertyName("language")] string? Language,
        [property: JsonPropertyName("stargazers_count")] int Stargazers);
}
