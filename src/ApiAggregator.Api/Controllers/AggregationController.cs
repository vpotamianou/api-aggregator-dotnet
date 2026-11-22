using ApiAggregator.Api.Models;
using ApiAggregator.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/aggregation")]
public sealed class AggregationController : ControllerBase
{
    private readonly IAggregationService _aggregation;

    public AggregationController(IAggregationService aggregation)
    {
        _aggregation = aggregation;
    }

    [HttpGet]
    public async Task<ActionResult<AggregatedResponse>> Get(
        [FromQuery] string? keyword,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? category,
        [FromQuery] SortBy sort = SortBy.Date,
        [FromQuery] bool descending = true,
        CancellationToken cancellationToken = default)
    {
        var request = new AggregationRequest(keyword, from, to, category, sort, descending);
        var response = await _aggregation.GetAsync(request, cancellationToken);
        return Ok(response);
    }
}
