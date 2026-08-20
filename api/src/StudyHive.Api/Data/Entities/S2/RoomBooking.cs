namespace StudyHive.Api.Data.Entities;

// Note: the generated `slot tstzrange` column and the no_double_booking EXCLUDE USING gist
// constraint are added via raw SQL in the migration (see StudyHive_Master_Project_Relay_Plan.html
// §10) and are intentionally not mapped here — only starts_at/ends_at are model-managed.
public class RoomBooking
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Guid BookingRequestId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public RoomBookingStatus Status { get; set; } = RoomBookingStatus.Confirmed;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public StudyRoom Room { get; set; } = null!;
    public BookingRequest BookingRequest { get; set; } = null!;
    public ICollection<QuotationLineItem> QuotationLineItems { get; set; } = new List<QuotationLineItem>();
}
