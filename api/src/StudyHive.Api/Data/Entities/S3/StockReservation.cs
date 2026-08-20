namespace StudyHive.Api.Data.Entities;

public class StockReservation
{
    public Guid Id { get; set; }
    public Guid BookingRequestItemId { get; set; }
    public Guid ConsumableId { get; set; }
    public int Quantity { get; set; }
    public StockReservationStatus Status { get; set; } = StockReservationStatus.Pending;
    public DateTimeOffset? ReservedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public BookingRequestItem BookingRequestItem { get; set; } = null!;
    public Consumable Consumable { get; set; } = null!;
    public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
}
