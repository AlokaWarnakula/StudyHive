using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

/// <summary>
/// Class fixture for the S3 (consumables and stock) tests. It owns the test host, and every role
/// logs in once per test class — login is rate-limited to 30/minute per path, and xUnit builds a
/// new test-class instance per test, so logging in from the test class would trip it. Every row
/// the tests create is recorded so <see cref="DisposeAsync"/> can remove it in foreign-key order:
/// stock_transactions -> stock_reservations -> users' requests (items cascade) -> consumable links
/// -> consumables / suppliers -> users.
/// </summary>
public sealed class S3Fixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> factory = new();

    public IServiceProvider Services => factory.Services;

    public readonly List<Guid> UserIds = [];
    public readonly List<Guid> ConsumableIds = [];
    public readonly List<Guid> SupplierIds = [];

    public string StoreOfficerToken { get; private set; } = "";
    public Guid StoreOfficerId { get; private set; }
    public string LibrarianToken { get; private set; } = "";
    public string AdminToken { get; private set; } = "";
    public string StudentToken { get; private set; } = "";
    public Guid StudentProfileId { get; private set; }

    public async Task InitializeAsync()
    {
        var client = factory.CreateClient();

        var (storeOfficerId, _, storeOfficerToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.StoreOfficer);
        UserIds.Add(storeOfficerId);
        StoreOfficerId = storeOfficerId;
        StoreOfficerToken = storeOfficerToken;

        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        UserIds.Add(librarianId);
        LibrarianToken = librarianToken;

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        UserIds.Add(adminId);
        AdminToken = adminToken;

        var (student, _, studentToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        UserIds.Add(student.Id);
        StudentToken = studentToken;
        StudentProfileId = (await TestSupport.CreateStudentProfileAsync(client, studentToken)).Id;
    }

    public HttpClient Client(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    /// <summary>Inserts a consumable directly (no ledger row), so a test controls its exact counters.</summary>
    public async Task<Guid> SeedConsumableAsync(int stock, int minStockLevel = 0, decimal unitPrice = 10m)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        var consumable = new Consumable
        {
            Id = Guid.NewGuid(),
            Name = UniqueName("S3 test item"),
            Unit = "pcs",
            UnitPrice = unitPrice,
            StockQuantity = stock,
            MinStockLevel = minStockLevel,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Consumables.Add(consumable);
        await db.SaveChangesAsync();
        ConsumableIds.Add(consumable.Id);
        return consumable.Id;
    }

    /// <summary>A booking request line for the class's student — the thing a reservation reserves.</summary>
    public async Task<Guid> SeedBookingRequestItemAsync(Guid consumableId, int quantity)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7));
        var request = new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = StudentProfileId,
            Objective = "S3 stock test",
            GroupSize = 2,
            PreferredDateFrom = today,
            PreferredDateTo = today,
            PreferredTimeFrom = new TimeOnly(9, 0),
            PreferredTimeTo = new TimeOnly(12, 0),
            SessionsRequired = 1,
            SessionDurationMinutes = 60,
            Budget = 1000,
            Status = BookingRequestStatus.PendingApproval,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var item = new BookingRequestItem
        {
            Id = Guid.NewGuid(),
            BookingRequestId = request.Id,
            ConsumableId = consumableId,
            Quantity = quantity,
            CreatedAt = now,
        };
        db.BookingRequests.Add(request);
        db.BookingRequestItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    public async Task<Consumable> ReadConsumableAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        return await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    public async Task DisposeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            await db.StockTransactions.Where(t => ConsumableIds.Contains(t.ConsumableId) || UserIds.Contains(t.CreatedBy)).ExecuteDeleteAsync();
            await db.StockReservations.Where(r => ConsumableIds.Contains(r.ConsumableId)).ExecuteDeleteAsync();
        }

        await TestSupport.CleanupAsync(factory, UserIds.ToArray());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            await db.ConsumableSuppliers.Where(l => ConsumableIds.Contains(l.ConsumableId) || SupplierIds.Contains(l.SupplierId)).ExecuteDeleteAsync();
            await db.Consumables.Where(c => ConsumableIds.Contains(c.Id)).ExecuteDeleteAsync();
            await db.Suppliers.Where(s => SupplierIds.Contains(s.Id)).ExecuteDeleteAsync();
        }

        await factory.DisposeAsync();
    }
}
