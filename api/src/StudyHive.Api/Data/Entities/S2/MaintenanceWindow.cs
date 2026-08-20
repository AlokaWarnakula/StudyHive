namespace StudyHive.Api.Data.Entities;

// Note: the generated `window tstzrange` column and its GiST index are added via raw SQL
// in the migration (see StudyHive_Master_Project_Relay_Plan.html §10) and are intentionally
// not mapped here — EF Core cannot express GENERATED ALWAYS AS ... tstzrange columns used by
// GiST indexes/exclusion constraints, only starts_at/ends_at are model-managed.
public class MaintenanceWindow
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public StudyRoom Room { get; set; } = null!;
}
