namespace StudyHive.Api.Data.Entities;

public class StudentProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string StudentNumber { get; set; }
    public required string Department { get; set; }
    public int YearOfStudy { get; set; }
    public int MaxBookingsPerWeek { get; set; } = 3;
    public int PenaltyPoints { get; set; }
    public DateOnly? SuspendedUntil { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();
}
