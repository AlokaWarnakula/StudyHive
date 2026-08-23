namespace StudyHive.Api.Contracts;

/// <summary>
/// The Planner Agent's typed contract (DOCS §11 "The four agents" table). The API always computes
/// eligibility itself and passes the verdict down — the agent never touches the database and never
/// re-derives eligibility from <see cref="Objective"/> free text, so a hostile objective cannot talk
/// its way past a real ineligibility.
/// </summary>
public sealed class PlannerRequest
{
    public required string Objective { get; init; }
    public required Guid StudentId { get; init; }
    public required int GroupSize { get; init; }
    public required DateOnly PreferredDateFrom { get; init; }
    public required DateOnly PreferredDateTo { get; init; }
    public required TimeOnly PreferredTimeFrom { get; init; }
    public required TimeOnly PreferredTimeTo { get; init; }
    public required int SessionsRequired { get; init; }
    public required int SessionDurationMinutes { get; init; }
    public required decimal Budget { get; init; }
    public required bool StudentEligible { get; init; }
    public required IReadOnlyList<string> EligibilityReasons { get; init; }
    public required IReadOnlyList<PlannerRequestItem> RequestedItems { get; init; }
}

public sealed class PlannerRequestItem
{
    public required Guid ConsumableId { get; init; }
    public required int Quantity { get; init; }
}

public sealed class PlannerStep
{
    public required int N { get; init; }
    public required string Agent { get; init; }
    public required string Action { get; init; }
    public required Dictionary<string, object?> Params { get; init; }
}

public sealed class PlannerResponse
{
    public required Guid PlanId { get; init; }
    public required bool Eligible { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public required IReadOnlyList<PlannerStep> Steps { get; init; }
}
