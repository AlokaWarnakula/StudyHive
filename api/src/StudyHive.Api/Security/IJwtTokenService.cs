using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Security;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    AccessToken GenerateAccessToken(User user);
}
