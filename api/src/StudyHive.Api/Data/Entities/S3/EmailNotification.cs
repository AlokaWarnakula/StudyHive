namespace StudyHive.Api.Data.Entities;

/// <summary>
/// One outbox row. It is written in the same transaction as the event it reports, so an email
/// exists only if that event committed. There is no body column: the row stores the template,
/// subject and booking request, and the sender renders the body from current data at send time.
/// </summary>
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

    /// <summary>A Queued row for a booking-request event, due immediately.</summary>
    public static EmailNotification ForBookingRequest(string toEmail, string template, Guid bookingRequestId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ToEmail = toEmail,
        Template = template,
        Subject = EmailTemplates.SubjectFor(template),
        BookingRequestId = bookingRequestId,
        Status = EmailNotificationStatus.Queued,
        CreatedAt = now,
    };
}

/// <summary>
/// The template names stored in email_notifications.template, and their subjects. What each body
/// is rendered from at send time:
/// <list type="bullet">
/// <item>BookingApproved / BookingRejected / BookingRevisionRequested — the request's latest
/// approval decision (comments) and its quotation; Approved also lists the room bookings and the
/// quotation total.</item>
/// <item>BookingValidationFailed — the request's latest workflow execution error_message, which is
/// the Validation agent's revision note.</item>
/// </list>
/// </summary>
public static class EmailTemplates
{
    public const string BookingApproved = "BookingApproved";
    public const string BookingRejected = "BookingRejected";
    public const string BookingRevisionRequested = "BookingRevisionRequested";
    public const string BookingValidationFailed = "BookingValidationFailed";

    public static string SubjectFor(string template) => template switch
    {
        BookingApproved => "Your StudyHive booking is approved",
        BookingRejected => "Your StudyHive booking request was rejected",
        BookingRevisionRequested => "Your StudyHive booking request needs changes",
        BookingValidationFailed => "Your StudyHive booking request could not be validated",
        _ => throw new ArgumentOutOfRangeException(nameof(template), template, "Unknown email template."),
    };

    public static string ForDecision(ApprovalDecisionType decision) => decision switch
    {
        ApprovalDecisionType.Approved => BookingApproved,
        ApprovalDecisionType.Rejected => BookingRejected,
        ApprovalDecisionType.RevisionRequested => BookingRevisionRequested,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown approval decision."),
    };
}
