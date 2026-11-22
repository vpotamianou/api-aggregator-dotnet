namespace ApiAggregator.Api.Models;

public sealed record SourceStatus(string Source, bool Success, string? Error, int ItemCount);

public sealed record AggregatedResponse(
    IReadOnlyList<SourceItem> Items,
    IReadOnlyList<SourceStatus> Sources,
    DateTimeOffset GeneratedAt);
