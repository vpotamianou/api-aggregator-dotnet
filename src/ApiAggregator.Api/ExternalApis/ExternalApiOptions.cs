namespace ApiAggregator.Api.ExternalApis;

public sealed class WeatherApiOptions
{
    public const string SectionName = "ExternalApis:Weather";
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultCity { get; set; } = "Athens";
    public int TimeoutSeconds { get; set; } = 5;
}

public sealed class NewsApiOptions
{
    public const string SectionName = "ExternalApis:News";
    public string BaseUrl { get; set; } = "https://newsapi.org/";
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultQuery { get; set; } = "technology";
    public int PageSize { get; set; } = 20;
    public int TimeoutSeconds { get; set; } = 5;
}

public sealed class GitHubApiOptions
{
    public const string SectionName = "ExternalApis:GitHub";
    public string BaseUrl { get; set; } = "https://api.github.com/";
    public string DefaultQuery { get; set; } = "stars:>10000";
    public int PageSize { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 5;
    public string UserAgent { get; set; } = "ApiAggregator/1.0";
}
