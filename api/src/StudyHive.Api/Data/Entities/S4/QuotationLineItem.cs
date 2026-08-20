namespace StudyHive.Api.Data.Entities;

public class QuotationLineItem
{
    public Guid Id { get; set; }
    public Guid QuotationId { get; set; }
    public QuotationLineItemType ItemType { get; set; }
    public Guid? RoomBookingId { get; set; }
    public Guid? ConsumableId { get; set; }
    public required string ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Generated always as (quantity * unit_price) stored.
    public decimal LineTotal { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Quotation Quotation { get; set; } = null!;
    public RoomBooking? RoomBooking { get; set; }
    public Consumable? Consumable { get; set; }
}
