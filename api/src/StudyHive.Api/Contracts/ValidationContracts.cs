namespace StudyHive.Api.Contracts;

/// <summary>
/// The S4 Validation Agent's typed contract (agent/app/schemas.py ValidationRequest). Everything on
/// it is StudyHive.Api's own data: <see cref="ProposedSlots"/> is step 2's Scheduling output,
/// <see cref="Items"/> is the same availability/price snapshot step 3's Resource request carried, and
/// <see cref="Rooms"/> is the trusted room snapshot for the rooms those slots name. The agent has no
/// database access, and no rule reads <see cref="Objective"/>.
/// </summary>
public sealed class ValidationRequest
{
    public required string Objective { get; init; }
    public required int GroupSize { get; init; }
    public required decimal Budget { get; init; }
    public required int SessionsRequired { get; init; }
    public required int SessionDurationMinutes { get; init; }
    public required IReadOnlyList<SchedulingSlot> ProposedSlots { get; init; }
    public required IReadOnlyList<SchedulingRoom> Rooms { get; init; }
    public required IReadOnlyList<ResourceRequestItem> Items { get; init; }
}

public sealed class ValidationRuleResult
{
    public required string Rule { get; init; }
    public required bool Passed { get; init; }
    public required string Detail { get; init; }
}

public sealed class ValidationQuotationLineItem
{
    /// <summary>"Room" or "Consumable" — matches <c>QuotationLineItemType</c>.</summary>
    public required string ItemType { get; init; }
    public required string ItemName { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
    public Guid? RoomId { get; init; }
    public Guid? ConsumableId { get; init; }
}

public sealed class ValidationQuotation
{
    public required decimal RoomFee { get; init; }
    public required decimal ConsumableCost { get; init; }
    public required decimal Total { get; init; }
    public required IReadOnlyList<ValidationQuotationLineItem> LineItems { get; init; }
}

/// <summary>S4 output contract: <c>{ valid, results[], quotation, failures[], revisionNote }</c>.</summary>
public sealed class ValidationResponse
{
    public required bool Valid { get; init; }
    public required IReadOnlyList<ValidationRuleResult> Results { get; init; }
    public required ValidationQuotation Quotation { get; init; }
    public required IReadOnlyList<string> Failures { get; init; }
    public string? RevisionNote { get; init; }
}
