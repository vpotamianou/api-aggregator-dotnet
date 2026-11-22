# API Aggregator

An ASP.NET Core service that aggregates data from three external APIs — **OpenWeatherMap**, **NewsAPI**, and **GitHub** — into a single unified endpoint, with JWT authentication, caching, resilience, and per-source request statistics.

## Stack

- .NET 10 / ASP.NET Core Web API
- `IHttpClientFactory` with typed clients
- `Microsoft.Extensions.Http.Resilience` (Polly v8) for retry + circuit breaker
- `IMemoryCache` for response + fallback caches
- JWT bearer authentication
- xUnit + Moq + FluentAssertions for tests
- Swagger UI for interactive docs

## Prerequisites

- .NET 10 SDK (`dotnet --version` → 10.x)
- An **OpenWeatherMap** API key — free at <https://openweathermap.org/api>
- A **NewsAPI** API key — free at <https://newsapi.org/>
- GitHub needs no key for public search endpoints

## Configuration

Edit `src/ApiAggregator.Api/appsettings.json` and paste your keys:

```json
"ExternalApis": {
  "Weather": { "ApiKey": "YOUR_OPENWEATHERMAP_KEY" },
  "News":    { "ApiKey": "YOUR_NEWSAPI_KEY" }
}
```

For local development, prefer `appsettings.Development.json` or user secrets:

```bash
cd src/ApiAggregator.Api
dotnet user-secrets init
dotnet user-secrets set "ExternalApis:Weather:ApiKey" "<your-key>"
dotnet user-secrets set "ExternalApis:News:ApiKey" "<your-key>"
```

The demo JWT credentials in `appsettings.json` are:

```
username: demo
password: demo123
```

## Run

```bash
dotnet run --project src/ApiAggregator.Api
```

Browse to `https://localhost:<port>/swagger` for the interactive UI.

## Test

```bash
dotnet test
```

## Endpoints

| Method | Route               | Auth       | Description                                 |
| ------ | ------------------- | ---------- | ------------------------------------------- |
| POST   | `/api/auth/token`   | anonymous  | Exchange demo credentials for a JWT         |
| GET    | `/api/aggregation`  | **Bearer** | Return aggregated items from all sources    |
| GET    | `/api/statistics`   | **Bearer** | Return per-source counts + bucketed timings |

### `POST /api/auth/token`

```json
{ "username": "demo", "password": "demo123" }
```

Response:

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresAt": "2026-11-21T15:00:00Z",
  "tokenType": "Bearer"
}
```

Send the token on subsequent calls:

```
Authorization: Bearer <token>
```

### `GET /api/aggregation`

Query parameters (all optional):

| Param        | Type      | Notes                                                        |
| ------------ | --------- | ------------------------------------------------------------ |
| `keyword`    | string    | Forwarded to each source (city for weather, query for rest)  |
| `from`       | ISO date  | Lower bound on `publishedAt`                                 |
| `to`         | ISO date  | Upper bound on `publishedAt`                                 |
| `category`   | string    | Exact-match filter on the unified `category` field           |
| `sort`       | enum      | `Date` (default), `Relevance`, or `Source`                   |
| `descending` | bool      | Default `true`                                               |

Response (truncated):

```json
{
  "items": [
    {
      "source": "NewsAPI",
      "title": "...",
      "description": "...",
      "url": "https://...",
      "publishedAt": "2026-11-21T10:30:00Z",
      "category": "BBC News",
      "relevance": 0.75
    }
  ],
  "sources": [
    { "source": "OpenWeatherMap", "success": true, "error": null, "itemCount": 1 },
    { "source": "NewsAPI",        "success": true, "error": null, "itemCount": 20 },
    { "source": "GitHub",         "success": false, "error": "served from fallback cache: HttpRequestException", "itemCount": 10 }
  ],
  "generatedAt": "2026-11-21T10:30:05Z"
}
```

### `GET /api/statistics`

```json
{
  "sources": [
    {
      "source": "GitHub",
      "totalRequests": 42,
      "successfulRequests": 41,
      "failedRequests": 1,
      "averageResponseMs": 143.27,
      "buckets": { "fast": 10, "average": 28, "slow": 4 }
    }
  ],
  "generatedAt": "2026-11-21T10:30:05Z"
}
```

Bucket thresholds live in `appsettings.json` → `Statistics`:

| Bucket   | Default range                    |
| -------- | -------------------------------- |
| Fast     | `< 100 ms`                       |
| Average  | `100 ms – 200 ms` (inclusive)    |
| Slow     | `> 200 ms`                       |

## Architecture

```
HTTP → Controller → AggregationService ──► [IExternalApiClient × N] → HttpClient (typed)
                         │                                              ├─ StatisticsHandler  (records ms + success)
                         │                                              └─ Resilience pipeline (retry, CB, timeout)
                         ├─ IMemoryCache (final response, short TTL)
                         └─ IMemoryCache (per-source fallback, long TTL)

                                           ↑
                                   StatisticsStore (in-memory, thread-safe)
