using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Data.Configurations;

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).HasDefaultValue(1);
        b.Property(x => x.RoomFee).HasColumnType("numeric(12,2)");
        b.Property(x => x.ConsumableCost).HasColumnType("numeric(12,2)");
        b.Property(x => x.TotalAmount)
            .HasColumnType("numeric(12,2)")
            .HasComputedColumnSql("room_fee + consumable_cost", stored: true)
            .ValueGeneratedOnAddOrUpdate();
        b.Property(x => x.BudgetSnapshot).HasColumnType("numeric(12,2)");
        b.Property(x => x.WithinBudget)
            .HasComputedColumnSql("room_fee + consumable_cost <= budget_snapshot", stored: true)
            .ValueGeneratedOnAddOrUpdate();
        b.Property(x => x.Currency).HasColumnType("char(3)").HasDefaultValue("LKR");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(QuotationStatus.Draft);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.BookingRequestId, x.Version }).IsUnique().HasDatabaseName("uq_quote_version");
        b.HasIndex(x => x.BookingRequestId)
            .IsUnique()
            .HasDatabaseName("ux_quote_active")
            .HasFilter("status IN ('Proposed','Approved')");

        b.HasOne(x => x.BookingRequest)
            .WithMany()
            .HasForeignKey(x => x.BookingRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint("ck_quotations_room_fee", "room_fee >= 0");
            tb.HasCheckConstraint("ck_quotations_consumable_cost", "consumable_cost >= 0");
            tb.HasCheckConstraint(
                "ck_quotations_status",
                "status IN ('Draft','Proposed','Approved','Rejected','Superseded')");
        });
    }
}

public class QuotationLineItemConfiguration : IEntityTypeConfiguration<QuotationLineItem>
{
    public void Configure(EntityTypeBuilder<QuotationLineItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ItemName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Quantity).HasColumnType("numeric(10,2)");
        b.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.LineTotal)
            .HasColumnType("numeric(12,2)")
            .HasComputedColumnSql("quantity * unit_price", stored: true)
            .ValueGeneratedOnAddOrUpdate();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.QuotationId).HasDatabaseName("ix_qli_quote");

        b.HasOne(x => x.Quotation)
            .WithMany(x => x.LineItems)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.RoomBooking)
            .WithMany(x => x.QuotationLineItems)
            .HasForeignKey(x => x.RoomBookingId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Consumable)
            .WithMany()
            .HasForeignKey(x => x.ConsumableId)
            .OnDelete(DeleteBehavior.SetNull);

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint("ck_quotation_line_items_item_type", "item_type IN ('Room','Consumable')");
            tb.HasCheckConstraint("ck_quotation_line_items_quantity", "quantity > 0");
            tb.HasCheckConstraint("ck_quotation_line_items_unit_price", "unit_price >= 0");
            tb.HasCheckConstraint(
                "chk_line_shape",
                "(item_type = 'Room' AND room_booking_id IS NOT NULL AND consumable_id IS NULL) OR " +
                "(item_type = 'Consumable' AND consumable_id IS NOT NULL AND room_booking_id IS NULL)");
        });
    }
}

public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.DecidedByRole).HasMaxLength(20).IsRequired();
        b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.DecidedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.QuotationId, x.DecidedAt }).HasDatabaseName("ix_ad_quote").IsDescending(false, true);

        b.HasOne(x => x.Quotation)
            .WithMany(x => x.ApprovalDecisions)
            .HasForeignKey(x => x.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.DecidedByUser)
            .WithMany()
            .HasForeignKey(x => x.DecidedBy)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb => tb.HasCheckConstraint(
            "ck_approval_decisions_decision",
            "decision IN ('Approved','Rejected','RevisionRequested')"));
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(60).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        b.Property(x => x.Details).HasColumnType("jsonb");
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("ix_audit_entity");
        b.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_audit_user").IsDescending(false, true);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
