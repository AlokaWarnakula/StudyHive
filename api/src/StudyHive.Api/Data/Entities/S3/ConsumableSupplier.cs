namespace StudyHive.Api.Data.Entities;

public class ConsumableSupplier
{
    public Guid ConsumableId { get; set; }
    public Guid SupplierId { get; set; }
    public decimal SupplyPrice { get; set; }
    public bool IsPreferred { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Consumable Consumable { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
}
