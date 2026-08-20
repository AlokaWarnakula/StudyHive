using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Data;

/// <summary>
/// The single shared DbContext for all four business components (see
/// DOCS/StudyHive_Master_Project_Relay_Plan.html §10 for the locked schema). Entity
/// configuration lives in Data/Configurations and is applied from this assembly —
/// add new entities there, not with inline OnModelCreating code.
/// </summary>
public class StudyHiveDbContext(DbContextOptions<StudyHiveDbContext> options) : DbContext(options)
{
    // Shared
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // S1 — Requests & Workflow
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();
    public DbSet<BookingRequestItem> BookingRequestItems => Set<BookingRequestItem>();
    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();
    public DbSet<WorkflowStepLog> WorkflowStepLogs => Set<WorkflowStepLog>();

    // S2 — Rooms & Availability
    public DbSet<StudyRoom> StudyRooms => Set<StudyRoom>();
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<RoomEquipment> RoomEquipment => Set<RoomEquipment>();
    public DbSet<BookingRequestEquipment> BookingRequestEquipment => Set<BookingRequestEquipment>();
    public DbSet<MaintenanceWindow> MaintenanceWindows => Set<MaintenanceWindow>();
    public DbSet<RoomBooking> RoomBookings => Set<RoomBooking>();

    // S3 — Consumables & Stock
    public DbSet<Consumable> Consumables => Set<Consumable>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ConsumableSupplier> ConsumableSuppliers => Set<ConsumableSupplier>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();

    // S4 — Costing & Approval
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLineItem> QuotationLineItems => Set<QuotationLineItem>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudyHiveDbContext).Assembly);
    }
}
