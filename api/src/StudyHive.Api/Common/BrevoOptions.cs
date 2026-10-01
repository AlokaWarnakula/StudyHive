namespace StudyHive.Api.Common;

/// <summary>Bound from the "Brevo" config section (env <c>Brevo__ApiKey</c>, <c>Brevo__SenderEmail</c>,
/// <c>Brevo__SenderName</c>). With no key or sender the email sender is not registered at all: queued
/// rows simply stay Queued, and nothing else in the API depends on email. The key is a secret — it
/// lives in the gitignored root .env or the host's environment and is never logged.</summary>
public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; init; } = "";
    public string SenderEmail { get; init; } = "";
    public string SenderName { get; init; } = "StudyHive";
    public string BaseUrl { get; init; } = "https://api.brevo.com";

    /// <summary>How often the sender looks for due rows.</summary>
    public int PollIntervalSeconds { get; init; } = 10;

    /// <summary>The most rows one poll sends; the rest wait for the next poll.</summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>Delay before the first retry; each further retry doubles it (1 min, 2 min, ...).</summary>
    public int RetryBaseSeconds { get; init; } = 60;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SenderEmail);
}
