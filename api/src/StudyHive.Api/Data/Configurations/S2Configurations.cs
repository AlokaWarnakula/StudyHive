using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Data.Configurations;

public class StudyRoomConfiguration : IEntityTypeConfiguration<StudyRoom>
{
    public void Configure(EntityTypeBuilder<StudyRoom> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Building).HasMaxLength(60).IsRequired();
        b.Property(x => x.HourlyRate).HasColumnType("numeric(12,2)");
        b.Property(x => x.QrCode).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.QrCode).IsUnique();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.IsActive, x.Capacity }).HasDatabaseName("ix_rooms_search");

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint("ck_study_rooms_capacity", "capacity > 0");
            tb.HasCheckConstraint("ck_study_rooms_hourly_rate", "hourly_rate >= 0");
        });
    }
}

public class EquipmentTypeConfiguration : IEntityTypeConfiguration<EquipmentType>
{
    public void Configure(EntityTypeBuilder<EquipmentType> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Category).HasMaxLength(40).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
    }
}

public class RoomEquipmentConfiguration : IEntityTypeConfiguration<RoomEquipment>
{
    public void Configure(EntityTypeBuilder<RoomEquipment> b)
    {
        b.HasKey(x => new { x.RoomId, x.EquipmentTypeId });
        b.Property(x => x.Quantity).HasDefaultValue(1);
        b.Property(x => x.InstalledAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Room)
            .WithMany(x => x.Equipment)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.EquipmentType)
            .WithMany(x => x.Rooms)
            .HasForeignKey(x => x.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb => tb.HasCheckConstraint("ck_room_equipment_quantity", "quantity > 0"));
    }
}

public class BookingRequestEquipmentConfiguration : IEntityTypeConfiguration<BookingRequestEquipment>
{
    public void Configure(EntityTypeBuilder<BookingRequestEquipment> b)
    {
        b.HasKey(x => new { x.BookingRequestId, x.EquipmentTypeId });
        b.Property(x => x.QuantityRequired).HasDefaultValue(1);

        b.HasOne(x => x.BookingRequest)
            .WithMany(x => x.RequiredEquipment)
            .HasForeignKey(x => x.BookingRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.EquipmentType)
            .WithMany(x => x.RequestedBy)
            .HasForeignKey(x => x.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb => tb.HasCheckConstraint("ck_booking_request_equipment_quantity", "quantity_required > 0"));
    }
}

public class MaintenanceWindowConfiguration : IEntityTypeConfiguration<MaintenanceWindow>
{
    public void Configure(EntityTypeBuilder<MaintenanceWindow> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).IsRequired();
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasOne(x => x.Room)
            .WithMany(x => x.MaintenanceWindows)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        b.ToTable(tb => tb.HasCheckConstraint("chk_mw_order", "ends_at > starts_at"));

        // The generated `window` tstzrange column and its GiST index (ix_mw_room) are added
        // by raw SQL in the migration — see StudyHive_Master_Project_Relay_Plan.html §10.
    }
}

public class RoomBookingConfiguration : IEntityTypeConfiguration<RoomBooking>
{
    public void Configure(EntityTypeBuilder<RoomBooking> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(RoomBookingStatus.Confirmed);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.BookingRequestId).HasDatabaseName("ix_rb_request");
        b.HasIndex(x => new { x.RoomId, x.StartsAt }).HasDatabaseName("ix_rb_room");

        b.HasOne(x => x.Room)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.BookingRequest)
            .WithMany()
            .HasForeignKey(x => x.BookingRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint("chk_rb_order", "ends_at > starts_at");
            tb.HasCheckConstraint(
                "ck_room_bookings_status",
                "status IN ('Confirmed','Cancelled','Completed','NoShow')");
        });

        // The generated `slot` tstzrange column and the no_double_booking EXCLUDE USING gist
        // constraint are added by raw SQL in the migration — see
        // StudyHive_Master_Project_Relay_Plan.html §10.
    }
}
