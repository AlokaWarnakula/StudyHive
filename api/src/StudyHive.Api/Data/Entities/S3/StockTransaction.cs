namespace StudyHive.Api.Data.Entities;

public class StockTransaction
{
    public Guid Id { get; set; }
    public Guid ConsumableId { get; set; }
    public StockTransactionType TransactionType { get; set; }
    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }
    public Guid? BookingRequestId { get; set; }
    public Guid? StockReservationId { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Consumable Consumable { get; set; } = null!;
    public BookingRequest? BookingRequest { get; set; }
    public StockReservation? StockReservation { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
