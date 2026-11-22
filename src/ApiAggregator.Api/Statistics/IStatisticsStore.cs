namespace ApiAggregator.Api.Statistics;

public interface IStatisticsStore
{
    void Record(string source, double elapsedMs, bool success);
    StatisticsSnapshot GetSnapshot();
}
