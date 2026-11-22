using ApiAggregator.Api.Models;

namespace ApiAggregator.Api.ExternalApis;

public interface IExternalApiClient
{
    string SourceName { get; }

    Task<IReadOnlyList<SourceItem>> FetchAsync(AggregationRequest request, CancellationToken cancellationToken);
}