```

Key design choices:

- **Single abstraction for sources.** Every external API implements `IExternalApiClient`, which returns a neutral `SourceItem`. Adding a new source is one class + one DI registration.
- **Parallelism.** `AggregationService.GetAsync` fires all clients concurrently via `Task.WhenAll`; the total latency is the slowest client, not the sum.
- **Fallback.** A per-source "last-known-good" cache (default 1 hour) kicks in when a client throws. The response tells the caller which source was served stale.
- **Statistics via `DelegatingHandler`.** Each typed `HttpClient` has a `StatisticsHandler` attached — no service code is aware of stats; it just works at the transport layer.
- **Thread-safe stats.** `ConcurrentDictionary` + `Interlocked` on fixed-point counters. The elapsed-ms sum is stored as `long` (scaled ×100) so updates stay lock-free.
- **Resilience.** `.AddStandardResilienceHandler()` gives us retry (exponential backoff + jitter), circuit breaker, and per-attempt timeout out of the box.
- **Caching.** Two layers: whole-response cache (short TTL) so repeat calls skip all work; per-source fallback cache (long TTL) that survives client failures.

## Project layout

```
src/
└── ApiAggregator.Api/
    ├── Auth/               # JWT options + token service
    ├── Controllers/        # Aggregation, Statistics, Auth
    ├── ExternalApis/       # IExternalApiClient + 3 client impls + options
    ├── Models/             # Unified DTOs
    ├── Services/           # AggregationService + CacheOptions
    ├── Statistics/         # Store, handler, options, snapshot model
    ├── Program.cs
    └── appsettings.json
tests/
└── ApiAggregator.Tests/
    ├── Services/           # AggregationService tests (fallback, filter, sort, cache)
    ├── Statistics/         # StatisticsStore tests (concurrency, buckets)
    └── ExternalApis/       # GitHubApiClient mapping test (via fake HttpMessageHandler)
```

## Adding a new source

1. Create `Foo/FooApiOptions.cs` + a `FooApiClient : IExternalApiClient`.
2. Register in `Program.cs`:
   ```csharp
   builder.Services.AddHttpClient<FooApiClient>(...)
       .AddHttpMessageHandler(sp => new StatisticsHandler(sp.GetRequiredService<IStatisticsStore>(), "Foo"))
       .AddStandardResilienceHandler();
   builder.Services.AddTransient<IExternalApiClient>(sp => sp.GetRequiredService<FooApiClient>());
   ```
3. Done. The aggregator will pick it up, stats will be tracked, resilience will apply.

## Notes & future work

- The optional performance-anomaly background service was intentionally skipped to keep scope tight. The hook point is `IStatisticsStore` — a periodic `BackgroundService` can compare rolling windows and log anomalies.
- The demo token endpoint accepts a single hardcoded user — a real deployment would swap `TokenService` for one backed by an identity store.
- Stats are in-process only. In a multi-instance deployment they would need to be externalized (Redis, Prometheus).
