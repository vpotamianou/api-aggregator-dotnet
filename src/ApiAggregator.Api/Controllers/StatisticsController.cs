using ApiAggregator.Api.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/statistics")]
public sealed class StatisticsController : ControllerBase
{
    private readonly IStatisticsStore _store;

    public StatisticsController(IStatisticsStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<StatisticsSnapshot> Get() => Ok(_store.GetSnapshot());
}
