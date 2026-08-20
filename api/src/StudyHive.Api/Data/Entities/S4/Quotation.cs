namespace StudyHive.Api.Data.Entities;

public class Quotation
{
    public Guid Id { get; set; }
    public Guid BookingRequestId { get; set; }
    public int Version { get; set; } = 1;
    public decimal RoomFee { get; set; }
    public decimal ConsumableCost { get; set; }

    // Generated always as (room_fee + consumable_cost) stored.
    public decimal TotalAmount { get; private set; }

    public decimal BudgetSnapshot { get; set; }

    // Generated always as (room_fee + consumable_cost <= budget_snapshot) stored.
    public bool WithinBudget { get; private set; }

    public string Currency { get; set; } = "LKR";
    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public BookingRequest BookingRequest { get; set; } = null!;
    public ICollection<QuotationLineItem> LineItems { get; set; } = new List<QuotationLineItem>();
    public ICollection<ApprovalDecision> ApprovalDecisions { get; set; } = new List<ApprovalDecision>();
}
