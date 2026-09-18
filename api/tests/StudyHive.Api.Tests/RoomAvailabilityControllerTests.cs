using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Common;
using StudyHive.Api.Controllers.Rooms;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

public class RoomAvailabilityControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = [];
    private readonly List<Guid> _createdRoomIds = [];
    private readonly List<Guid> _createdEquipmentTypeIds = [];
    private readonly List<Guid> _createdWindowIds = [];
    private readonly List<Guid> _createdBookingIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            db.RoomBookings.RemoveRange(db.RoomBookings.Where(b => _createdBookingIds.Contains(b.Id)));
            db.MaintenanceWindows.RemoveRange(db.MaintenanceWindows.Where(w => _createdWindowIds.Contains(w.Id)));
            db.RoomEquipment.RemoveRange(db.RoomEquipment.Where(e =>
                _createdRoomIds.Contains(e.RoomId) ||
                _createdEquipmentTypeIds.Contains(e.EquipmentTypeId)));
            await db.SaveChangesAsync();

            db.StudyRooms.RemoveRange(db.StudyRooms.Where(r => _createdRoomIds.Contains(r.Id)));
            db.EquipmentTypes.RemoveRange(db.EquipmentTypes.Where(e => _createdEquipmentTypeIds.Contains(e.Id)));
            await db.SaveChangesAsync();
        }

        await TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());
    }

    [Fact]
    public async Task Availability_Filters_By_Minimum_Capacity()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var smallRoom = await CreateRoomAsync(client, capacity: 4);
        var largeRoom = await CreateRoomAsync(client, capacity: 12);
        var startsAt = DateTimeOffset.UtcNow.AddDays(10);

        var result = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            AvailabilityUrl(startsAt, startsAt.AddHours(2), "&capacity=8&pageSize=100"),
            TestSupport.JsonOptions);

        result!.Items.Should().Contain(r => r.Id == largeRoom.Id);
        result.Items.Should().NotContain(r => r.Id == smallRoom.Id);
    }

    [Fact]
    public async Task Availability_Filters_By_Installed_Equipment()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var equippedRoom = await CreateRoomAsync(client);
        var otherRoom = await CreateRoomAsync(client);
        var equipment = await CreateEquipmentAsync(client);

        var assign = await client.PostAsJsonAsync($"/api/rooms/{equippedRoom.Id}/equipment", new
        {
            equipmentTypeId = equipment.Id,
            quantity = 1,
        });
        assign.EnsureSuccessStatusCode();

        var startsAt = DateTimeOffset.UtcNow.AddDays(11);
        var result = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            AvailabilityUrl(startsAt, startsAt.AddHours(2), $"&equipmentTypeId={equipment.Id}&pageSize=100"),
            TestSupport.JsonOptions);

        result!.Items.Should().Contain(r => r.Id == equippedRoom.Id);
        result.Items.Should().NotContain(r => r.Id == otherRoom.Id);
    }

    [Fact]
    public async Task Availability_Excludes_A_Room_During_Maintenance()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var blockedRoom = await CreateRoomAsync(client);
        var freeRoom = await CreateRoomAsync(client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(12);
        await CreateMaintenanceWindowAsync(client, blockedRoom.Id, startsAt, startsAt.AddHours(2));

        var result = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            AvailabilityUrl(startsAt.AddMinutes(30), startsAt.AddHours(1), "&pageSize=100"),
            TestSupport.JsonOptions);

        result!.Items.Should().NotContain(r => r.Id == blockedRoom.Id);
        result.Items.Should().Contain(r => r.Id == freeRoom.Id);
    }

    [Fact]
    public async Task Availability_Excludes_A_Room_With_A_Confirmed_Booking()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var bookedRoom = await CreateRoomAsync(client);
        var freeRoom = await CreateRoomAsync(client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(13);
        await CreateConfirmedBookingAsync(client, bookedRoom.Id, startsAt, startsAt.AddHours(2));

        var result = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            AvailabilityUrl(startsAt.AddMinutes(15), startsAt.AddHours(1), "&pageSize=100"),
            TestSupport.JsonOptions);

        result!.Items.Should().NotContain(r => r.Id == bookedRoom.Id);
        result.Items.Should().Contain(r => r.Id == freeRoom.Id);
    }

    [Fact]
    public async Task Schedule_Returns_Booked_And_Maintenance_Periods_In_Time_Order()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(14);
        await CreateMaintenanceWindowAsync(client, room.Id, startsAt.AddHours(3), startsAt.AddHours(4));
        await CreateConfirmedBookingAsync(client, room.Id, startsAt.AddHours(1), startsAt.AddHours(2));

        var slots = await client.GetFromJsonAsync<List<RoomScheduleSlotResponse>>(
            ScheduleUrl(room.Id, startsAt, startsAt.AddHours(5)),
            TestSupport.JsonOptions);

        slots.Should().HaveCount(2);
        slots![0].Kind.Should().Be("Booked");
        slots[1].Kind.Should().Be("Maintenance");
    }

    [Fact]
    public async Task Student_Can_Read_A_Room_Schedule_For_The_Mobile_Flow()
    {
        var librarian = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(librarian);
        var studentClient = factory.CreateClient();
        var (student, _, token) = await TestSupport.CreateAndLoginStudentAsync(studentClient);
        _createdUserIds.Add(student.Id);
        studentClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var startsAt = DateTimeOffset.UtcNow.AddDays(15);

        var response = await studentClient.GetAsync(
            ScheduleUrl(room.Id, startsAt, startsAt.AddHours(2)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Availability_And_Schedule_Reject_Invalid_Time_Ranges()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(16);

        (await client.GetAsync(AvailabilityUrl(startsAt, startsAt)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync(ScheduleUrl(room.Id, startsAt, startsAt)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Schedule_For_An_Unknown_Room_Returns_404()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var startsAt = DateTimeOffset.UtcNow.AddDays(17);

        var response = await client.GetAsync(
            ScheduleUrl(Guid.NewGuid(), startsAt, startsAt.AddHours(2)));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateStaffClientAsync(UserRole role)
    {
        var client = factory.CreateClient();
        var (userId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, client, role);
        _createdUserIds.Add(userId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private async Task<RoomResponse> CreateRoomAsync(HttpClient client, int capacity = 8)
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = $"Availability room {unique[..8]}",
            building = "Main",
            floor = 2,
            capacity,
            hourlyRate = 300m,
            qrCode = $"availability-room-{unique}",
        });
        response.EnsureSuccessStatusCode();

        var room = (await response.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions))!;
        _createdRoomIds.Add(room.Id);
        return room;
    }

    private async Task<EquipmentTypeResponse> CreateEquipmentAsync(HttpClient client)
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/equipment", new
        {
            name = $"Availability equipment {unique[..8]}",
            category = "Display",
            description = "Availability test equipment",
        });
        response.EnsureSuccessStatusCode();

        var equipment = (await response.Content.ReadFromJsonAsync<EquipmentTypeResponse>(TestSupport.JsonOptions))!;
        _createdEquipmentTypeIds.Add(equipment.Id);
        return equipment;
    }

    private async Task CreateMaintenanceWindowAsync(
        HttpClient client,
        Guid roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        var response = await client.PostAsJsonAsync("/api/maintenance-windows", new
        {
            roomId,
            startsAt,
            endsAt,
            reason = "Availability test maintenance",
        });
        response.EnsureSuccessStatusCode();

        var window = (await response.Content.ReadFromJsonAsync<MaintenanceWindowResponse>(TestSupport.JsonOptions))!;
        _createdWindowIds.Add(window.Id);
    }

    private async Task CreateConfirmedBookingAsync(
        HttpClient client,
        Guid roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        var (student, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(student.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, token);
        var requestId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        _createdBookingIds.Add(bookingId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.BookingRequests.Add(new BookingRequest
        {
            Id = requestId,
            StudentId = profile.Id,
            Objective = "Availability conflict test",
            GroupSize = 4,
            PreferredDateFrom = DateOnly.FromDateTime(startsAt.UtcDateTime),
            PreferredDateTo = DateOnly.FromDateTime(endsAt.UtcDateTime),
            PreferredTimeFrom = new TimeOnly(9, 0),
            PreferredTimeTo = new TimeOnly(11, 0),
            SessionsRequired = 1,
            SessionDurationMinutes = 60,
            Budget = 500m,
            Status = BookingRequestStatus.Approved,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.RoomBookings.Add(new RoomBooking
        {
            Id = bookingId,
            RoomId = roomId,
            BookingRequestId = requestId,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = RoomBookingStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static string AvailabilityUrl(
        DateTimeOffset from,
        DateTimeOffset to,
        string suffix = "") =>
        $"/api/rooms/available?from={Uri.EscapeDataString(from.ToString("O"))}" +
        $"&to={Uri.EscapeDataString(to.ToString("O"))}{suffix}";

    private static string ScheduleUrl(Guid roomId, DateTimeOffset from, DateTimeOffset to) =>
        $"/api/rooms/{roomId}/schedule?from={Uri.EscapeDataString(from.ToString("O"))}" +
        $"&to={Uri.EscapeDataString(to.ToString("O"))}";
}
