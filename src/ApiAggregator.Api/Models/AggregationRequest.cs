namespace ApiAggregator.Api.Models;

public enum SortBy
{
    Date,
    Relevance,
    Source
}

public sealed record AggregationRequest(
    string? Keyword,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Category,
    SortBy Sort = SortBy.Date,
    bool Descending = true)
{
    public static AggregationRequest Empty { get; } = new(null, null, null, null);
}
