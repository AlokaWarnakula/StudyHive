using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Data.Configurations;

public class StudentProfileConfiguration : IEntityTypeConfiguration<StudentProfile>
{
    public void Configure(EntityTypeBuilder<StudentProfile> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.StudentNumber).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.StudentNumber).IsUnique();
        b.Property(x => x.Department).HasMaxLength(80).IsRequired();
        b.Property(x => x.MaxBookingsPerWeek).HasDefaultValue(3);
        b.Property(x => x.PenaltyPoints).HasDefaultValue(0);
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<StudentProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint("ck_student_profiles_year", "year_of_study BETWEEN 1 AND 5");
            tb.HasCheckConstraint("ck_student_profiles_max_bookings", "max_bookings_per_week > 0");
            tb.HasCheckConstraint("ck_student_profiles_penalty_points", "penalty_points >= 0");
        });
    }
}

public class BookingRequestConfiguration : IEntityTypeConfiguration<BookingRequest>
{
    public void Configure(EntityTypeBuilder<BookingRequest> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Objective).IsRequired();
        b.Property(x => x.Budget).HasColumnType("numeric(12,2)");
        b.Property(x => x.SessionsRequired).HasDefaultValue(1);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).HasDefaultValue(BookingRequestStatus.Draft);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.StudentId, x.CreatedAt }).HasDatabaseName("ix_br_student").IsDescending(false, true);
        b.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_br_status").IsDescending(false, true);

        b.HasOne(x => x.Student)
            .WithMany(x => x.BookingRequests)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb =>
        {
            tb.HasCheckConstraint(
                "ck_booking_requests_status",
                "status IN ('Draft','Submitted','Processing','PendingApproval','Approved','Rejected','RevisionRequested','Completed','Cancelled','Failed')");
            tb.HasCheckConstraint("ck_booking_requests_group_size", "group_size BETWEEN 1 AND 50");
            tb.HasCheckConstraint("ck_booking_requests_sessions", "sessions_required BETWEEN 1 AND 7");
            tb.HasCheckConstraint("ck_booking_requests_duration", "session_duration_minutes BETWEEN 30 AND 480");
            tb.HasCheckConstraint("ck_booking_requests_budget", "budget > 0");
            tb.HasCheckConstraint("chk_date_order", "preferred_date_to >= preferred_date_from");
            tb.HasCheckConstraint("chk_time_order", "preferred_time_to > preferred_time_from");
        });
    }
}

public class BookingRequestItemConfiguration : IEntityTypeConfiguration<BookingRequestItem>
{
    public void Configure(EntityTypeBuilder<BookingRequestItem> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.BookingRequestId, x.ConsumableId }).IsUnique().HasDatabaseName("uq_bri");

        b.HasOne(x => x.BookingRequest)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.BookingRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Consumable)
            .WithMany(x => x.RequestedIn)
            .HasForeignKey(x => x.ConsumableId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb => tb.HasCheckConstraint("ck_booking_request_items_quantity", "quantity > 0"));
    }
}

public class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowExecution> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Objective).IsRequired();
        b.Property(x => x.PlanJson).HasColumnType("jsonb");
        b.Property(x => x.CurrentStep).HasDefaultValue(0);
        b.Property(x => x.Attempt).HasDefaultValue(1);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).HasDefaultValue(WorkflowStatus.Started);
        b.Property(x => x.ErrorCode).HasMaxLength(50);
        b.Property(x => x.StartedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => x.BookingRequestId)
            .IsUnique()
            .HasDatabaseName("ux_wf_active")
            .HasFilter("status IN ('Started','InProgress','PendingApproval')");

        b.HasOne(x => x.BookingRequest)
            .WithMany()
            .HasForeignKey(x => x.BookingRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(tb => tb.HasCheckConstraint(
            "ck_workflow_executions_status",
            "status IN ('Started','InProgress','PendingApproval','Approved','Rejected','Failed','Completed')"));
    }
}

public class WorkflowStepLogConfiguration : IEntityTypeConfiguration<WorkflowStepLog>
{
    public void Configure(EntityTypeBuilder<WorkflowStepLog> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Attempt).HasDefaultValue(1);
        b.Property(x => x.AgentName).HasMaxLength(40).IsRequired();
        b.Property(x => x.ToolName).HasMaxLength(60);
        b.Property(x => x.InputJson).HasColumnType("jsonb");
        b.Property(x => x.OutputJson).HasColumnType("jsonb");
        b.Property(x => x.ValidationResult).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        b.HasIndex(x => new { x.WorkflowExecutionId, x.StepNumber, x.Attempt }).IsUnique().HasDatabaseName("uq_step");
        b.HasIndex(x => new { x.WorkflowExecutionId, x.StepNumber }).HasDatabaseName("ix_step_wf");

        b.HasOne(x => x.WorkflowExecution)
            .WithMany(x => x.StepLogs)
            .HasForeignKey(x => x.WorkflowExecutionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.ToTable(tb => tb.HasCheckConstraint(
            "ck_workflow_step_logs_validation_result",
            "validation_result IN ('Pass','Fail','Warning')"));
    }
}
