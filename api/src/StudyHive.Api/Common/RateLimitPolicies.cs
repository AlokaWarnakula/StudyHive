namespace StudyHive.Api.Common;

public static class RateLimitPolicies
{
    /// <summary>Applied to POST /api/auth/login and /api/auth/refresh — see Program.cs for the limiter config.</summary>
    public const string AuthEndpoints = "AuthEndpoints";
}
