namespace StudyHive.Api.Data.Entities;

public class ApprovalDecision
{
    public Guid Id { get; set; }
    public Guid QuotationId { get; set; }
    public Guid DecidedBy { get; set; }
    public required string DecidedByRole { get; set; }
    public ApprovalDecisionType Decision { get; set; }
    public string? Comments { get; set; }
    public DateTimeOffset DecidedAt { get; set; }

    public Quotation Quotation { get; set; } = null!;
    public User DecidedByUser { get; set; } = null!;
}
