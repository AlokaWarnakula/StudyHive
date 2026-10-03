using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudyHive.Api.Contracts;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>
/// AUDIT A2: the weekly limit counts distinct requests and never the one being submitted (C-03),
/// and a request sent back with "ask for a change" can be edited and resent (C-02).
/// </summary>
public sealed class EligibilityAndRevisionTests : IAsyncLifetime
{
    private readonly Guid roomId = Guid.NewGuid();
    private readonly Guid consumableId = Guid.NewGuid();
    private readonly List<Guid> userIds = [];
    private readonly WebApplicationFactory<Program> factory;
    private string librarianToken = "";

    public EligibilityAndRevisionTests()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlannerClient>();
                services.AddSingleton<IPlannerClient>(new FakePlannerClient());
                services.RemoveAll<ISchedulingAgentClient>();
                services.AddSingleton<ISchedulingAgentClient>(new FakeSchedulingAgentClient
                {
                    OnPropose = request =>
                    {
                        var room = request.Rooms.Single(r => r.RoomId == roomId);
                        var startsAt = new DateTimeOffset(request.PreferredDateFrom.ToDateTime(request.PreferredTimeFrom), TimeSpan.FromMinutes(330));
                        return new SchedulingResponse
                        {
                            Slots =
                            [
                                new SchedulingSlot
                                {
                                    RoomId = room.RoomId, RoomName = room.RoomName, StartsAt = startsAt,
                                    EndsAt = startsAt.AddMinutes(request.SessionDurationMinutes), HourlyRate = room.HourlyRate,
                                },
                            ],
                            Conflicts = [],
                        };
                    },
                });
                services.RemoveAll<IResourceClient>();
                services.AddSingleton<IResourceClient>(new FakeResourceClient());
                services.RemoveAll<IValidationClient>();
                services.AddSingleton<IValidationClient>(new FakeValidationClient());
            }));
    }

    public async Task InitializeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = Db(scope);
            var now = DateTimeOffset.UtcNow;
            db.StudyRooms.Add(new StudyRoom
            {
                Id = roomId, Name = $"Revision test room {Guid.NewGuid():N}", Building = "Test building", Floor = 1,
                Capacity = 20, HourlyRate = 10m, QrCode = $"revision-test-{Guid.NewGuid():N}", IsActive = true,
                CreatedAt = now, UpdatedAt = now,
            });
            db.Consumables.Add(new Consumable
            {
                Id = consumableId, Name = $"Revision test item {Guid.NewGuid():N}", Unit = "pcs", UnitPrice = 1m,
                StockQuantity = 50, CreatedAt = now, UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var (librarianId, _, token) = await TestSupport.CreateAndLoginStaffAsync(factory, factory.CreateClient(), UserRole.Librarian);
        userIds.Add(librarianId);
        librarianToken = token;
    }

    public async Task DisposeAsync()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = Db(scope);
            await db.AuditLogs.Where(a => a.UserId != null && userIds.Contains(a.UserId.Value)).ExecuteDeleteAsync();
            await db.ApprovalDecisions.Where(d => userIds.Contains(d.DecidedBy)).ExecuteDeleteAsync();
            await db.StockTransactions.Where(t => t.ConsumableId == consumableId).ExecuteDeleteAsync();
            await db.StockReservations.Where(r => r.ConsumableId == consumableId).ExecuteDeleteAsync();
            await db.RoomBookings.Where(b => b.RoomId == roomId).ExecuteDeleteAsync();
        }
        await TestSupport.CleanupAsync(factory, userIds.ToArray());
        using (var scope = factory.Services.CreateScope())
        {
            var db = Db(scope);
            await db.Consumables.Where(c => c.Id == consumableId).ExecuteDeleteAsync();
            await db.StudyRooms.Where(r => r.Id == roomId).ExecuteDeleteAsync();
        }
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task The_Third_Request_Of_The_Week_Reaches_PendingApproval_And_The_Fourth_Is_Refused_At_Submit()
    {
        var (client, profileId) = await NewStudentAsync();
        await SeedSubmittedRequestsAsync(profileId, count: 2);

        var third = await CreateAsync(client, dayOffset: 3);
        (await client.PostAsync($"/api/booking-requests/{third}/submit", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await WaitForWorkflowAsync(client, third)).Should().Be("PendingApproval", "the request being run must not count against itself");

        var fourth = await CreateAsync(client, dayOffset: 4);
        var refused = await client.PostAsync($"/api/booking-requests/{fourth}/submit", null);
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Weekly booking limit reached (3 per week).");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Revision_Can_Be_Resent_As_Quotation_Version_2_Without_Using_Another_Weekly_Slot(bool editFirst)
    {
        var (client, profileId) = await NewStudentAsync();
        await SeedSubmittedRequestsAsync(profileId, count: 2);
        var requestId = await CreateAsync(client, dayOffset: 5, quantity: 3);
        (await client.PostAsync($"/api/booking-requests/{requestId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await WaitForWorkflowAsync(client, requestId)).Should().Be("PendingApproval");
        var v1 = await QuotationIdAsync(requestId, version: 1);
        var librarian = Client(librarianToken);
        (await librarian.PostAsJsonAsync("/api/approvals", new { quotationId = v1, decision = "RevisionRequested", comments = "Use fewer markers." }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        if (editFirst)
        {
            var edit = await client.PutAsJsonAsync($"/api/booking-requests/{requestId}", Body(dayOffset: 5, quantity: 1));
            edit.StatusCode.Should().Be(HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());
            (await edit.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions))!.Status.Should().Be("Draft");
        }

        // Already 3 requests this week: resending must not count as a 4th.
        var resend = await client.PostAsync($"/api/booking-requests/{requestId}/submit", null);
        resend.StatusCode.Should().Be(HttpStatusCode.Accepted, await resend.Content.ReadAsStringAsync());
        (await WaitForWorkflowAsync(client, requestId)).Should().Be("PendingApproval");

        using var scope = factory.Services.CreateScope();
        var db = Db(scope);
        var quotations = await db.Quotations.AsNoTracking().Where(q => q.BookingRequestId == requestId).OrderBy(q => q.Version).ToListAsync();
        quotations.Select(q => (q.Version, q.Status)).Should().Equal((1, QuotationStatus.Superseded), (2, QuotationStatus.Proposed));
        var reservation = await db.StockReservations.AsNoTracking().SingleAsync(r => r.ConsumableId == consumableId
            && db.BookingRequestItems.Any(i => i.Id == r.BookingRequestItemId && i.BookingRequestId == requestId));
        reservation.Status.Should().Be(StockReservationStatus.Pending);
        reservation.Quantity.Should().Be(editFirst ? 1 : 3);

        // The librarian can approve version 2: its Pending note is promoted to Reserved.
        (await librarian.PostAsJsonAsync("/api/approvals", new { quotationId = quotations[1].Id, decision = "Approved" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Only_Draft_Or_RevisionRequested_Requests_Can_Be_Edited_Or_Submitted()
    {
        var (client, _) = await NewStudentAsync();
        var requestId = await CreateAsync(client, dayOffset: 6);
        (await client.PostAsync($"/api/booking-requests/{requestId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await WaitForWorkflowAsync(client, requestId)).Should().Be("PendingApproval");

        (await client.PutAsJsonAsync($"/api/booking-requests/{requestId}", Body(dayOffset: 6))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsync($"/api/booking-requests/{requestId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Two_Simultaneous_Submits_Of_One_Request_Start_Exactly_One_Workflow()
    {
        var (client, _) = await NewStudentAsync();
        var requestId = await CreateAsync(client, dayOffset: 7);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => client.PostAsync($"/api/booking-requests/{requestId}/submit", null)));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Accepted, HttpStatusCode.Conflict]);
        using var scope = factory.Services.CreateScope();
        (await Db(scope).WorkflowExecutions.CountAsync(w => w.BookingRequestId == requestId)).Should().Be(1);
    }

    private static StudyHiveDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();

    private HttpClient Client(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(HttpClient Client, Guid ProfileId)> NewStudentAsync()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, token);
        return (Client(token), profile.Id);
    }

    /// <summary>Requests this student already submitted this week: one workflow row each.</summary>
    private async Task SeedSubmittedRequestsAsync(Guid profileId, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = Db(scope);
        var now = DateTimeOffset.UtcNow;
        for (var n = 0; n < count; n++)
        {
            var request = new BookingRequest
            {
                Id = Guid.NewGuid(), StudentId = profileId, Objective = $"Earlier request {n}", GroupSize = 2,
                PreferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), PreferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                PreferredTimeFrom = new TimeOnly(9, 0), PreferredTimeTo = new TimeOnly(10, 0), SessionsRequired = 1,
                SessionDurationMinutes = 60, Budget = 100m, Status = BookingRequestStatus.Rejected, CreatedAt = now, UpdatedAt = now,
            };
            db.BookingRequests.Add(request);
            db.WorkflowExecutions.Add(new WorkflowExecution
            {
                Id = Guid.NewGuid(), BookingRequestId = request.Id, Objective = request.Objective,
                Status = WorkflowStatus.Rejected, StartedAt = now, UpdatedAt = now,
            });
        }
        await db.SaveChangesAsync();
    }

    private object Body(int dayOffset, int quantity = 2)
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dayOffset));
        return new
        {
            objective = "Revision path study session",
            groupSize = 3,
            preferredDateFrom = day,
            preferredDateTo = day,
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 60,
            budget = 1000m,
            notes = (string?)null,
            items = new[] { new { consumableId, quantity } },
        };
    }

    private async Task<Guid> CreateAsync(HttpClient client, int dayOffset, int quantity = 2)
    {
        var created = await client.PostAsJsonAsync("/api/booking-requests", Body(dayOffset, quantity));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions))!.Id;
    }

    private async Task<Guid> QuotationIdAsync(Guid requestId, int version)
    {
        using var scope = factory.Services.CreateScope();
        return await Db(scope).Quotations.Where(q => q.BookingRequestId == requestId && q.Version == version).Select(q => q.Id).SingleAsync();
    }

    private static async Task<string> WaitForWorkflowAsync(HttpClient client, Guid requestId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<BookingRequestResponseShape>($"/api/booking-requests/{requestId}", TestSupport.JsonOptions);
            if (status!.Status is "PendingApproval" or "Rejected" or "Failed") return status.Status;
            await Task.Delay(150);
        }
        throw new TimeoutException($"Request {requestId} did not leave the workflow within 10 s.");
    }
}
