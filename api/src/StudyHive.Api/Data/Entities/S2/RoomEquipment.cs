namespace StudyHive.Api.Data.Entities;

public class RoomEquipment
{
    public Guid RoomId { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTimeOffset InstalledAt { get; set; }

    public StudyRoom Room { get; set; } = null!;
    public EquipmentType EquipmentType { get; set; } = null!;
}
