using ApiAggregator.Api.Models;

namespace ApiAggregator.Api.Services;

public interface IAggregationService
{
    Task<AggregatedResponse> GetAsync(AggregationRequest request, CancellationToken cancellationToken);
}
