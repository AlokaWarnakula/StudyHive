namespace StudyHive.Api.Data.Entities;

public class Supplier
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string ContactEmail { get; set; }
    public required string Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ConsumableSupplier> Consumables { get; set; } = new List<ConsumableSupplier>();
}
