namespace StudyHive.Api.Contracts;

/// <summary>S2 Scheduling Agent input assembled from trusted request and database data.</summary>
public sealed class SchedulingRequest
{
    public required int GroupSize { get; init; }
    public required DateOnly PreferredDateFrom { get; init; }
    public required DateOnly PreferredDateTo { get; init; }
    public required TimeOnly PreferredTimeFrom { get; init; }
    public required TimeOnly PreferredTimeTo { get; init; }
    public required int SessionsRequired { get; init; }
    public required int SessionDurationMinutes { get; init; }
    public required IReadOnlyList<Guid> RequiredEquipmentTypeIds { get; init; }
    public required IReadOnlyList<SchedulingRoom> Rooms { get; init; }
}

public sealed class SchedulingRoom
{
    public required Guid RoomId { get; init; }
    public required string RoomName { get; init; }
    public required int Capacity { get; init; }
    public required decimal HourlyRate { get; init; }
    public required bool IsActive { get; init; }
    public required IReadOnlyList<Guid> EquipmentTypeIds { get; init; }
    public required IReadOnlyList<SchedulingTimeBlock> Bookings { get; init; }
    public required IReadOnlyList<SchedulingTimeBlock> MaintenanceWindows { get; init; }
}

public sealed class SchedulingTimeBlock
{
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset EndsAt { get; init; }
}

/// <summary>S2 output contract required by the shared workflow.</summary>
public sealed class SchedulingResponse
{
    public required IReadOnlyList<SchedulingSlot> Slots { get; init; }
    public required IReadOnlyList<string> Conflicts { get; init; }
}

public sealed class SchedulingSlot
{
    public required Guid RoomId { get; init; }
    public required string RoomName { get; init; }
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset EndsAt { get; init; }
    public required decimal HourlyRate { get; init; }
}
