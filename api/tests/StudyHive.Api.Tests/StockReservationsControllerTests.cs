using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StudyHive.Api.Controllers.Approvals;
using StudyHive.Api.Controllers.Store;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

public class StockReservationsControllerTests(S3Fixture s3) : IClassFixture<S3Fixture>
{
    private readonly S3Fixture _s3 = s3;

    private async Task<StockReservationResponse> ReserveAsync(Guid itemId)
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/stock-reservations", new { bookingRequestItemId = itemId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<StockReservationResponse>(TestSupport.JsonOptions))!;
    }

    [Fact]
    public async Task Anonymous_Request_Returns_401()
    {
        var response = await _s3.Client(null).GetAsync("/api/stock-reservations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Student_Cannot_Create_Or_List_Reservations()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 5);
        var itemId = await _s3.SeedBookingRequestItemAsync(consumableId, 1);
        var client = _s3.Client(_s3.StudentToken);

        (await client.PostAsJsonAsync("/api/stock-reservations", new { bookingRequestItemId = itemId }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/stock-reservations")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _s3.ReadConsumableAsync(consumableId)).ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Reserve_Holds_Stock_And_Release_Gives_It_Back()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 5);
        var itemId = await _s3.SeedBookingRequestItemAsync(consumableId, 3);

        var reservation = await ReserveAsync(itemId);
        reservation.Status.Should().Be(StockReservationStatus.Reserved);
        reservation.Quantity.Should().Be(3);
        var held = await _s3.ReadConsumableAsync(consumableId);
        held.ReservedQuantity.Should().Be(3);
        held.AvailableQuantity.Should().Be(2);
        held.StockQuantity.Should().Be(5, "reserving holds stock, it does not remove it");

        var client = _s3.Client(_s3.StoreOfficerToken);
        var list = await client.GetFromJsonAsync<PagedResultShape<StockReservationResponse>>(
            "/api/stock-reservations?status=Reserved&pageSize=100", TestSupport.JsonOptions);
        list!.Items.Should().Contain(r => r.Id == reservation.Id);

        var release = await client.PutAsync($"/api/stock-reservations/{reservation.Id}/release", null);
        release.StatusCode.Should().Be(HttpStatusCode.OK);
        (await release.Content.ReadFromJsonAsync<StockReservationResponse>(TestSupport.JsonOptions))!
            .Status.Should().Be(StockReservationStatus.Released);

        var afterRelease = await _s3.ReadConsumableAsync(consumableId);
        afterRelease.ReservedQuantity.Should().Be(0);
        afterRelease.StockQuantity.Should().Be(5);
    }

    [Fact]
    public async Task Mark_Used_Removes_The_Stock_For_Good()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 5);
        var reservation = await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 2));

        var response = await _s3.Client(_s3.StoreOfficerToken).PutAsync($"/api/stock-reservations/{reservation.Id}/use", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var consumable = await _s3.ReadConsumableAsync(consumableId);
        consumable.StockQuantity.Should().Be(3);
        consumable.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Only_A_Reserved_Reservation_Can_Be_Released()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 5);
        var client = _s3.Client(_s3.StoreOfficerToken);

        // Released already: nothing left to give back.
        var reservation = await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 1));
        (await client.PutAsync($"/api/stock-reservations/{reservation.Id}/release", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsync($"/api/stock-reservations/{reservation.Id}/release", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Pending: a note of intent that never held stock.
        Guid pendingId;
        using (var scope = _s3.Services.CreateScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IConsumableStockService>();
            var pending = await stock.CreatePendingReservationAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 1), CancellationToken.None);
            pending.Succeeded.Should().BeTrue();
            pendingId = pending.Reservation!.Id;
        }
        (await client.PutAsync($"/api/stock-reservations/{pendingId}/release", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Used: the stock has left the store.
        var used = await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 1));
        (await client.PutAsync($"/api/stock-reservations/{used.Id}/use", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsync($"/api/stock-reservations/{used.Id}/release", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var consumable = await _s3.ReadConsumableAsync(consumableId);
        consumable.ReservedQuantity.Should().Be(0);
        consumable.StockQuantity.Should().Be(4);
    }

    [Fact]
    public async Task Unknown_Ids_Return_404()
    {
        var client = _s3.Client(_s3.StoreOfficerToken);

        (await client.PostAsJsonAsync("/api/stock-reservations", new { bookingRequestItemId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsync($"/api/stock-reservations/{Guid.NewGuid()}/release", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsync($"/api/stock-reservations/{Guid.NewGuid()}/use", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_Status_Filter_Returns_400()
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).GetAsync("/api/stock-reservations?status=Held");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reserving_More_Than_Is_Available_Returns_409_And_Changes_Nothing()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 2);
        var itemId = await _s3.SeedBookingRequestItemAsync(consumableId, 3);

        var response = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/stock-reservations", new { bookingRequestItemId = itemId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var consumable = await _s3.ReadConsumableAsync(consumableId);
        consumable.ReservedQuantity.Should().Be(0);
        using var scope = _s3.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        (await db.StockReservations.AnyAsync(r => r.BookingRequestItemId == itemId)).Should().BeFalse();
        (await db.StockTransactions.AnyAsync(t => t.ConsumableId == consumableId)).Should().BeFalse();
    }

    [Fact]
    public async Task Reserving_The_Same_Item_Twice_Returns_409()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 5);
        var itemId = await _s3.SeedBookingRequestItemAsync(consumableId, 1);
        await ReserveAsync(itemId);

        var again = await _s3.Client(_s3.StoreOfficerToken).PostAsJsonAsync("/api/stock-reservations", new { bookingRequestItemId = itemId });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _s3.ReadConsumableAsync(consumableId)).ReservedQuantity.Should().Be(1);
    }

    /// <summary>
    /// Headline claim #2: two callers race for the last units of one consumable. Each call runs in
    /// its own DI scope (its own DbContext and connection), started together behind a barrier, so
    /// the two guarded UPDATEs genuinely overlap in Postgres. Repeated so a lucky serial schedule
    /// cannot pass it by accident.
    /// </summary>
    [Fact]
    public async Task Parallel_Reservations_For_The_Last_Units_Never_Oversell()
    {
        for (var round = 0; round < 10; round++)
        {
            var consumableId = await _s3.SeedConsumableAsync(stock: 3);
            var first = await _s3.SeedBookingRequestItemAsync(consumableId, 2);
            var second = await _s3.SeedBookingRequestItemAsync(consumableId, 2);

            using var start = new Barrier(2);
            async Task<StockOperationResult> Race(Guid itemId)
            {
                await Task.Yield();
                using var scope = _s3.Services.CreateScope();
                var stock = scope.ServiceProvider.GetRequiredService<IConsumableStockService>();
                start.SignalAndWait(TimeSpan.FromSeconds(10));
                return await stock.ReserveAsync(itemId, _s3.StoreOfficerId, CancellationToken.None);
            }

            var results = await Task.WhenAll(Task.Run(() => Race(first)), Task.Run(() => Race(second)));

            results.Count(r => r.Succeeded).Should().Be(1, $"round {round}: exactly one of two 2-unit reservations fits in 3 units");
            results.Should().ContainSingle(r => r.Outcome == StockOperationOutcome.InsufficientStock);

            var consumable = await _s3.ReadConsumableAsync(consumableId);
            consumable.ReservedQuantity.Should().Be(2);
            consumable.StockQuantity.Should().Be(3);
            consumable.ReservedQuantity.Should().BeLessThanOrEqualTo(consumable.StockQuantity);
            consumable.AvailableQuantity.Should().BeGreaterThanOrEqualTo(0);

            using var verify = _s3.Services.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            (await db.StockReservations.CountAsync(r => r.ConsumableId == consumableId && r.Status == StockReservationStatus.Reserved))
                .Should().Be(1, "the loser's transaction rolled back, leaving no reservation row");
            (await db.StockTransactions.CountAsync(t => t.ConsumableId == consumableId && t.TransactionType == StockTransactionType.Reserve))
                .Should().Be(1, "and no ledger row");
        }
    }

    /// <summary>The S4 approval transaction calls ReserveAsync inside its own transaction; EF Core
    /// cannot nest transactions, so the stock service must join the caller's and leave commit and
    /// rollback to it. A rolled-back caller must leave no trace of the reservation.</summary>
    [Fact]
    public async Task Reserve_Joins_A_Callers_Transaction_And_Rolls_Back_With_It()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 5);
        var itemId = await _s3.SeedBookingRequestItemAsync(consumableId, 2);

        using (var scope = _s3.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var stock = scope.ServiceProvider.GetRequiredService<IConsumableStockService>();
            await using var outer = await db.Database.BeginTransactionAsync();

            var result = await stock.ReserveAsync(itemId, _s3.StoreOfficerId, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            db.Database.CurrentTransaction.Should().BeSameAs(outer, "the service must not have committed or replaced the caller's transaction");
            await outer.RollbackAsync();
        }

        (await _s3.ReadConsumableAsync(consumableId)).ReservedQuantity.Should().Be(0);
        using (var scope = _s3.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            (await db.StockReservations.AnyAsync(r => r.BookingRequestItemId == itemId)).Should().BeFalse();
            (await db.StockTransactions.AnyAsync(t => t.ConsumableId == consumableId && t.TransactionType == StockTransactionType.Reserve)).Should().BeFalse();
        }

        // Committed by the caller, the same call sticks.
        using (var scope = _s3.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var stock = scope.ServiceProvider.GetRequiredService<IConsumableStockService>();
            await using var outer = await db.Database.BeginTransactionAsync();
            (await stock.ReserveAsync(itemId, _s3.StoreOfficerId, CancellationToken.None)).Succeeded.Should().BeTrue();
            await outer.CommitAsync();
        }
        (await _s3.ReadConsumableAsync(consumableId)).ReservedQuantity.Should().Be(2);
    }

    [Fact]
    public async Task An_Oversell_Inside_A_Callers_Transaction_Reports_Insufficient_Stock_Without_Committing()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 1);
        var itemId = await _s3.SeedBookingRequestItemAsync(consumableId, 2);

        using (var scope = _s3.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var stock = scope.ServiceProvider.GetRequiredService<IConsumableStockService>();
            await using var outer = await db.Database.BeginTransactionAsync();

            var result = await stock.ReserveAsync(itemId, _s3.StoreOfficerId, CancellationToken.None);

            result.Outcome.Should().Be(StockOperationOutcome.InsufficientStock);
            db.Database.CurrentTransaction.Should().BeSameAs(outer);
            await outer.RollbackAsync();
        }

        (await _s3.ReadConsumableAsync(consumableId)).ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Database_Rejects_A_Direct_Update_That_Would_Oversell()
    {
        var consumableId = await _s3.SeedConsumableAsync(stock: 4);
        using var scope = _s3.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();

        var act = () => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE consumables SET reserved_quantity = stock_quantity + 1 WHERE id = {consumableId}");

        var thrown = await act.Should().ThrowAsync<PostgresException>();
        thrown.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        thrown.Which.ConstraintName.Should().Be("chk_never_oversold");
        (await _s3.ReadConsumableAsync(consumableId)).ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Store_Officer_Consumable_Usage_Report_Reflects_The_Ledger()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var consumableId = await _s3.SeedConsumableAsync(stock: 10, minStockLevel: 8, unitPrice: 25m);
        var client = _s3.Client(_s3.StoreOfficerToken);

        (await client.PostAsJsonAsync($"/api/consumables/{consumableId}/stock-in", new { quantity = 5 })).EnsureSuccessStatusCode();
        var used = await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 4));
        (await client.PutAsync($"/api/stock-reservations/{used.Id}/use", null)).EnsureSuccessStatusCode();
        var released = await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 2));
        (await client.PutAsync($"/api/stock-reservations/{released.Id}/release", null)).EnsureSuccessStatusCode();
        await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 3));
        var to = DateTimeOffset.UtcNow.AddMinutes(1);

        var response = await client.GetAsync(
            $"/api/reports/consumable-usage?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = (await response.Content.ReadFromJsonAsync<ConsumableUsageReportResponse>(TestSupport.JsonOptions))!;
        var row = report.ByItem.Items.Should().ContainSingle(r => r.ConsumableId == consumableId).Subject;
        row.Issued.Should().Be(4);
        row.Cost.Should().Be(100m);
        row.Reserved.Should().Be(9);
        row.Released.Should().Be(2);
        row.StockedIn.Should().Be(5);
        row.StockQuantity.Should().Be(11);
        row.ReservedNow.Should().Be(3);
        row.AvailableQuantity.Should().Be(8);
        row.IsLowStock.Should().BeFalse("11 on hand is above the minimum of 8");
        report.TotalIssued.Should().BeGreaterThanOrEqualTo(4);
        report.LowStock.Should().NotContain(l => l.ConsumableId == consumableId);

        // Issue enough to fall to the minimum and it joins the low-stock list.
        (await client.PutAsync($"/api/stock-reservations/{(await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, 3))).Id}/use", null))
            .EnsureSuccessStatusCode();
        var afterIssue = await client.GetFromJsonAsync<ConsumableUsageReportResponse>("/api/reports/consumable-usage", TestSupport.JsonOptions);
        afterIssue!.LowStock.Should().Contain(l => l.ConsumableId == consumableId && l.StockQuantity == 8);
    }

    [Fact]
    public async Task Consumable_Usage_Report_Sorts_And_Pages_In_The_Database()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var client = _s3.Client(_s3.StoreOfficerToken);
        // Far more units than any other test issues, so these two lead every "issued"/"cost" sort.
        var cheap = await _s3.SeedConsumableAsync(stock: 60, unitPrice: 5m);
        var dear = await _s3.SeedConsumableAsync(stock: 60, unitPrice: 10m);
        foreach (var (consumableId, quantity) in new[] { (cheap, 50), (dear, 40) })
        {
            var reservation = await ReserveAsync(await _s3.SeedBookingRequestItemAsync(consumableId, quantity));
            (await client.PutAsync($"/api/stock-reservations/{reservation.Id}/use", null)).EnsureSuccessStatusCode();
        }
        var range = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"))}";

        async Task<PagedResultShape<ConsumableUsageRowResponse>> Page(string extra)
        {
            var report = await client.GetFromJsonAsync<ConsumableUsageReportResponse>(
                $"/api/reports/consumable-usage?{range}&{extra}", TestSupport.JsonOptions);
            return new PagedResultShape<ConsumableUsageRowResponse>
            {
                Items = report!.ByItem.Items.ToList(),
                Page = report.ByItem.Page,
                PageSize = report.ByItem.PageSize,
                TotalItems = report.ByItem.TotalItems,
            };
        }

        var first = await Page("sortBy=issued&pageSize=1&page=1");
        first.Items.Should().ContainSingle().Which.ConsumableId.Should().Be(cheap);
        first.TotalItems.Should().BeGreaterThanOrEqualTo(2);
        first.PageSize.Should().Be(1);

        var second = await Page("sortBy=issued&pageSize=1&page=2");
        second.Page.Should().Be(2);
        second.Items.Should().ContainSingle().Which.ConsumableId.Should().Be(dear);
        // Other test classes create active consumables concurrently, so the count may only grow.
        second.TotalItems.Should().BeGreaterThanOrEqualTo(first.TotalItems);

        var byCost = await Page("sortBy=cost&pageSize=2");
        byCost.Items.Select(r => r.ConsumableId).Should().Equal(dear, cheap);
        byCost.Items.Select(r => r.Cost).Should().Equal(400m, 250m);

        // The last ascending page moves whenever a concurrent test adds a consumable between the two
        // reads, so re-read until the page asked for is still the last one in its own response.
        PagedResultShape<ConsumableUsageRowResponse> ascending = null!;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var lastPage = (await Page("sortBy=issued&sortDir=asc&pageSize=1&page=1")).TotalItems;
            ascending = await Page($"sortBy=issued&sortDir=asc&pageSize=1&page={lastPage}");
            if (ascending.TotalItems == lastPage) break;
        }
        ascending.Items.Should().ContainSingle().Which.ConsumableId.Should().Be(cheap, "the largest issue sorts last ascending");
    }

    [Fact]
    public async Task Consumable_Usage_Report_Is_Store_Officer_Only()
    {
        (await _s3.Client(null).GetAsync("/api/reports/consumable-usage")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _s3.Client(_s3.StudentToken).GetAsync("/api/reports/consumable-usage")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _s3.Client(_s3.LibrarianToken).GetAsync("/api/reports/consumable-usage")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("from=2030-01-02T00:00:00Z&to=2030-01-01T00:00:00Z")]
    [InlineData("from=2030-01-01T00:00:00Z&to=2030-01-01T00:00:00Z")]
    [InlineData("sortBy=secretColumn")]
    [InlineData("from=not-a-date")]
    public async Task Consumable_Usage_Report_Rejects_A_Bad_Query_With_400(string queryString)
    {
        var response = await _s3.Client(_s3.StoreOfficerToken).GetAsync($"/api/reports/consumable-usage?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
