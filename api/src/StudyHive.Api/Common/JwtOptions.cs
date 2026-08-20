namespace StudyHive.Api.Common;

/// <summary>Bound from the "Jwt" config section. SigningKey must come from an environment variable / secret store in every real environment — never commit a real key.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}
