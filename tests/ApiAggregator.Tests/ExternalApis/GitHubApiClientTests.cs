using System.Net;
using System.Net.Http.Json;
using ApiAggregator.Api.ExternalApis;
using ApiAggregator.Api.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ApiAggregator.Tests.ExternalApis;

public sealed class GitHubApiClientTests
{
    [Fact]
    public async Task FetchAsync_MapsRepositoriesToSourceItems()
    {
        var json = """
        {
          "items": [
            {
              "name": "dotnet",
              "full_name": "dotnet/runtime",
              "description": ".NET is a cross-platform runtime",
              "html_url": "https://github.com/dotnet/runtime",
              "updated_at": "2024-01-01T12:00:00Z",
              "language": "C#",
              "stargazers_count": 15000
            }
          ]
        }
        """;

        var handler = new FakeHandler(HttpStatusCode.OK, json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var options = Options.Create(new GitHubApiOptions());
        var client = new GitHubApiClient(http, options);

        var items = await client.FetchAsync(new AggregationRequest("dotnet", null, null, null), CancellationToken.None);

        items.Should().ContainSingle();
        var item = items[0];
        item.Source.Should().Be("GitHub");
        item.Title.Should().Be("dotnet/runtime");
        item.Url.Should().Be("https://github.com/dotnet/runtime");
        item.Category.Should().Be("C#");
        item.Relevance.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task FetchAsync_PropagatesHttpErrors()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable, "{}");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var options = Options.Create(new GitHubApiOptions());
        var client = new GitHubApiClient(http, options);

        var act = () => client.FetchAsync(AggregationRequest.Empty, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = JsonContent.Create(System.Text.Json.JsonDocument.Parse(_body).RootElement)
            });
    }
}
