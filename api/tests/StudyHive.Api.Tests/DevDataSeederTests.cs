using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

public class DevDataSeederTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Development_Seed_Is_Complete_And_Idempotent()
    {
        _ = factory.CreateClient(); // Start the Development host and run its seed pass.

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();

        var student = await db.StudentProfiles
            .Include(p => p.User)
            .SingleAsync(p => p.User.Email == "student@studyhive.dev");
        student.Department.Should().Be("Computing");

        var expectedStatuses = new[]
        {
            BookingRequestStatus.Draft,
            BookingRequestStatus.Processing,
            BookingRequestStatus.PendingApproval,
            BookingRequestStatus.Approved,
            BookingRequestStatus.Rejected,
            BookingRequestStatus.Cancelled,
            BookingRequestStatus.Completed,
            BookingRequestStatus.Failed,
        };
        var requestIds = Enumerable.Range(1, 8)
            .Select(number => Guid.Parse($"10000000-0000-0000-0000-{number:D12}"))
            .ToArray();
        var workflowIds = new[] { 2, 3, 4, 5, 7, 8 }
            .Select(number => Guid.Parse($"20000000-0000-0000-0000-{number:D12}"))
            .ToArray();
        var stepIds = new[] { (2, 2), (3, 4), (4, 4), (5, 4), (7, 4), (8, 2) }
            .SelectMany(pair => Enumerable.Range(1, pair.Item2)
                .Select(step => Guid.Parse($"500000{pair.Item1:D2}-0000-0000-0000-{step:D12}")))
            .ToArray();
        var requests = await db.BookingRequests
            .Where(r => r.StudentId == student.Id && requestIds.Contains(r.Id))
            .ToListAsync();
        requests.Select(r => r.Status).Should().BeEquivalentTo(expectedStatuses);

        var consumableIds = new[]
        {
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Guid.Parse("30000000-0000-0000-0000-000000000002"),
            Guid.Parse("30000000-0000-0000-0000-000000000003"),
        };
        var consumables = await db.Consumables
            .Where(c => consumableIds.Contains(c.Id))
            .OrderBy(c => c.Id)
            .ToListAsync();
        consumables.Should().HaveCount(3);
        consumables[0].Should().BeEquivalentTo(new
        {
            Name = "Whiteboard markers",
            Unit = "marker",
            UnitPrice = 60m,
            StockQuantity = 42,
            MinStockLevel = 10,
        });
        consumables[1].Should().BeEquivalentTo(new
        {
            Name = "A4 printouts",
            Unit = "page",
            UnitPrice = 5m,
            StockQuantity = 1200,
            MinStockLevel = 200,
        });
        consumables[2].Should().BeEquivalentTo(new
        {
            Name = "HDMI cable",
            Unit = "cable",
            UnitPrice = 0m,
            StockQuantity = 0,
            MinStockLevel = 2,
        });
        (await db.WorkflowExecutions.CountAsync(w => workflowIds.Contains(w.Id))).Should().Be(6);
        (await db.WorkflowStepLogs.CountAsync(s => stepIds.Contains(s.Id))).Should().Be(20);

        consumables[0].UnitPrice = 1m;
        consumables[0].StockQuantity = 1;
        await db.SaveChangesAsync();

        await DevDataSeeder.SeedAsync(scope.ServiceProvider);
        db.ChangeTracker.Clear();

        (await db.BookingRequests.CountAsync(r => requestIds.Contains(r.Id))).Should().Be(8);
        (await db.WorkflowExecutions.CountAsync(w => workflowIds.Contains(w.Id))).Should().Be(6);
        (await db.WorkflowStepLogs.CountAsync(s => stepIds.Contains(s.Id))).Should().Be(20);
        var reconciledMarkers = await db.Consumables.SingleAsync(c => c.Id == consumableIds[0]);
        reconciledMarkers.UnitPrice.Should().Be(60m);
        reconciledMarkers.StockQuantity.Should().Be(42);
    }
}
