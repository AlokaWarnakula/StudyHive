namespace StudyHive.Api.Data.Entities;

public class Consumable
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }

    // Generated always as (stock_quantity - reserved_quantity) stored.
    public int AvailableQuantity { get; private set; }

    public int MinStockLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ConsumableSupplier> Suppliers { get; set; } = new List<ConsumableSupplier>();
    public ICollection<BookingRequestItem> RequestedIn { get; set; } = new List<BookingRequestItem>();
    public ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
    public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
}
