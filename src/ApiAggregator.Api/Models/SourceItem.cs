namespace ApiAggregator.Api.Models;

public sealed record SourceItem(
    string Source,
    string Title,
    string? Description,
    string? Url,
    DateTimeOffset? PublishedAt,
    string? Category,
    double Relevance);
