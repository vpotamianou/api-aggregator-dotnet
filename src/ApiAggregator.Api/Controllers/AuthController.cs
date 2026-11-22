using ApiAggregator.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ApiAggregator.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ITokenService _tokens;

    public AuthController(ITokenService tokens)
    {
        _tokens = tokens;
    }

    public sealed record TokenRequest(string Username, string Password);
    public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string TokenType);

    [HttpPost("token")]
    public ActionResult<TokenResponse> Issue([FromBody] TokenRequest request)
    {
        if (!_tokens.TryIssue(request.Username, request.Password, out var token, out var expiresAt))
        {
            return Unauthorized(new { error = "invalid_credentials" });
        }
        return Ok(new TokenResponse(token, expiresAt, "Bearer"));
    }
}
