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

public class EquipmentControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = [];
    private readonly List<Guid> _createdRoomIds = [];
    private readonly List<Guid> _createdEquipmentTypeIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            db.RoomEquipment.RemoveRange(db.RoomEquipment.Where(x =>
                _createdRoomIds.Contains(x.RoomId) ||
                _createdEquipmentTypeIds.Contains(x.EquipmentTypeId)));
            await db.SaveChangesAsync();

            db.StudyRooms.RemoveRange(db.StudyRooms.Where(x => _createdRoomIds.Contains(x.Id)));
            db.EquipmentTypes.RemoveRange(db.EquipmentTypes.Where(x => _createdEquipmentTypeIds.Contains(x.Id)));
            await db.SaveChangesAsync();
        }

        await TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());
    }

    [Fact]
    public async Task Librarian_Can_Create_List_And_Update_Equipment()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var equipment = await CreateEquipmentAsync(client);

        var list = await client.GetFromJsonAsync<PagedResult<EquipmentTypeResponse>>(
            $"/api/equipment?search={Uri.EscapeDataString(equipment.Name)}",
            TestSupport.JsonOptions);

        list!.Items.Should().ContainSingle(e => e.Id == equipment.Id);

        var updateResponse = await client.PutAsJsonAsync($"/api/equipment/{equipment.Id}", new
        {
            name = equipment.Name,
            category = "Presentation",
            description = "Updated description",
            isActive = false,
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<EquipmentTypeResponse>(TestSupport.JsonOptions);
        updated!.Category.Should().Be("Presentation");
        updated.Description.Should().Be("Updated description");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Student_Cannot_Create_Equipment()
    {
        var client = factory.CreateClient();
        var (student, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(student.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/equipment", NewEquipmentBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Rejects_Invalid_Equipment_Data()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);

        var response = await client.PostAsJsonAsync("/api/equipment", new
        {
            name = " ",
            category = " ",
            description = "Invalid equipment",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_Equipment_Sort_Field_Returns_400()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);

        var response = await client.GetAsync("/api/equipment?sortBy=notARealField");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Librarian_Can_Assign_And_Remove_Room_Equipment()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);
        var equipment = await CreateEquipmentAsync(client);

        var assignResponse = await client.PostAsJsonAsync($"/api/rooms/{room.Id}/equipment", new
        {
            equipmentTypeId = equipment.Id,
            quantity = 2,
        });

        assignResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var assignment = await assignResponse.Content.ReadFromJsonAsync<RoomEquipmentResponse>(TestSupport.JsonOptions);
        assignment!.EquipmentTypeId.Should().Be(equipment.Id);
        assignment.Quantity.Should().Be(2);

        var detail = await client.GetFromJsonAsync<RoomDetailResponse>(
            $"/api/rooms/{room.Id}", TestSupport.JsonOptions);
        detail!.Equipment.Should().ContainSingle(e => e.EquipmentTypeId == equipment.Id && e.Quantity == 2);

        var removeResponse = await client.DeleteAsync(
            $"/api/rooms/{room.Id}/equipment/{equipment.Id}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        detail = await client.GetFromJsonAsync<RoomDetailResponse>(
            $"/api/rooms/{room.Id}", TestSupport.JsonOptions);
        detail!.Equipment.Should().BeEmpty();
    }

    [Fact]
    public async Task Assigning_The_Same_Equipment_Twice_Returns_409()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);
        var equipment = await CreateEquipmentAsync(client);
        var body = new { equipmentTypeId = equipment.Id, quantity = 1 };

        (await client.PostAsJsonAsync($"/api/rooms/{room.Id}/equipment", body))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await client.PostAsJsonAsync($"/api/rooms/{room.Id}/equipment", body))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
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
            name = $"Equipment test room {unique[..8]}",
            building = "Main",
            floor = 1,
            capacity = 6,
            hourlyRate = 250m,
            qrCode = $"equipment-room-{unique}",
        });
        response.EnsureSuccessStatusCode();

        var room = (await response.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions))!;
        _createdRoomIds.Add(room.Id);
        return room;
    }

    private async Task<EquipmentTypeResponse> CreateEquipmentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/equipment", NewEquipmentBody());
        response.EnsureSuccessStatusCode();

        var equipment = (await response.Content.ReadFromJsonAsync<EquipmentTypeResponse>(TestSupport.JsonOptions))!;
        _createdEquipmentTypeIds.Add(equipment.Id);
        return equipment;
    }

    private static object NewEquipmentBody()
    {
        var unique = Guid.NewGuid().ToString("N");
        return new
        {
            name = $"Projector-{unique[..8]}",
            category = "Display",
            description = "Portable projector",
        };
    }
}
