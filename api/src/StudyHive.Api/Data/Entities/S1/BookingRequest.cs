namespace StudyHive.Api.Data.Entities;

public class BookingRequest
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public required string Objective { get; set; }
    public int GroupSize { get; set; }
    public DateOnly PreferredDateFrom { get; set; }
    public DateOnly PreferredDateTo { get; set; }
    public TimeOnly PreferredTimeFrom { get; set; }
    public TimeOnly PreferredTimeTo { get; set; }
    public int SessionsRequired { get; set; } = 1;
    public int SessionDurationMinutes { get; set; }
    public decimal Budget { get; set; }
    public string? Notes { get; set; }
    public BookingRequestStatus Status { get; set; } = BookingRequestStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public StudentProfile Student { get; set; } = null!;
    public ICollection<BookingRequestItem> Items { get; set; } = new List<BookingRequestItem>();
    public ICollection<BookingRequestEquipment> RequiredEquipment { get; set; } = new List<BookingRequestEquipment>();
}
