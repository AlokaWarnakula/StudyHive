namespace StudyHive.Api.Services;

/// <summary>Sends one rendered email. The seam between <see cref="EmailDispatcher"/> and Brevo, so
/// tests fake delivery. A failure is a result, not an exception: <see cref="EmailSendResult.Error"/>
/// is stored as email_notifications.last_error, so it must never contain the API key.</summary>
public interface IEmailProvider
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed record EmailMessage(string ToEmail, string ToName, string Subject, string HtmlBody, string TextBody);

public sealed record EmailSendResult(bool Succeeded, string? ProviderMessageId, string? Error)
{
    public static EmailSendResult Sent(string? providerMessageId) => new(true, providerMessageId, null);
    public static EmailSendResult Failed(string error) => new(false, null, error);
}
