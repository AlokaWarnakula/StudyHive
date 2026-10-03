using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Controllers.Approvals;
using StudyHive.Api.Controllers.Rooms;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

public class RoomCheckInAndUsageReportTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly List<Guid> _userIds = [];
    private readonly List<Guid> _roomIds = [];
    private readonly List<Guid> _requestIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            db.RoomBookings.RemoveRange(db.RoomBookings.Where(b => _requestIds.Contains(b.BookingRequestId)));
            await db.SaveChangesAsync();
            db.StudyRooms.RemoveRange(db.StudyRooms.Where(r => _roomIds.Contains(r.Id)));
            await db.SaveChangesAsync();
        }
        await TestSupport.CleanupAsync(factory, _userIds.ToArray());
    }

    [Fact]
    public async Task Owning_Student_Can_Check_In_With_The_Assigned_Room_Code()
    {
        var (client, profileId) = await CreateStudentClientAsync();
        var booking = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed,
            DateTimeOffset.UtcNow.AddMinutes(5), 2); // inside the check-in window

        var response = await client.PostAsJsonAsync($"/api/room-bookings/{booking.BookingId}/check-in",
            new { qrCode = booking.QrCode });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoomCheckInResponse>(TestSupport.JsonOptions);
        result!.RoomName.Should().Be(booking.RoomName);
        result.CheckedInAt.Should().NotBe(default);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        (await db.RoomBookings.SingleAsync(b => b.Id == booking.BookingId)).CheckedInAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Mobile_Can_Check_In_Using_The_Booking_Request_Id()
    {
        var (client, profileId) = await CreateStudentClientAsync();
        var booking = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed,
            DateTimeOffset.UtcNow.AddMinutes(5), 2); // inside the check-in window

        var response = await client.PostAsJsonAsync($"/api/room-bookings/{booking.RequestId}/check-in",
            new { qrCode = booking.QrCode });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoomCheckInResponse>(TestSupport.JsonOptions);
        result!.BookingId.Should().Be(booking.BookingId);
        result.CheckedInAt.Should().NotBe(default);
    }

    [Fact]
    public async Task Check_In_Rejects_A_Code_For_Another_Room()
    {
        var (client, profileId) = await CreateStudentClientAsync();
        var booking = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed,
            DateTimeOffset.UtcNow.AddMinutes(5), 2); // inside the check-in window

        var response = await client.PostAsJsonAsync($"/api/room-bookings/{booking.BookingId}/check-in",
            new { qrCode = "wrong-room-code" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Librarian_Can_Read_Room_Usage_For_A_Date_Range()
    {
        var (_, profileId) = await CreateStudentClientAsync();
        var start = new DateTimeOffset(2035, 3, 10, 8, 0, 0, TimeSpan.Zero);
        var first = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed, start, 2);
        await SeedBookingAsync(profileId, RoomBookingStatus.NoShow, start.AddHours(2), 1, first.RoomId);

        var client = factory.CreateClient();
        var (librarianId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _userIds.Add(librarianId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var from = Uri.EscapeDataString(start.ToString("O"));
        var to = Uri.EscapeDataString(start.AddHours(4).ToString("O"));
        var response = await client.GetAsync($"/api/reports/room-usage?from={from}&to={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<RoomUsageReportResponse>(TestSupport.JsonOptions);
        report!.TotalBookings.Should().Be(2);
        report.TotalBookedHours.Should().Be(3m);
        report.NoShows.Should().Be(1);
        report.ByRoom.Single(r => r.RoomId == first.RoomId).UtilisationPercent.Should().Be(75m);
        report.BookingsByHour.Single(h => h.Hour == 8).BookingCount.Should().Be(1);
    }

    [Theory]
    [InlineData(120, "Check-in opens at")]   // starts in 2 h: too early
    [InlineData(-180, "This booking ended at")] // started 3 h ago, 2 h long: over
    public async Task Check_In_Outside_The_Window_Is_422(int startsInMinutes, string detail)
    {
        var (client, profileId) = await CreateStudentClientAsync();
        var booking = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed,
            DateTimeOffset.UtcNow.AddMinutes(startsInMinutes), 2);

        var response = await client.PostAsJsonAsync($"/api/room-bookings/{booking.BookingId}/check-in",
            new { qrCode = booking.QrCode });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("outside-check-in-window").And.Contain(detail);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>().RoomBookings
            .SingleAsync(b => b.Id == booking.BookingId)).CheckedInAt.Should().BeNull();
    }

    [Fact]
    public async Task Check_In_Opens_Fifteen_Minutes_Before_The_Start()
    {
        var (client, profileId) = await CreateStudentClientAsync();
        var justInside = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed, DateTimeOffset.UtcNow.AddMinutes(14), 1);
        var justOutside = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed, DateTimeOffset.UtcNow.AddMinutes(17), 1);

        (await client.PostAsJsonAsync($"/api/room-bookings/{justInside.BookingId}/check-in", new { qrCode = justInside.QrCode }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"/api/room-bookings/{justOutside.BookingId}/check-in", new { qrCode = justOutside.QrCode }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_Wrong_Code_Sent_With_The_Booking_Request_Id_Is_422_Not_404()
    {
        var (client, profileId) = await CreateStudentClientAsync();
        var booking = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed, DateTimeOffset.UtcNow.AddMinutes(5), 2);

        var response = await client.PostAsJsonAsync($"/api/room-bookings/{booking.RequestId}/check-in",
            new { qrCode = "wrong-room-code" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid room QR code");

        // Another student using that request id gets 403, not a hint about the code.
        var (other, _) = await CreateStudentClientAsync();
        (await other.PostAsJsonAsync($"/api/room-bookings/{booking.RequestId}/check-in", new { qrCode = "wrong-room-code" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Students_Never_Receive_Room_Qr_Codes_But_Staff_Do()
    {
        var (student, profileId) = await CreateStudentClientAsync();
        var booking = await SeedBookingAsync(profileId, RoomBookingStatus.Confirmed, DateTimeOffset.UtcNow.AddDays(30), 1);
        var librarian = factory.CreateClient();
        var (librarianId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, librarian, UserRole.Librarian);
        _userIds.Add(librarianId);
        librarian.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var studentDetail = await student.GetStringAsync($"/api/rooms/{booking.RoomId}");
        studentDetail.Should().Contain("\"qrCode\":null").And.NotContain(booking.QrCode);
        (await student.GetStringAsync("/api/rooms?pageSize=100")).Should().NotContain(booking.QrCode);
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(60).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(60).AddHours(1).ToString("O"));
        (await student.GetStringAsync($"/api/rooms/available?from={from}&to={to}&pageSize=100")).Should().NotContain(booking.QrCode);

        (await librarian.GetStringAsync($"/api/rooms/{booking.RoomId}")).Should().Contain(booking.QrCode);
    }

    private async Task<(HttpClient Client, Guid ProfileId)> CreateStudentClientAsync()
    {
        var client = factory.CreateClient();
        var (student, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _userIds.Add(student.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (client, profile.Id);
    }

    private async Task<SeededBooking> SeedBookingAsync(
        Guid profileId,
        RoomBookingStatus status,
        DateTimeOffset startsAt,
        int durationHours,
        Guid? existingRoomId = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        var room = existingRoomId is null
            ? new StudyRoom
            {
                Id = Guid.NewGuid(),
                Name = $"Step 8 room {Guid.NewGuid():N}"[..24],
                Building = "Test building",
                Floor = 1,
                Capacity = 6,
                HourlyRate = 100,
                QrCode = $"step8-{Guid.NewGuid():N}",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            }
            : await db.StudyRooms.SingleAsync(r => r.Id == existingRoomId.Value);
        if (existingRoomId is null)
        {
            db.StudyRooms.Add(room);
            _roomIds.Add(room.Id);
        }

        var request = new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = profileId,
            Objective = "Step 8 endpoint test",
            GroupSize = 2,
            PreferredDateFrom = DateOnly.FromDateTime(startsAt.Date),
            PreferredDateTo = DateOnly.FromDateTime(startsAt.Date),
            PreferredTimeFrom = new TimeOnly(8, 0),
            PreferredTimeTo = new TimeOnly(12, 0),
            SessionsRequired = 1,
            SessionDurationMinutes = 60,
            Budget = 500,
            Status = BookingRequestStatus.Approved,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var booking = new RoomBooking
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            BookingRequestId = request.Id,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(durationHours),
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.BookingRequests.Add(request);
        db.RoomBookings.Add(booking);
        await db.SaveChangesAsync();
        _requestIds.Add(request.Id);
        return new SeededBooking(booking.Id, request.Id, room.Id, room.Name, room.QrCode);
    }

    private sealed record SeededBooking(Guid BookingId, Guid RequestId, Guid RoomId, string RoomName, string QrCode);
}
