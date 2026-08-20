namespace StudyHive.Api.Data.Entities;

public class BookingRequestEquipment
{
    public Guid BookingRequestId { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public int QuantityRequired { get; set; } = 1;

    public BookingRequest BookingRequest { get; set; } = null!;
    public EquipmentType EquipmentType { get; set; } = null!;
}
