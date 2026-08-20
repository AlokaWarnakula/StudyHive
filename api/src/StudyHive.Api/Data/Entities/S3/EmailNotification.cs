namespace StudyHive.Api.Data.Entities;

public class EmailNotification
{
    public Guid Id { get; set; }
    public required string ToEmail { get; set; }
    public required string Template { get; set; }
    public required string Subject { get; set; }
    public Guid? BookingRequestId { get; set; }
    public EmailNotificationStatus Status { get; set; } = EmailNotificationStatus.Queued;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public BookingRequest? BookingRequest { get; set; }
}
