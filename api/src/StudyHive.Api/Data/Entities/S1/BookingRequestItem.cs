namespace StudyHive.Api.Data.Entities;

public class BookingRequestItem
{
    public Guid Id { get; set; }
    public Guid BookingRequestId { get; set; }
    public Guid ConsumableId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public BookingRequest BookingRequest { get; set; } = null!;
    public Consumable Consumable { get; set; } = null!;
    public StockReservation? StockReservation { get; set; }
}
