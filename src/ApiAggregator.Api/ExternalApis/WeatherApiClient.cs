using System.Text.Json.Serialization;
using ApiAggregator.Api.Models;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Api.ExternalApis;

public sealed class WeatherApiClient : IExternalApiClient
{
    public const string ClientName = "weather";
    public string SourceName => "OpenWeatherMap";

    private readonly HttpClient _http;
    private readonly WeatherApiOptions _options;

    public WeatherApiClient(HttpClient http, IOptions<WeatherApiOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<SourceItem>> FetchAsync(AggregationRequest request, CancellationToken cancellationToken)
    {
        var city = string.IsNullOrWhiteSpace(request.Keyword) ? _options.DefaultCity : request.Keyword;
        var url = $"data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={_options.ApiKey}&units=metric";

        var payload = await _http.GetFromJsonAsync<WeatherResponse>(url, cancellationToken);
        if (payload is null) return Array.Empty<SourceItem>();

        var description = payload.Weather?.FirstOrDefault()?.Description ?? "n/a";
        var temp = payload.Main?.Temp ?? 0;
        var publishedAt = DateTimeOffset.FromUnixTimeSeconds(payload.Dt);

        return
        [
            new SourceItem(
                Source: SourceName,
                Title: $"Weather in {payload.Name}",
                Description: $"{temp:0.#}°C, {description}",
                Url: $"https://openweathermap.org/city/{payload.Id}",
                PublishedAt: publishedAt,
                Category: "weather",
                Relevance: 1.0)
        ];
    }

    private sealed record WeatherResponse(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("dt")] long Dt,
        [property: JsonPropertyName("main")] MainBlock? Main,
        [property: JsonPropertyName("weather")] IReadOnlyList<WeatherEntry>? Weather);

    private sealed record MainBlock([property: JsonPropertyName("temp")] double Temp);

    private sealed record WeatherEntry(
        [property: JsonPropertyName("main")] string Main,
        [property: JsonPropertyName("description")] string Description);
}
