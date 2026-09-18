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

public class RoomsControllerTests(WebApplicationFactory<Program> factory)
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

            if (_createdRoomIds.Count > 0)
            {
                db.RoomEquipment.RemoveRange(db.RoomEquipment.Where(x => _createdRoomIds.Contains(x.RoomId)));
                db.StudyRooms.RemoveRange(db.StudyRooms.Where(x => _createdRoomIds.Contains(x.Id)));
            }

            if (_createdEquipmentTypeIds.Count > 0)
            {
                db.EquipmentTypes.RemoveRange(db.EquipmentTypes.Where(x => _createdEquipmentTypeIds.Contains(x.Id)));
            }

            await db.SaveChangesAsync();
        }

        await TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());
    }

    [Fact]
    public async Task Anonymous_User_Cannot_List_Rooms()
    {
        var response = await factory.CreateClient().GetAsync("/api/rooms");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Librarian_Can_Create_And_Read_A_Room()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);

        var createResponse = await client.PostAsJsonAsync("/api/rooms", NewRoomBody());

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions);
        created.Should().NotBeNull();
        _createdRoomIds.Add(created!.Id);

        var getResponse = await client.GetAsync($"/api/rooms/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await getResponse.Content.ReadFromJsonAsync<RoomDetailResponse>(TestSupport.JsonOptions);
        detail!.Name.Should().Be(created.Name);
        detail.Equipment.Should().BeEmpty();
    }

    [Fact]
    public async Task Student_Cannot_Create_A_Room()
    {
        var client = factory.CreateClient();
        var (student, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(student.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/rooms", NewRoomBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Rejects_Invalid_Room_Data()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);

        var response = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = " ",
            building = "Main",
            floor = 1,
            capacity = 0,
            hourlyRate = -1,
            qrCode = " ",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Room_Detail_Includes_Assigned_Equipment()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);

        var equipmentTypeId = Guid.NewGuid();
        _createdEquipmentTypeIds.Add(equipmentTypeId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.EquipmentTypes.Add(new EquipmentType
            {
                Id = equipmentTypeId,
                Name = $"Projector-{equipmentTypeId:N}",
                Category = "Display",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RoomEquipment.Add(new RoomEquipment
            {
                RoomId = room.Id,
                EquipmentTypeId = equipmentTypeId,
                Quantity = 2,
                InstalledAt = now,
            });
            await db.SaveChangesAsync();
        }

        var detail = await client.GetFromJsonAsync<RoomDetailResponse>(
            $"/api/rooms/{room.Id}", TestSupport.JsonOptions);

        detail!.Equipment.Should().ContainSingle();
        detail.Equipment[0].EquipmentTypeId.Should().Be(equipmentTypeId);
        detail.Equipment[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Librarian_Can_Update_A_Room()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(client);

        var response = await client.PutAsJsonAsync($"/api/rooms/{room.Id}", new
        {
            name = room.Name + " Updated",
            building = "Engineering",
            floor = 3,
            capacity = 12,
            hourlyRate = 450m,
            qrCode = room.QrCode + "-updated",
            isActive = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions);
        updated!.Building.Should().Be("Engineering");
        updated.Capacity.Should().Be(12);
        updated.HourlyRate.Should().Be(450m);
    }

    [Fact]
    public async Task Unknown_Sort_Field_Returns_400()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);

        var response = await client.GetAsync("/api/rooms?sortBy=notARealField");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Filters_By_Minimum_Capacity_And_Equipment_Type()
    {
        var client = await CreateStaffClientAsync(UserRole.Librarian);
        var prefix = $"Filter-{Guid.NewGuid():N}";

        var smallResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = $"{prefix}-small",
            building = "Main",
            floor = 1,
            capacity = 4,
            hourlyRate = 100m,
            qrCode = $"{prefix}-small",
        });
        smallResponse.EnsureSuccessStatusCode();
        var small = (await smallResponse.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions))!;
        _createdRoomIds.Add(small.Id);

        var largeResponse = await client.PostAsJsonAsync("/api/rooms", new
        {
            name = $"{prefix}-large",
            building = "Main",
            floor = 1,
            capacity = 12,
            hourlyRate = 200m,
            qrCode = $"{prefix}-large",
        });
        largeResponse.EnsureSuccessStatusCode();
        var large = (await largeResponse.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions))!;
        _createdRoomIds.Add(large.Id);

        var equipmentTypeId = Guid.NewGuid();
        _createdEquipmentTypeIds.Add(equipmentTypeId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.EquipmentTypes.Add(new EquipmentType
            {
                Id = equipmentTypeId,
                Name = $"Projector-{equipmentTypeId:N}",
                Category = "Display",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.RoomEquipment.Add(new RoomEquipment
            {
                RoomId = large.Id,
                EquipmentTypeId = equipmentTypeId,
                Quantity = 1,
                InstalledAt = now,
            });
            await db.SaveChangesAsync();
        }

        var result = await client.GetFromJsonAsync<PagedResult<RoomResponse>>(
            $"/api/rooms?search={prefix}&capacity=8&equipmentTypeId={equipmentTypeId}",
            TestSupport.JsonOptions);

        result!.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(large.Id);
    }

    [Fact]
    public async Task Only_Admin_Can_Deactivate_A_Room()
    {
        var librarian = await CreateStaffClientAsync(UserRole.Librarian);
        var room = await CreateRoomAsync(librarian);

        var forbidden = await librarian.DeleteAsync($"/api/rooms/{room.Id}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await CreateStaffClientAsync(UserRole.Admin);
        var deactivated = await admin.DeleteAsync($"/api/rooms/{room.Id}");
        deactivated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await admin.GetFromJsonAsync<RoomDetailResponse>(
            $"/api/rooms/{room.Id}", TestSupport.JsonOptions);
        detail!.IsActive.Should().BeFalse();
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
        var response = await client.PostAsJsonAsync("/api/rooms", NewRoomBody());
        response.EnsureSuccessStatusCode();
        var room = (await response.Content.ReadFromJsonAsync<RoomResponse>(TestSupport.JsonOptions))!;
        _createdRoomIds.Add(room.Id);
        return room;
    }

    private static object NewRoomBody()
    {
        var unique = Guid.NewGuid().ToString("N");
        return new
        {
            name = $"Test room {unique[..8]}",
            building = "Main",
            floor = 2,
            capacity = 8,
            hourlyRate = 300m,
            qrCode = $"room-{unique}",
        };
    }
}
