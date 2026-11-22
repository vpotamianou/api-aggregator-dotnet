using System.Text;
using ApiAggregator.Api.Auth;
using ApiAggregator.Api.ExternalApis;
using ApiAggregator.Api.Services;
using ApiAggregator.Api.Statistics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Options binding -------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.Configure<StatisticsOptions>(builder.Configuration.GetSection(StatisticsOptions.SectionName));
builder.Services.Configure<WeatherApiOptions>(builder.Configuration.GetSection(WeatherApiOptions.SectionName));
builder.Services.Configure<NewsApiOptions>(builder.Configuration.GetSection(NewsApiOptions.SectionName));
builder.Services.Configure<GitHubApiOptions>(builder.Configuration.GetSection(GitHubApiOptions.SectionName));

// --- Infrastructure --------------------------------------------------------
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IStatisticsStore, StatisticsStore>();
builder.Services.AddSingleton<ITokenService, TokenService>();

// --- External API typed clients (resilience + stats-tracking handler) ------
builder.Services.AddHttpClient<WeatherApiClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherApiOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
})
    .AddHttpMessageHandler(sp => new StatisticsHandler(sp.GetRequiredService<IStatisticsStore>(), "OpenWeatherMap"))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<NewsApiClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NewsApiOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("User-Agent", "ApiAggregator/1.0");
})
    .AddHttpMessageHandler(sp => new StatisticsHandler(sp.GetRequiredService<IStatisticsStore>(), "NewsAPI"))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<GitHubApiClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GitHubApiOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("User-Agent", opt.UserAgent);
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
})
    .AddHttpMessageHandler(sp => new StatisticsHandler(sp.GetRequiredService<IStatisticsStore>(), "GitHub"))
    .AddStandardResilienceHandler();

// Expose the clients via the common contract for the aggregator.
builder.Services.AddTransient<IExternalApiClient>(sp => sp.GetRequiredService<WeatherApiClient>());
builder.Services.AddTransient<IExternalApiClient>(sp => sp.GetRequiredService<NewsApiClient>());
builder.Services.AddTransient<IExternalApiClient>(sp => sp.GetRequiredService<GitHubApiClient>());

builder.Services.AddScoped<IAggregationService, AggregationService>();

// --- Authentication (JWT) --------------------------------------------------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// --- MVC + Swagger ---------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API Aggregator", Version = "v1" });
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: {token}"
    };
    c.AddSecurityDefinition("Bearer", bearer);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
