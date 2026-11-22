using System.Diagnostics;

namespace ApiAggregator.Api.Statistics;

public sealed class StatisticsHandler : DelegatingHandler
{
    private readonly IStatisticsStore _store;
    private readonly string _source;

    public StatisticsHandler(IStatisticsStore store, string source)
    {
        _store = store;
        _source = source;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            success = response.IsSuccessStatusCode;
            return response;
        }
        finally
        {
            stopwatch.Stop();
            _store.Record(_source, stopwatch.Elapsed.TotalMilliseconds, success);
        }
    }
}
