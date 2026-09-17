using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Controllers.Rooms;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

public class RoomBookingCreationTests(WebApplicationFactory<Program> factory)
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
            db.MaintenanceWindows.RemoveRange(db.MaintenanceWindows.Where(w => _roomIds.Contains(w.RoomId)));
            await db.SaveChangesAsync();
            db.BookingRequests.RemoveRange(db.BookingRequests.Where(r => _requestIds.Contains(r.Id)));
            db.StudyRooms.RemoveRange(db.StudyRooms.Where(r => _roomIds.Contains(r.Id)));
            await db.SaveChangesAsync();
        }

        await TestSupport.CleanupAsync(factory, _userIds.ToArray());
    }

    [Fact]
    public async Task Staff_Can_Create_Confirmed_Booking_And_Local_Offset_Is_Normalized_To_Utc()
    {
        var setup = await CreateSetupAsync(BookingRequestStatus.Approved);
        var startsAt = new DateTimeOffset(2036, 1, 10, 9, 0, 0, TimeSpan.FromHours(5.5));

        var response = await setup.StaffClient.PostAsJsonAsync("/api/room-bookings", new
        {
            roomId = setup.RoomId,
            bookingRequestId = setup.RequestId,
            startsAt,
            endsAt = startsAt.AddHours(1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var booking = await response.Content.ReadFromJsonAsync<RoomBookingResponse>(TestSupport.JsonOptions);
        booking.Should().NotBeNull();
        booking!.RoomId.Should().Be(setup.RoomId);
        booking.BookingRequestId.Should().Be(setup.RequestId);
        booking.Status.Should().Be(RoomBookingStatus.Confirmed);
        booking.StartsAt.Offset.Should().Be(TimeSpan.Zero);
        booking.StartsAt.Should().Be(startsAt.ToUniversalTime());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        (await db.RoomBookings.SingleAsync(b => b.Id == booking.Id)).StartsAt
            .Should().Be(startsAt.ToUniversalTime());
    }

    [Fact]
    public async Task Create_Rejects_An_Overlapping_Confirmed_Booking()
    {
        var first = await CreateSetupAsync(BookingRequestStatus.Approved);
        var second = await SeedRequestForExistingStudentAsync(first.StudentProfileId, BookingRequestStatus.Approved);
        var startsAt = new DateTimeOffset(2036, 2, 10, 8, 0, 0, TimeSpan.Zero);
        await SeedConfirmedBookingAsync(first.RoomId, first.RequestId, startsAt, startsAt.AddHours(2));

        var response = await first.StaffClient.PostAsJsonAsync("/api/room-bookings", new
        {
            roomId = first.RoomId,
            bookingRequestId = second,
            startsAt = startsAt.AddHours(1),
            endsAt = startsAt.AddHours(3),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Rejects_A_Maintenance_Overlap()
    {
        var setup = await CreateSetupAsync(BookingRequestStatus.Approved);
        var startsAt = new DateTimeOffset(2036, 3, 10, 8, 0, 0, TimeSpan.Zero);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            db.MaintenanceWindows.Add(new MaintenanceWindow
            {
                Id = Guid.NewGuid(),
                RoomId = setup.RoomId,
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(2),
                Reason = "Creation test maintenance",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var response = await setup.StaffClient.PostAsJsonAsync("/api/room-bookings", new
        {
            roomId = setup.RoomId,
            bookingRequestId = setup.RequestId,
            startsAt = startsAt.AddMinutes(30),
            endsAt = startsAt.AddHours(1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Rejects_A_Request_That_Is_Not_Approved()
    {
        var setup = await CreateSetupAsync(BookingRequestStatus.PendingApproval);
        var startsAt = new DateTimeOffset(2036, 4, 10, 8, 0, 0, TimeSpan.Zero);

        var response = await setup.StaffClient.PostAsJsonAsync("/api/room-bookings", new
        {
            roomId = setup.RoomId,
            bookingRequestId = setup.RequestId,
            startsAt,
            endsAt = startsAt.AddHours(1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Rejects_An_Invalid_Time_Range()
    {
        var setup = await CreateSetupAsync(BookingRequestStatus.Approved);
        var startsAt = new DateTimeOffset(2036, 5, 10, 8, 0, 0, TimeSpan.Zero);

        var response = await setup.StaffClient.PostAsJsonAsync("/api/room-bookings", new
        {
            roomId = setup.RoomId,
            bookingRequestId = setup.RequestId,
            startsAt,
            endsAt = startsAt,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Student_Cannot_Create_A_Confirmed_Room_Booking()
    {
        var setup = await CreateSetupAsync(BookingRequestStatus.Approved);
        var studentClient = factory.CreateClient();
        var studentToken = await LoginExistingStudentAsync(setup.StudentEmail);
        studentClient.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        var startsAt = new DateTimeOffset(2036, 6, 10, 8, 0, 0, TimeSpan.Zero);

        var response = await studentClient.PostAsJsonAsync("/api/room-bookings", new
        {
            roomId = setup.RoomId,
            bookingRequestId = setup.RequestId,
            startsAt,
            endsAt = startsAt.AddHours(1),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Setup> CreateSetupAsync(BookingRequestStatus requestStatus)
    {
        var studentClient = factory.CreateClient();
        var (student, email, token) = await TestSupport.CreateAndLoginStudentAsync(studentClient);
        _userIds.Add(student.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(studentClient, token);

        var staffClient = factory.CreateClient();
        var (staffId, _, staffToken) = await TestSupport.CreateAndLoginStaffAsync(
            factory, staffClient, UserRole.Librarian);
        _userIds.Add(staffId);
        staffClient.DefaultRequestHeaders.Authorization = new("Bearer", staffToken);

        var now = DateTimeOffset.UtcNow;
        var room = new StudyRoom
        {
            Id = Guid.NewGuid(),
            Name = $"Booking create {Guid.NewGuid():N}"[..28],
            Building = "Test building",
            Floor = 1,
            Capacity = 8,
            HourlyRate = 125,
            QrCode = $"booking-create-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = NewBookingRequest(profile.Id, requestStatus);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            db.StudyRooms.Add(room);
            db.BookingRequests.Add(request);
            await db.SaveChangesAsync();
        }

        _roomIds.Add(room.Id);
        _requestIds.Add(request.Id);
        return new Setup(staffClient, room.Id, request.Id, profile.Id, email);
    }

    private async Task<Guid> SeedRequestForExistingStudentAsync(
        Guid studentProfileId,
        BookingRequestStatus status)
    {
        var request = NewBookingRequest(studentProfileId, status);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        db.BookingRequests.Add(request);
        await db.SaveChangesAsync();
        _requestIds.Add(request.Id);
        return request.Id;
    }

    private async Task SeedConfirmedBookingAsync(
        Guid roomId,
        Guid requestId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.RoomBookings.Add(new RoomBooking
        {
            Id = Guid.NewGuid(),
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

    private async Task<string> LoginExistingStudentAsync(string email)
    {
        var client = factory.CreateClient();
        var tokens = await TestSupport.LoginAsync(client, email);
        return tokens.AccessToken;
    }

    private static BookingRequest NewBookingRequest(Guid studentProfileId, BookingRequestStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = studentProfileId,
            Objective = "Create a confirmed room booking",
            GroupSize = 4,
            PreferredDateFrom = new DateOnly(2036, 1, 1),
            PreferredDateTo = new DateOnly(2036, 12, 31),
            PreferredTimeFrom = new TimeOnly(8, 0),
            PreferredTimeTo = new TimeOnly(18, 0),
            SessionsRequired = 1,
            SessionDurationMinutes = 60,
            Budget = 500,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private sealed record Setup(
        HttpClient StaffClient,
        Guid RoomId,
        Guid RequestId,
        Guid StudentProfileId,
        string StudentEmail);
}
