namespace ApiAggregator.Api.Auth;

public interface ITokenService
{
    bool TryIssue(string username, string password, out string token, out DateTimeOffset expiresAt);
}
