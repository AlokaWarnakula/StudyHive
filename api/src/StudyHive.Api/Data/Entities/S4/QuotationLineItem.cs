namespace StudyHive.Api.Data.Entities;

public class QuotationLineItem
{
    public Guid Id { get; set; }
    public Guid QuotationId { get; set; }
    public QuotationLineItemType ItemType { get; set; }

    // Room lines: the proposed room is known at quoting time; the booking only exists once the
    // approval transaction creates it and links it here (chk_line_shape).
    public Guid? RoomId { get; set; }
    public Guid? RoomBookingId { get; set; }
    public Guid? ConsumableId { get; set; }
    public required string ItemName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    // Generated always as (quantity * unit_price) stored.
    public decimal LineTotal { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Quotation Quotation { get; set; } = null!;
    public StudyRoom? Room { get; set; }
    public RoomBooking? RoomBooking { get; set; }
    public Consumable? Consumable { get; set; }
}
