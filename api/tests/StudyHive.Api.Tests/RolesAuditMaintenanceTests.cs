using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Controllers.Approvals;
using StudyHive.Api.Controllers.Rooms;
using StudyHive.Api.Controllers.Store;
using StudyHive.Api.Controllers.StudentProfiles;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>
/// PLAN.md B1: one role table (CW-05), every staff write audited (CW-04), maintenance edit/cancel
/// and the overlap rule (CW-06), report fixes (CW-07, C-15), the students list (CW-08) and the
/// weekly allowance (C-16). Uses the S3 fixture for its four signed-in roles; rooms, equipment and
/// maintenance windows a test creates are removed by this class.
/// </summary>
public class RolesAuditMaintenanceTests(S3Fixture s3) : IClassFixture<S3Fixture>, IAsyncLifetime
{
    private readonly List<Guid> _roomIds = [];
    private readonly List<Guid> _equipmentIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = s3.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        await db.MaintenanceWindows.Where(w => _roomIds.Contains(w.RoomId)).ExecuteDeleteAsync();
        await db.RoomBookings.Where(b => _roomIds.Contains(b.RoomId)).ExecuteDeleteAsync();
        await db.RoomEquipment.Where(e => _roomIds.Contains(e.RoomId) || _equipmentIds.Contains(e.EquipmentTypeId)).ExecuteDeleteAsync();
        await db.StudyRooms.Where(r => _roomIds.Contains(r.Id)).ExecuteDeleteAsync();
        await db.EquipmentTypes.Where(e => _equipmentIds.Contains(e.Id)).ExecuteDeleteAsync();
    }

    private string Token(string role) => role switch
    {
        "Admin" => s3.AdminToken,
        "Librarian" => s3.LibrarianToken,
        "StoreOfficer" => s3.StoreOfficerToken,
        "Student" => s3.StudentToken,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    private async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(TestSupport.JsonOptions))!;

    private async Task<Guid> CreateRoomAsync()
    {
        var response = await s3.Client(s3.LibrarianToken).PostAsJsonAsync("/api/rooms", new CreateRoomRequest(
            S3Fixture.UniqueName("B1 room")[..30], "B1 block", 1, 6, 100m, $"b1-{Guid.NewGuid():N}"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var room = await Read<RoomResponse>(response);
        _roomIds.Add(room.Id);
        return room.Id;
    }

    /// <summary>A Confirmed booking of the fixture student's (PendingApproval) request in the room.</summary>
    private async Task<(Guid BookingId, Guid RequestId)> SeedConfirmedBookingAsync(Guid roomId, DateTimeOffset startsAt)
    {
        var consumableId = await s3.SeedConsumableAsync(stock: 10);
        var itemId = await s3.SeedBookingRequestItemAsync(consumableId, 1);
        using var scope = s3.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var requestId = await db.BookingRequestItems.Where(i => i.Id == itemId).Select(i => i.BookingRequestId).SingleAsync();
        var booking = new RoomBooking
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            BookingRequestId = requestId,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(2),
            Status = RoomBookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.RoomBookings.Add(booking);
        await db.SaveChangesAsync();
        return (booking.Id, requestId);
    }

    private async Task<List<string>> AuditActionsAsync(Guid entityId)
    {
        using var scope = s3.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        return await db.AuditLogs.AsNoTracking().Where(a => a.EntityId == entityId).OrderBy(a => a.CreatedAt).Select(a => a.Action).ToListAsync();
    }

    // ---- CW-05: one role table -------------------------------------------------------------

    [Theory]
    [InlineData("/api/maintenance-windows", "Admin", HttpStatusCode.OK)]
    [InlineData("/api/maintenance-windows", "Librarian", HttpStatusCode.OK)]
    [InlineData("/api/maintenance-windows", "StoreOfficer", HttpStatusCode.Forbidden)]
    [InlineData("/api/suppliers", "Admin", HttpStatusCode.OK)]
    [InlineData("/api/suppliers", "StoreOfficer", HttpStatusCode.OK)]
    [InlineData("/api/suppliers", "Librarian", HttpStatusCode.Forbidden)]
    [InlineData("/api/stock-reservations", "Admin", HttpStatusCode.OK)]
    [InlineData("/api/stock-reservations", "Librarian", HttpStatusCode.OK)]
    [InlineData("/api/stock-reservations", "StoreOfficer", HttpStatusCode.OK)]
    [InlineData("/api/stock-reservations", "Student", HttpStatusCode.Forbidden)]
    [InlineData("/api/stock-transactions", "Admin", HttpStatusCode.OK)]
    [InlineData("/api/stock-transactions", "StoreOfficer", HttpStatusCode.OK)]
    [InlineData("/api/stock-transactions", "Librarian", HttpStatusCode.Forbidden)]
    public async Task The_Role_Table_Allows_And_Forbids_Reads(string path, string role, HttpStatusCode expected)
    {
        var response = await s3.Client(Token(role)).GetAsync(path);

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Only_A_Librarian_Writes_Maintenance_Windows()
    {
        var roomId = await CreateRoomAsync();
        var body = new CreateMaintenanceWindowRequest(roomId, DateTimeOffset.UtcNow.AddDays(400), DateTimeOffset.UtcNow.AddDays(400).AddHours(2), "Admin attempt");

        (await s3.Client(s3.AdminToken).PostAsJsonAsync("/api/maintenance-windows", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await s3.Client(s3.LibrarianToken).PostAsJsonAsync("/api/maintenance-windows", body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Consumable_Put_Honours_IsActive_And_Keeps_It_When_Omitted()
    {
        var id = await s3.SeedConsumableAsync(stock: 5);
        var client = s3.Client(s3.StoreOfficerToken);
        object Body(bool? isActive) => new { name = S3Fixture.UniqueName("B1 put"), unit = "pcs", unitPrice = 5m, minStockLevel = 1, isActive };

        (await Read<ConsumableResponse>(await client.PutAsJsonAsync($"/api/consumables/{id}", Body(false)))).IsActive.Should().BeFalse();
        (await Read<ConsumableResponse>(await client.PutAsJsonAsync($"/api/consumables/{id}", Body(null)))).IsActive.Should().BeFalse("left out, the current value is kept");
        (await Read<ConsumableResponse>(await client.PutAsJsonAsync($"/api/consumables/{id}", Body(true)))).IsActive.Should().BeTrue();
    }

    // ---- CW-04: every listed staff write leaves an audit row -------------------------------

    [Fact]
    public async Task Room_Equipment_And_Maintenance_Writes_Are_Audited()
    {
        var librarian = s3.Client(s3.LibrarianToken);
        var roomId = await CreateRoomAsync();
        (await librarian.PutAsJsonAsync($"/api/rooms/{roomId}", new UpdateRoomRequest("B1 room renamed " + Guid.NewGuid().ToString("N")[..6], "B1 block", 2, 8, 120m, $"b1-{Guid.NewGuid():N}", true)))
            .EnsureSuccessStatusCode();

        var equipment = await Read<EquipmentTypeResponse>(await librarian.PostAsJsonAsync("/api/equipment", new CreateEquipmentTypeRequest(S3Fixture.UniqueName("B1 kit")[..30], "AV", null)));
        _equipmentIds.Add(equipment.Id);
        (await librarian.PutAsJsonAsync($"/api/equipment/{equipment.Id}", new UpdateEquipmentTypeRequest(equipment.Name, "AV", "updated", true))).EnsureSuccessStatusCode();
        (await librarian.PostAsJsonAsync($"/api/rooms/{roomId}/equipment", new AssignRoomEquipmentRequest(equipment.Id, 1))).EnsureSuccessStatusCode();
        (await librarian.DeleteAsync($"/api/rooms/{roomId}/equipment/{equipment.Id}")).EnsureSuccessStatusCode();

        var starts = DateTimeOffset.UtcNow.AddDays(400);
        var window = await Read<MaintenanceWindowResponse>(await librarian.PostAsJsonAsync("/api/maintenance-windows",
            new CreateMaintenanceWindowRequest(roomId, starts, starts.AddHours(1), "Projector repair")));
        (await librarian.DeleteAsync($"/api/maintenance-windows/{window.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await s3.Client(s3.AdminToken).DeleteAsync($"/api/rooms/{roomId}")).EnsureSuccessStatusCode();

        (await AuditActionsAsync(roomId)).Should().Equal(
            "RoomCreated", "RoomUpdated", "RoomEquipmentAdded", "RoomEquipmentRemoved", "RoomDeactivated");
        (await AuditActionsAsync(equipment.Id)).Should().Equal("EquipmentCreated", "EquipmentUpdated");
        (await AuditActionsAsync(window.Id)).Should().Equal("MaintenanceScheduled", "MaintenanceCancelled");

        using var scope = s3.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>().AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityId == roomId && a.Action == "RoomCreated");
        row.UserId.Should().NotBeNull("the signed-in librarian is recorded");
        row.EntityType.Should().Be("StudyRoom");
        row.Details.Should().Contain("B1 block");
    }

    [Fact]
    public async Task Store_Student_And_Booking_Writes_Are_Audited()
    {
        var store = s3.Client(s3.StoreOfficerToken);
        var consumable = await Read<ConsumableResponse>(await store.PostAsJsonAsync("/api/consumables",
            new { name = S3Fixture.UniqueName("B1 audited"), unit = "pcs", unitPrice = 2m, stockQuantity = 20, minStockLevel = 1 }));
        s3.ConsumableIds.Add(consumable.Id);
        (await store.PutAsJsonAsync($"/api/consumables/{consumable.Id}", new { name = consumable.Name, unit = "pcs", unitPrice = 3m, minStockLevel = 2 })).EnsureSuccessStatusCode();
        (await store.PostAsJsonAsync($"/api/consumables/{consumable.Id}/stock-in", new { quantity = 5, notes = "delivery" })).EnsureSuccessStatusCode();

        async Task<Guid> ReserveAsync()
        {
            var itemId = await s3.SeedBookingRequestItemAsync(consumable.Id, 2);
            var reserved = await store.PostAsJsonAsync("/api/stock-reservations", new { bookingRequestItemId = itemId });
            reserved.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await Read<StockReservationResponse>(reserved)).Id;
        }
        var issued = await ReserveAsync();
        (await store.PutAsync($"/api/stock-reservations/{issued}/use", null)).EnsureSuccessStatusCode();
        var released = await ReserveAsync();
        (await store.PutAsync($"/api/stock-reservations/{released}/release", null)).EnsureSuccessStatusCode();
        (await s3.Client(s3.AdminToken).DeleteAsync($"/api/consumables/{consumable.Id}")).EnsureSuccessStatusCode();

        var supplier = await Read<SupplierResponse>(await store.PostAsJsonAsync("/api/suppliers",
            new { name = S3Fixture.UniqueName("B1 supplier"), contactEmail = "b1@studyhive.test", phone = "0110000000" }));
        s3.SupplierIds.Add(supplier.Id);
        (await store.PutAsJsonAsync($"/api/suppliers/{supplier.Id}", new { name = supplier.Name, contactEmail = "b1@studyhive.test", phone = "0110000001", isActive = true })).EnsureSuccessStatusCode();

        var admin = s3.Client(s3.AdminToken);
        var profile = await Read<StudentProfileResponse>(await s3.Client(s3.LibrarianToken).GetAsync($"/api/student-profiles/{s3.StudentProfileId}"));
        (await admin.PutAsJsonAsync($"/api/student-profiles/{s3.StudentProfileId}", new
        {
            department = profile.Department, yearOfStudy = profile.YearOfStudy, maxBookingsPerWeek = profile.MaxBookingsPerWeek,
            penaltyPoints = profile.PenaltyPoints, suspendedUntil = profile.SuspendedUntil, isActive = profile.IsActive,
        })).EnsureSuccessStatusCode();

        var cancelItem = await s3.SeedBookingRequestItemAsync(consumable.Id, 1);
        Guid cancelRequest;
        using (var scope = s3.Services.CreateScope())
        {
            cancelRequest = await scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>().BookingRequestItems
                .Where(i => i.Id == cancelItem).Select(i => i.BookingRequestId).SingleAsync();
        }
        (await s3.Client(s3.StudentToken).DeleteAsync($"/api/booking-requests/{cancelRequest}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AuditActionsAsync(consumable.Id)).Should().Equal("ConsumableCreated", "ConsumableUpdated", "StockIn", "ConsumableDeactivated");
        (await AuditActionsAsync(issued)).Should().Equal("ReservationIssued");
        (await AuditActionsAsync(released)).Should().Equal("ReservationReleased");
        (await AuditActionsAsync(supplier.Id)).Should().Equal("SupplierCreated", "SupplierUpdated");
        (await AuditActionsAsync(s3.StudentProfileId)).Should().Contain("StudentProfileUpdated");
        (await AuditActionsAsync(cancelRequest)).Should().Equal("BookingCancelled");
    }

    // ---- CW-06: maintenance ------------------------------------------------------------------

    [Fact]
    public async Task A_Window_Over_Confirmed_Bookings_Is_409_Listing_Them_Unless_Forced_Which_Emails_The_Students()
    {
        var roomId = await CreateRoomAsync();
        var starts = DateTimeOffset.UtcNow.AddDays(300);
        var (bookingId, requestId) = await SeedConfirmedBookingAsync(roomId, starts.AddHours(1));
        var librarian = s3.Client(s3.LibrarianToken);
        var body = new CreateMaintenanceWindowRequest(roomId, starts, starts.AddHours(4), "Air conditioning");

        var refused = await librarian.PostAsJsonAsync("/api/maintenance-windows", body);

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("type").GetString().Should().EndWith("maintenance-overlaps-bookings");
        problem.RootElement.GetProperty("bookings").EnumerateArray()
            .Select(b => b.GetProperty("bookingId").GetGuid()).Should().Equal(bookingId);

        var forced = await librarian.PostAsJsonAsync("/api/maintenance-windows?force=true", body);

        forced.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Read<MaintenanceWindowResponse>(forced)).AffectedBookings.Should().Be(1);
        using var scope = s3.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var email = await db.EmailNotifications.AsNoTracking()
            .SingleAsync(e => e.BookingRequestId == requestId && e.Template == EmailTemplates.MaintenanceConflict);
        var message = await new EmailRenderer(db).RenderAsync(email, CancellationToken.None);
        message!.TextBody.Should().Contain("Air conditioning").And.Contain("contact the library");
    }

    [Fact]
    public async Task Future_Windows_Can_Be_Edited_And_Cancelled_But_Started_Ones_Cannot()
    {
        var roomId = await CreateRoomAsync();
        var librarian = s3.Client(s3.LibrarianToken);
        var future = DateTimeOffset.UtcNow.AddDays(350);
        var window = await Read<MaintenanceWindowResponse>(await librarian.PostAsJsonAsync("/api/maintenance-windows",
            new CreateMaintenanceWindowRequest(roomId, future, future.AddHours(1), "Painting")));

        var edited = await librarian.PutAsJsonAsync($"/api/maintenance-windows/{window.Id}",
            new UpdateMaintenanceWindowRequest(future.AddHours(2), future.AddHours(3), "Painting, second coat"));
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Read<MaintenanceWindowResponse>(edited)).Reason.Should().Be("Painting, second coat");

        Guid startedId;
        using (var scope = s3.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var started = new MaintenanceWindow
            {
                Id = Guid.NewGuid(), RoomId = roomId, Reason = "Already underway",
                StartsAt = DateTimeOffset.UtcNow.AddHours(-1), EndsAt = DateTimeOffset.UtcNow.AddHours(1), CreatedAt = DateTimeOffset.UtcNow,
            };
            db.MaintenanceWindows.Add(started);
            await db.SaveChangesAsync();
            startedId = started.Id;
        }

        (await librarian.PutAsJsonAsync($"/api/maintenance-windows/{startedId}",
            new UpdateMaintenanceWindowRequest(future, future.AddHours(1), "Move it"))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await librarian.DeleteAsync($"/api/maintenance-windows/{startedId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await librarian.DeleteAsync($"/api/maintenance-windows/{window.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AuditActionsAsync(window.Id)).Should().Equal("MaintenanceScheduled", "MaintenanceUpdated", "MaintenanceCancelled");
    }

    // ---- CW-07: consumable report counts released reservations -----------------------------

    [Fact]
    public async Task The_Consumable_Report_Counts_Released_Reservations_Even_Without_A_Ledger_Row()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var consumableId = await s3.SeedConsumableAsync(stock: 10);
        var itemId = await s3.SeedBookingRequestItemAsync(consumableId, 4321);
        using (var scope = s3.Services.CreateScope())
        {
            // A Pending reservation released by reject/ask-for-change: no stock moved, no ledger row.
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            db.StockReservations.Add(new StockReservation
            {
                Id = Guid.NewGuid(), BookingRequestItemId = itemId, ConsumableId = consumableId, Quantity = 4321,
                Status = StockReservationStatus.Released, ReleasedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        var range = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"))}";

        var report = await s3.Client(s3.StoreOfficerToken).GetFromJsonAsync<ConsumableUsageReportResponse>(
            $"/api/reports/consumable-usage?{range}&sortBy=released&pageSize=5", TestSupport.JsonOptions);

        report!.ByItem.Items.Should().Contain(r => r.ConsumableId == consumableId && r.Released == 4321);
        report.TotalReleased.Should().BeGreaterThanOrEqualTo(4321);
    }

    [Fact]
    public async Task Reservations_Say_Who_And_When_And_Filter_By_The_Day_They_Are_Due()
    {
        var roomId = await CreateRoomAsync();
        var slot = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(200).AddHours(5), TimeSpan.Zero); // 10:30 Colombo
        var (_, requestId) = await SeedConfirmedBookingAsync(roomId, slot);
        Guid itemId;
        using (var scope = s3.Services.CreateScope())
        {
            itemId = await scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>().BookingRequestItems
                .Where(i => i.BookingRequestId == requestId).Select(i => i.Id).SingleAsync();
        }
        var store = s3.Client(s3.StoreOfficerToken);
        (await store.PostAsJsonAsync("/api/stock-reservations", new { bookingRequestItemId = itemId })).StatusCode.Should().Be(HttpStatusCode.Created);
        var day = DateOnly.FromDateTime(slot.ToOffset(TimeSpan.FromMinutes(330)).DateTime);

        var due = await store.GetFromJsonAsync<PagedResultShape<StockReservationResponse>>(
            $"/api/stock-reservations?dueOn={day:yyyy-MM-dd}&pageSize=100", TestSupport.JsonOptions);
        var notDue = await store.GetFromJsonAsync<PagedResultShape<StockReservationResponse>>(
            $"/api/stock-reservations?dueOn={day.AddDays(1):yyyy-MM-dd}&pageSize=100", TestSupport.JsonOptions);

        var row = due!.Items.Should().ContainSingle(r => r.BookingRequestItemId == itemId).Subject;
        row.BookingRequestId.Should().Be(requestId);
        row.StudentName.Should().NotBeNullOrWhiteSpace();
        row.RequestObjective.Should().Be("S3 stock test");
        row.SlotStartsAt.Should().Be(slot);
        row.RoomName.Should().StartWith("B1 room");
        notDue!.Items.Should().NotContain(r => r.BookingRequestItemId == itemId);
    }

    // ---- CW-08 and C-16: students list and weekly allowance --------------------------------

    [Fact]
    public async Task The_Students_List_Shows_Name_Email_And_A_Suspended_Flag_And_Searches_By_Email()
    {
        var admin = s3.Client(s3.AdminToken);
        var profile = await Read<StudentProfileResponse>(await s3.Client(s3.LibrarianToken).GetAsync($"/api/student-profiles/{s3.StudentProfileId}"));
        profile.Email.Should().NotBeNullOrWhiteSpace();
        profile.FullName.Should().NotBeNullOrWhiteSpace();
        profile.IsSuspended.Should().BeFalse();

        (await admin.PutAsJsonAsync($"/api/student-profiles/{s3.StudentProfileId}", new
        {
            department = profile.Department, yearOfStudy = profile.YearOfStudy, maxBookingsPerWeek = profile.MaxBookingsPerWeek,
            penaltyPoints = 2, suspendedUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), isActive = true,
        })).EnsureSuccessStatusCode();

        var page = await admin.GetFromJsonAsync<PagedResultShape<StudentProfileResponse>>(
            $"/api/student-profiles?search={Uri.EscapeDataString(profile.Email)}", TestSupport.JsonOptions);
        var row = page!.Items.Should().ContainSingle().Subject;
        row.Id.Should().Be(s3.StudentProfileId);
        row.IsSuspended.Should().BeTrue();
        row.PenaltyPoints.Should().Be(2);

        (await admin.PutAsJsonAsync($"/api/student-profiles/{s3.StudentProfileId}", new
        {
            department = profile.Department, yearOfStudy = profile.YearOfStudy, maxBookingsPerWeek = profile.MaxBookingsPerWeek,
            penaltyPoints = profile.PenaltyPoints, suspendedUntil = (DateOnly?)null, isActive = true,
        })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Eligibility_Reports_How_Many_Requests_Were_Sent_This_Week()
    {
        var student = s3.Client(s3.StudentToken);
        var before = await student.GetFromJsonAsync<EligibilityResponse>($"/api/student-profiles/{s3.StudentProfileId}/eligibility", TestSupport.JsonOptions);
        before!.MaxBookingsPerWeek.Should().BeGreaterThan(0);

        var itemId = await s3.SeedBookingRequestItemAsync(await s3.SeedConsumableAsync(stock: 5), 1);
        using (var scope = s3.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var requestId = await db.BookingRequestItems.Where(i => i.Id == itemId).Select(i => i.BookingRequestId).SingleAsync();
            db.WorkflowExecutions.Add(new WorkflowExecution
            {
                Id = Guid.NewGuid(), BookingRequestId = requestId, Objective = "B1 weekly count",
                Status = WorkflowStatus.PendingApproval, StartedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var after = await student.GetFromJsonAsync<EligibilityResponse>($"/api/student-profiles/{s3.StudentProfileId}/eligibility", TestSupport.JsonOptions);
        after!.UsedThisWeek.Should().Be(before.UsedThisWeek + 1);
    }
}
