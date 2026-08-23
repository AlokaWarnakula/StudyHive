namespace StudyHive.Api.Common;

public static class RateLimitPolicies
{
    /// <summary>Applied to public register/login/refresh endpoints — see Program.cs for the limiter config.</summary>
    public const string AuthEndpoints = "AuthEndpoints";

    /// <summary>Applied to POST /api/booking-requests/{id}/submit — an authenticated agent-workflow
    /// trigger, expensive enough to rate limit per user rather than leave uncapped (Codex security
    /// review, P2). See Program.cs for the limiter config.</summary>
    public const string WorkflowSubmit = "WorkflowSubmit";
}
