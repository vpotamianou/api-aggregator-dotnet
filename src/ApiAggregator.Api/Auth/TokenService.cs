using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiAggregator.Api.Auth;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public bool TryIssue(string username, string password, out string token, out DateTimeOffset expiresAt)
    {
        token = string.Empty;
        expiresAt = default;

        if (!string.Equals(username, _options.DemoUsername, StringComparison.Ordinal) ||
            !string.Equals(password, _options.DemoPassword, StringComparison.Ordinal))
        {
            return false;
        }

        expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpirationMinutes);
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return true;
    }
}
