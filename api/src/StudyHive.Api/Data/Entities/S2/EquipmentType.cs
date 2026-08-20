namespace StudyHive.Api.Data.Entities;

public class EquipmentType
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RoomEquipment> Rooms { get; set; } = new List<RoomEquipment>();
    public ICollection<BookingRequestEquipment> RequestedBy { get; set; } = new List<BookingRequestEquipment>();
}
