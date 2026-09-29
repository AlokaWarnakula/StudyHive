using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using StudyHive.Api.Controllers.Store;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

public class ConsumablesControllerTests(S3Fixture s3) : IClassFixture<S3Fixture>
{
    private readonly S3Fixture _s3 = s3;

    private object NewConsumable(string? name = null, int stock = 10, int minStock = 2) => new
    {
        name = name ?? S3Fixture.UniqueName("Marker"),
        description = "Blue whiteboard marker",
        unit = "pcs",
        unitPrice = 58.50m,
        stockQuantity = stock,
        minStockLevel = minStock,
    };

    private async Task<ConsumableResponse> CreateAsync(object body)
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/consumables", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<ConsumableResponse>(TestSupport.JsonOptions))!;
        _s3.ConsumableIds.Add(created.Id);
        return created;
    }

    [Fact]
    public async Task Anonymous_Request_Returns_401()
    {
        var response = await _s3.Client(null).GetAsync("/api/consumables");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Student_Cannot_Create_A_Consumable()
    {
        var response = await _s3.Client(_s3.StudentToken).PostAsJsonAsync("/api/consumables", NewConsumable());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Store_Officer_Can_Create_Read_Update_And_List_A_Consumable()
    {
        var created = await CreateAsync(NewConsumable(stock: 10, minStock: 2));
        created.StockQuantity.Should().Be(10);
        created.AvailableQuantity.Should().Be(10);
        created.IsLowStock.Should().BeFalse();

        var client = _s3.Client(_s3.StoreOfficerToken);

        var detail = await client.GetFromJsonAsync<ConsumableDetailResponse>($"/api/consumables/{created.Id}", TestSupport.JsonOptions);
        detail!.Consumable.Name.Should().Be(created.Name);

        var update = await client.PutAsJsonAsync($"/api/consumables/{created.Id}", new
        {
            name = created.Name,
            description = "Now red",
            unit = "box",
            unitPrice = 60m,
            minStockLevel = 12,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<ConsumableResponse>(TestSupport.JsonOptions))!;
        updated.Unit.Should().Be("box");
        updated.UnitPrice.Should().Be(60m);
        updated.StockQuantity.Should().Be(10, "a PUT never edits stock — only stock-in and reservations move it");
        updated.IsLowStock.Should().BeTrue();

        var list = await client.GetFromJsonAsync<PagedResultShape<ConsumableResponse>>(
            $"/api/consumables?search={Uri.EscapeDataString(created.Name)}", TestSupport.JsonOptions);
        list!.Items.Should().ContainSingle(c => c.Id == created.Id);

        var lowStock = await client.GetFromJsonAsync<List<ConsumableResponse>>("/api/consumables/low-stock", TestSupport.JsonOptions);
        lowStock!.Should().Contain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task Duplicate_Name_Returns_409()
    {
        var created = await CreateAsync(NewConsumable());

        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/consumables", NewConsumable(created.Name));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Negative_Stock_On_Create_Returns_400()
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/consumables", NewConsumable(stock: -1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_Consumable_Returns_404()
    {
        var client = _s3.Client(_s3.StoreOfficerToken);
        var missing = Guid.NewGuid();

        (await client.GetAsync($"/api/consumables/{missing}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/api/consumables/{missing}/stock-in", new { quantity = 5 }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_SortBy_Returns_400()
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).GetAsync("/api/consumables?sortBy=secretColumn");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stock_In_Adds_Stock_And_Writes_A_Ledger_Row()
    {
        var created = await CreateAsync(NewConsumable(stock: 3));
        var client = _s3.Client(_s3.StoreOfficerToken);

        var response = await client.PostAsJsonAsync($"/api/consumables/{created.Id}/stock-in", new { quantity = 7, notes = "Delivery #42" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ConsumableResponse>(TestSupport.JsonOptions))!.StockQuantity.Should().Be(10);

        var detail = await client.GetFromJsonAsync<ConsumableDetailResponse>($"/api/consumables/{created.Id}", TestSupport.JsonOptions);
        var entry = detail!.RecentTransactions.Should().ContainSingle().Subject;
        entry.TransactionType.Should().Be(StockTransactionType.StockIn);
        entry.Quantity.Should().Be(7);
        entry.BalanceAfter.Should().Be(10);
        entry.Notes.Should().Be("Delivery #42");
        entry.CreatedBy.Should().Be(_s3.StoreOfficerId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Stock_In_Must_Be_Positive(int quantity)
    {
        var created = await CreateAsync(NewConsumable(stock: 3));

        var response = await _s3.Client(_s3.StoreOfficerToken)
            .PostAsJsonAsync($"/api/consumables/{created.Id}/stock-in", new { quantity });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _s3.ReadConsumableAsync(created.Id)).StockQuantity.Should().Be(3);
    }

    [Fact]
    public async Task Student_Cannot_Stock_In()
    {
        var created = await CreateAsync(NewConsumable(stock: 3));

        var response = await _s3.Client(_s3.StudentToken)
            .PostAsJsonAsync($"/api/consumables/{created.Id}/stock-in", new { quantity = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_Admin_Can_Deactivate_And_It_Hides_From_The_Active_List()
    {
        var created = await CreateAsync(NewConsumable());

        (await _s3.Client(_s3.StoreOfficerToken).DeleteAsync($"/api/consumables/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await _s3.Client(_s3.AdminToken).DeleteAsync($"/api/consumables/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _s3.ReadConsumableAsync(created.Id)).IsActive.Should().BeFalse("deletes are deactivations, not physical deletes");
        var list = await _s3.Client(_s3.StoreOfficerToken).GetFromJsonAsync<PagedResultShape<ConsumableResponse>>(
            $"/api/consumables?search={Uri.EscapeDataString(created.Name)}", TestSupport.JsonOptions);
        list!.Items.Should().BeEmpty();
    }
}
