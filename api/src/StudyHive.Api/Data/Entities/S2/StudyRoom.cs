namespace StudyHive.Api.Data.Entities;

public class StudyRoom
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Building { get; set; }
    public int Floor { get; set; }
    public int Capacity { get; set; }
    public decimal HourlyRate { get; set; }
    public required string QrCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RoomEquipment> Equipment { get; set; } = new List<RoomEquipment>();
    public ICollection<MaintenanceWindow> MaintenanceWindows { get; set; } = new List<MaintenanceWindow>();
    public ICollection<RoomBooking> Bookings { get; set; } = new List<RoomBooking>();
}
