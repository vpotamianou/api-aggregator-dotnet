namespace ApiAggregator.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "ApiAggregator";
    public string Audience { get; set; } = "ApiAggregator.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
    public string DemoUsername { get; set; } = "demo";
    public string DemoPassword { get; set; } = "demo123";
}
