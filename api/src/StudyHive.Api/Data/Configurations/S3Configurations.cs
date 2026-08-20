using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Data.Configurations;

public class ConsumableConfiguration : IEntityTypeConfiguration<Consumable>
{
    public void Configure(EntityTypeBuilder<Consumable> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        b.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.StockQuantity).HasDefaultValue(0);
        b.Property(x => x.ReservedQuantity).HasDefaultValue(0);
        b.Property(x => x.AvailableQuantity)
            .HasComputedColumnSql("stock_quantity - reserved_quantity", stored: true)
            .ValueGeneratedOnAddOrUpdate();
        b.Property(x => x.MinStockLevel).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.IsActive)
            .HasDatabaseName("ix_cons_low")
            .HasFilter("stock_quantity <= min_stock_level");

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint("ck_consumables_unit_price", "unit_price >= 0");
            tb.HasCheckConstraint("ck_consumables_stock_quantity", "stock_quantity >= 0");
            tb.HasCheckConstraint("ck_consumables_reserved_quantity", "reserved_quantity >= 0");
            tb.HasCheckConstraint("ck_consumables_min_stock_level", "min_stock_level >= 0");
            tb.HasCheckConstraint("chk_never_oversold", "reserved_quantity <= stock_quantity");
        });
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.ContactEmail).HasColumnType("citext").IsRequired();
        b.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}

public class ConsumableSupplierConfiguration : IEntityTypeConfiguration<ConsumableSupplier>
{
    public void Configure(EntityTypeBuilder<ConsumableSupplier> b)
    {
        b.HasKey(x => new { x.ConsumableId, x.SupplierId });
        b.Property(x => x.SupplyPrice).HasColumnType("numeric(12,2)");
        b.Property(x => x.IsPreferred).HasDefaultValue(false);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.ConsumableId)
            .IsUnique()
            .HasDatabaseName("ux_preferred")
            .HasFilter("is_preferred");

        b.HasOne(x => x.Consumable)
            .WithMany(x => x.Suppliers)
            .HasForeignKey(x => x.ConsumableId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Supplier)
            .WithMany(x => x.Consumables)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb => tb.HasCheckConstraint("ck_consumable_suppliers_supply_price", "supply_price >= 0"));
    }
}

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(StockReservationStatus.Pending);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.BookingRequestItemId).IsUnique();
        b.HasIndex(x => new { x.ConsumableId, x.Status }).HasDatabaseName("ix_res_cons");

        b.HasOne(x => x.BookingRequestItem)
            .WithOne(x => x.StockReservation)
            .HasForeignKey<StockReservation>(x => x.BookingRequestItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Consumable)
            .WithMany(x => x.Reservations)
            .HasForeignKey(x => x.ConsumableId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint("ck_stock_reservations_quantity", "quantity > 0");
            tb.HasCheckConstraint(
                "ck_stock_reservations_status",
                "status IN ('Pending','Reserved','Released','Used')");
        });
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.ConsumableId, x.CreatedAt }).HasDatabaseName("ix_tx_cons").IsDescending(false, true);

        b.HasOne(x => x.Consumable)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.ConsumableId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.BookingRequest)
            .WithMany()
            .HasForeignKey(x => x.BookingRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.StockReservation)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.StockReservationId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint(
                "ck_stock_transactions_type",
                "transaction_type IN ('StockIn','StockOut','Reserve','Release','Adjust')");
            tb.HasCheckConstraint("ck_stock_transactions_quantity", "quantity <> 0");
        });
    }
}

public class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.ToEmail).HasColumnType("citext").IsRequired();
        b.Property(x => x.Template).HasMaxLength(50).IsRequired();
        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(EmailNotificationStatus.Queued);
        b.Property(x => x.AttemptCount).HasDefaultValue(0);
        b.Property(x => x.MaxAttempts).HasDefaultValue(3);
        b.Property(x => x.ProviderMessageId).HasMaxLength(200);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.Status, x.NextAttemptAt }).HasDatabaseName("ix_email_due");

        b.HasOne(x => x.BookingRequest)
            .WithMany()
            .HasForeignKey(x => x.BookingRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        b.ToTable(tb => tb.HasCheckConstraint(
            "ck_email_notifications_status",
            "status IN ('Queued','Sent','Failed','DeadLettered')"));
    }
}
