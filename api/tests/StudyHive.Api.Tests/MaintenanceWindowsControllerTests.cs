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

public class MaintenanceWindowsControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = [];
    private readonly List<Guid> _createdRoomIds = [];
    private readonly List<Guid> _createdWindowIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            db.MaintenanceWindows.RemoveRange(db.MaintenanceWindows.Where(w => _createdWindowIds.Contains(w.Id)));
            await db.SaveChangesAsync();
            db.StudyRooms.RemoveRange(db.StudyRooms.Where(r => _createdRoomIds.Contains(r.Id)));
            await db.SaveChangesAsync();
        }

        await TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());
    }

    [Fact]
    public async Task Librarian_Can_Create_And_Search_Maintenance_Windows()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(5);

        var createResponse = await client.PostAsJsonAsync("/api/maintenance-windows", new
        {
            roomId = room.Id,
            startsAt,
            endsAt = startsAt.AddHours(2),
            reason = "Projector service",
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<MaintenanceWindowResponse>(TestSupport.JsonOptions);
        created.Should().NotBeNull();
        _createdWindowIds.Add(created!.Id);
        created.RoomName.Should().Be(room.Name);
        created.AffectedBookings.Should().Be(0);

        var list = await client.GetFromJsonAsync<PagedResult<MaintenanceWindowResponse>>(
            "/api/maintenance-windows?search=Projector&sortBy=startsAt&sortDir=asc",
            TestSupport.JsonOptions);

        list!.Items.Should().ContainSingle(w => w.Id == created.Id);
    }

    [Fact]
    public async Task Create_Rejects_An_Invalid_Time_Range()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);
        var startsAt = DateTimeOffset.UtcNow.AddDays(5);

        var response = await client.PostAsJsonAsync("/api/maintenance-windows", new
        {
            roomId = room.Id,
            startsAt,
            endsAt = startsAt,
            reason = "Inspection",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_For_An_Unknown_Room_Returns_404()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var startsAt = DateTimeOffset.UtcNow.AddDays(5);

        var response = await client.PostAsJsonAsync("/api/maintenance-windows", new
        {
            roomId = Guid.NewGuid(),
            startsAt,
            endsAt = startsAt.AddHours(1),
            reason = "Inspection",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Student_Cannot_Create_Or_List_Maintenance_Windows()
    {
        var client = factory.CreateClient();
        var (student, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(student.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var startsAt = DateTimeOffset.UtcNow.AddDays(5);

        var createResponse = await client.PostAsJsonAsync("/api/maintenance-windows", new
        {
            roomId = Guid.NewGuid(),
            startsAt,
            endsAt = startsAt.AddHours(1),
            reason = "Inspection",
        });

        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/maintenance-windows"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unknown_Maintenance_Sort_Field_Returns_400()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);

        var response = await client.GetAsync("/api/maintenance-windows?sortBy=notARealField");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Maintenance_List_Uses_The_Shared_Pagination_Envelope()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);
        await CreateWindowAsync(client, room.Id, "First service", DateTimeOffset.UtcNow.AddDays(6));
        await CreateWindowAsync(client, room.Id, "Second service", DateTimeOffset.UtcNow.AddDays(7));

        var page = await client.GetFromJsonAsync<PagedResult<MaintenanceWindowResponse>>(
            $"/api/maintenance-windows?search={Uri.EscapeDataString(room.Name)}&page=1&pageSize=1",
            TestSupport.JsonOptions);

        page!.Items.Should().HaveCount(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(1);
        page.TotalItems.Should().Be(2);
        page.TotalPages.Should().Be(2);
    }

    private async Task<HttpClient> CreateStaffClientAsync(UserRole role)
    {
        var client = factory.CreateClient();
        var (userId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, client, role);
        _createdUserIds.Add(userId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private async Task<RoomResponse> CreateRoomAsync(HttpClient client)
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = $"Maintenance room {unique[..8]}",
            building = "Main",
            floor = 2,
            capacity = 10,
            hourlyRate = 300m,
            qrCode = $"maintenance-room-{unique}",
        });
        response.EnsureSuccessStatusCode();

        var room = (await response.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions))!;
        _createdRoomIds.Add(room.Id);
        return room;
    }

    private async Task CreateWindowAsync(
        HttpClient client,
        Guid roomId,
        string reason,
        DateTimeOffset startsAt)
    {
        var response = await client.PostAsJsonAsync("/api/maintenance-windows", new
        {
            roomId,
            startsAt,
            endsAt = startsAt.AddHours(1),
            reason,
        });
        response.EnsureSuccessStatusCode();

        var window = (await response.Content.ReadFromJsonAsync<MaintenanceWindowResponse>(TestSupport.JsonOptions))!;
        _createdWindowIds.Add(window.Id);
    }
}
