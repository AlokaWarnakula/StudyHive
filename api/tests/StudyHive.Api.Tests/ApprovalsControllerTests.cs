using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// Class fixture for the S4 approval tests. Every role logs in once per class (login is rate-limited
/// to 30/minute), agent calls are faked, and every row a test creates is recorded so
/// <see cref="DisposeAsync"/> removes it in foreign-key order.
/// </summary>
public sealed class ApprovalsFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions LogJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly WebApplicationFactory<Program> factory;

    public ApprovalsFixture()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlannerClient>();
                services.AddSingleton<IPlannerClient>(new FakePlannerClient());
                services.RemoveAll<ISchedulingAgentClient>();
                services.AddSingleton<ISchedulingAgentClient>(new FakeSchedulingAgentClient
                {
                    // The end-to-end test pins scheduling to its own room so other rooms can't win.
                    OnPropose = request =>
                    {
                        var room = request.Rooms.Single(r => r.RoomId == PinnedRoomId);
                        var startsAt = new DateTimeOffset(
                            request.PreferredDateFrom.ToDateTime(request.PreferredTimeFrom), TimeSpan.FromMinutes(330));
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

    public IServiceProvider Services => factory.Services;

    public Guid PinnedRoomId { get; set; }

    public readonly List<Guid> UserIds = [];
    public readonly List<Guid> RoomIds = [];
    public readonly List<Guid> ConsumableIds = [];

    public string LibrarianToken { get; private set; } = "";
    public Guid LibrarianId { get; private set; }
    public string StudentToken { get; private set; } = "";
    public Guid StudentProfileId { get; private set; }
    public string StoreOfficerToken { get; private set; } = "";
    public string AdminToken { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var client = factory.CreateClient();

        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        UserIds.Add(librarianId);
        LibrarianId = librarianId;
        LibrarianToken = librarianToken;

        var (storeOfficerId, _, storeOfficerToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.StoreOfficer);
        UserIds.Add(storeOfficerId);
        StoreOfficerToken = storeOfficerToken;

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        UserIds.Add(adminId);
        AdminToken = adminToken;

        var (student, _, studentToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        UserIds.Add(student.Id);
        StudentToken = studentToken;
        StudentProfileId = (await TestSupport.CreateStudentProfileAsync(client, studentToken)).Id;
    }

    /// <summary>A second student for the end-to-end test: every seeded proposal belongs to the
    /// fixture's student and counts against their weekly booking limit.</summary>
    public async Task<string> CreateFreshStudentAsync()
    {
        var client = factory.CreateClient();
        var (student, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        UserIds.Add(student.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        return token;
    }

    public HttpClient Client(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public StudyHiveDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();

    public async Task<Guid> SeedRoomAsync(decimal hourlyRate = 100m)
    {
        using var scope = Services.CreateScope();
        var db = Db(scope);
        var now = DateTimeOffset.UtcNow;
        var room = new StudyRoom
        {
            Id = Guid.NewGuid(),
            Name = $"Approval test room {Guid.NewGuid():N}",
            Building = "Test building",
            Floor = 1,
            Capacity = 20,
            HourlyRate = hourlyRate,
            QrCode = $"approval-test-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.StudyRooms.Add(room);
        await db.SaveChangesAsync();
        RoomIds.Add(room.Id);
        return room.Id;
    }

    public async Task<Guid> SeedConsumableAsync(int stock, decimal unitPrice)
    {
        using var scope = Services.CreateScope();
        var db = Db(scope);
        var now = DateTimeOffset.UtcNow;
        var consumable = new Consumable
        {
            Id = Guid.NewGuid(),
            Name = $"Approval test item {Guid.NewGuid():N}",
            Unit = "pcs",
            UnitPrice = unitPrice,
            StockQuantity = stock,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Consumables.Add(consumable);
        await db.SaveChangesAsync();
        ConsumableIds.Add(consumable.Id);
        return consumable.Id;
    }

    /// <summary>
    /// Seeds exactly what a successful workflow leaves behind: a PendingApproval request with its
    /// items and Pending reservations, a PendingApproval workflow whose step-4 log carries the
    /// validated proposal (slots), and a Proposed quotation with one Room line per slot (room_id,
    /// no booking yet) and one Consumable line per item.
    /// </summary>
    public async Task<Proposal> SeedProposalAsync(
        Guid roomId, int sessions, IReadOnlyList<(Guid ConsumableId, int Quantity)> items, int dayOffset)
    {
        using var scope = Services.CreateScope();
        var db = Db(scope);
        var now = DateTimeOffset.UtcNow;
        var room = await db.StudyRooms.AsNoTracking().SingleAsync(r => r.Id == roomId);
        var consumables = await db.Consumables.AsNoTracking()
            .Where(c => items.Select(i => i.ConsumableId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);
        var firstDay = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(dayOffset));

        var request = new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = StudentProfileId,
            Objective = $"Approval test {Guid.NewGuid():N}",
            GroupSize = 4,
            PreferredDateFrom = firstDay,
            PreferredDateTo = firstDay.AddDays(sessions - 1),
            PreferredTimeFrom = new TimeOnly(9, 0),
            PreferredTimeTo = new TimeOnly(12, 0),
            SessionsRequired = sessions,
            SessionDurationMinutes = 90,
            Budget = 10_000m,
            Status = BookingRequestStatus.PendingApproval,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.BookingRequests.Add(request);

        var itemRows = items.Select(i => new BookingRequestItem
        {
            Id = Guid.NewGuid(),
            BookingRequestId = request.Id,
            ConsumableId = i.ConsumableId,
            Quantity = i.Quantity,
            CreatedAt = now,
        }).ToList();
        db.BookingRequestItems.AddRange(itemRows);
        db.StockReservations.AddRange(itemRows.Select(i => new StockReservation
        {
            Id = Guid.NewGuid(),
            BookingRequestItemId = i.Id,
            ConsumableId = i.ConsumableId,
            Quantity = i.Quantity,
            Status = StockReservationStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        }));

        var slots = Enumerable.Range(0, sessions).Select(n =>
        {
            var startsAt = new DateTimeOffset(firstDay.AddDays(n).ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromMinutes(330));
            return new SchedulingSlot
            {
                RoomId = room.Id, RoomName = room.Name, StartsAt = startsAt, EndsAt = startsAt.AddMinutes(90), HourlyRate = room.HourlyRate,
            };
        }).ToList();
        var validationRequest = new ValidationRequest
        {
            Objective = request.Objective,
            GroupSize = request.GroupSize,
            Budget = request.Budget,
            SessionsRequired = sessions,
            SessionDurationMinutes = 90,
            ProposedSlots = slots,
            Rooms =
            [
                new SchedulingRoom
                {
                    RoomId = room.Id, RoomName = room.Name, Capacity = room.Capacity, HourlyRate = room.HourlyRate,
                    IsActive = true, EquipmentTypeIds = [], Bookings = [], MaintenanceWindows = [],
                },
            ],
            Items = itemRows.Select(i => new ResourceRequestItem
            {
                ConsumableId = i.ConsumableId,
                Name = consumables[i.ConsumableId].Name,
                Requested = i.Quantity,
                Available = consumables[i.ConsumableId].StockQuantity,
                UnitPrice = consumables[i.ConsumableId].UnitPrice,
            }).ToList(),
        };
        var priced = FakeValidationClient.Price(validationRequest).Quotation;

        var execution = new WorkflowExecution
        {
            Id = Guid.NewGuid(),
            BookingRequestId = request.Id,
            Objective = request.Objective,
            Status = WorkflowStatus.PendingApproval,
            CurrentStep = 4,
            TotalSteps = 4,
            StartedAt = now,
            UpdatedAt = now,
        };
        db.WorkflowExecutions.Add(execution);
        db.WorkflowStepLogs.Add(new WorkflowStepLog
        {
            Id = Guid.NewGuid(),
            WorkflowExecutionId = execution.Id,
            StepNumber = 4,
            AgentName = "Validation",
            ToolName = "calculate_quotation",
            InputJson = JsonSerializer.Serialize(validationRequest, LogJsonOptions),
            ValidationResult = StepValidationResult.Pass,
            CreatedAt = now,
        });

        var quotation = new Quotation
        {
            Id = Guid.NewGuid(),
            BookingRequestId = request.Id,
            RoomFee = priced.RoomFee,
            ConsumableCost = priced.ConsumableCost,
            BudgetSnapshot = request.Budget,
            Status = QuotationStatus.Proposed,
            CreatedAt = now,
            UpdatedAt = now,
            LineItems = priced.LineItems.Select(l => new QuotationLineItem
            {
                Id = Guid.NewGuid(),
                ItemType = Enum.Parse<QuotationLineItemType>(l.ItemType),
                RoomId = l.RoomId,
                ConsumableId = l.ConsumableId,
                ItemName = l.ItemName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                CreatedAt = now,
            }).ToList(),
        };
        db.Quotations.Add(quotation);
        await db.SaveChangesAsync();

        return new Proposal(quotation.Id, request.Id, execution.Id, roomId, slots, itemRows.Select(i => i.Id).ToList());
    }

    public async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = Db(scope);
            var requestIds = await db.BookingRequests
                .Where(r => db.StudentProfiles.Any(p => p.Id == r.StudentId && UserIds.Contains(p.UserId)))
                .Select(r => r.Id)
                .ToListAsync();
            var quotationIds = await db.Quotations.Where(q => requestIds.Contains(q.BookingRequestId)).Select(q => q.Id).ToListAsync();

            await db.AuditLogs.Where(a => quotationIds.Contains(a.EntityId) || (a.UserId != null && UserIds.Contains(a.UserId.Value))).ExecuteDeleteAsync();
            await db.ApprovalDecisions.Where(d => quotationIds.Contains(d.QuotationId)).ExecuteDeleteAsync();
            await db.Quotations.Where(q => quotationIds.Contains(q.Id)).ExecuteDeleteAsync();
            await db.StockTransactions.Where(t => ConsumableIds.Contains(t.ConsumableId) || UserIds.Contains(t.CreatedBy)).ExecuteDeleteAsync();
            await db.StockReservations.Where(r => ConsumableIds.Contains(r.ConsumableId)).ExecuteDeleteAsync();
            await db.RoomBookings.Where(b => RoomIds.Contains(b.RoomId) || requestIds.Contains(b.BookingRequestId)).ExecuteDeleteAsync();
        }

        await TestSupport.CleanupAsync(factory, UserIds.ToArray());

        using (var scope = Services.CreateScope())
        {
            var db = Db(scope);
            await db.Consumables.Where(c => ConsumableIds.Contains(c.Id)).ExecuteDeleteAsync();
            await db.StudyRooms.Where(r => RoomIds.Contains(r.Id)).ExecuteDeleteAsync();
        }

        await factory.DisposeAsync();
    }
}

public sealed record Proposal(
    Guid QuotationId,
    Guid RequestId,
    Guid WorkflowId,
    Guid RoomId,
    IReadOnlyList<SchedulingSlot> Slots,
    IReadOnlyList<Guid> ItemIds);

public class ApprovalsControllerTests(ApprovalsFixture fx) : IClassFixture<ApprovalsFixture>
{
    // Each test books its own room on its own days, so the tests never clash with each other.
    private static int nextDayOffset = 30;
    private static int NextDays(int sessions) => Interlocked.Add(ref nextDayOffset, sessions + 1);

    private static object Decision(Guid quotationId, string decision, string? comments = null) =>
        new { quotationId, decision, comments };

    private Task<HttpResponseMessage> DecideAsync(Guid quotationId, string decision, string? comments = null, string? token = null) =>
        fx.Client(token ?? fx.LibrarianToken).PostAsJsonAsync("/api/approvals", Decision(quotationId, decision, comments));

    [Fact]
    public async Task Approve_Books_Every_Slot_And_Reserves_Every_Item_In_One_Transaction()
    {
        var roomId = await fx.SeedRoomAsync(hourlyRate: 100m);
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1.50m);
        var paper = await fx.SeedConsumableAsync(stock: 5, unitPrice: 0.25m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 2, [(markers, 4), (paper, 5)], NextDays(2));

        var response = await DecideAsync(proposal.QuotationId, "Approved");

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApprovalDecisionShape>(TestSupport.JsonOptions);
        body!.Decision.Should().Be("Approved");
        body.QuotationId.Should().Be(proposal.QuotationId);
        body.DecidedBy.Should().Be(fx.LibrarianId);
        body.DecidedByRole.Should().Be("Librarian");

        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);

        var quotation = await db.Quotations.AsNoTracking().Include(q => q.LineItems).SingleAsync(q => q.Id == proposal.QuotationId);
        quotation.Status.Should().Be(QuotationStatus.Approved);
        quotation.TotalAmount.Should().Be(quotation.LineItems.Sum(l => l.LineTotal));
        (await db.BookingRequests.SingleAsync(r => r.Id == proposal.RequestId)).Status.Should().Be(BookingRequestStatus.Approved);
        var workflow = await db.WorkflowExecutions.SingleAsync(w => w.Id == proposal.WorkflowId);
        workflow.Status.Should().Be(WorkflowStatus.Approved);
        workflow.CompletedAt.Should().NotBeNull();

        var bookings = await db.RoomBookings.AsNoTracking().Where(b => b.BookingRequestId == proposal.RequestId).OrderBy(b => b.StartsAt).ToListAsync();
        bookings.Should().HaveCount(2);
        bookings.Should().OnlyContain(b => b.RoomId == roomId && b.Status == RoomBookingStatus.Confirmed);
        bookings.Select(b => b.StartsAt).Should().Equal(proposal.Slots.Select(s => s.StartsAt));

        var roomLines = quotation.LineItems.Where(l => l.ItemType == QuotationLineItemType.Room).ToList();
        roomLines.Select(l => l.RoomBookingId).Should().BeEquivalentTo(bookings.Select(b => (Guid?)b.Id));

        var reservations = await db.StockReservations.AsNoTracking().Where(r => proposal.ItemIds.Contains(r.BookingRequestItemId)).ToListAsync();
        reservations.Should().HaveCount(2).And.OnlyContain(r => r.Status == StockReservationStatus.Reserved && r.ReservedAt != null);
        (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == markers)).ReservedQuantity.Should().Be(4);
        (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == paper)).ReservedQuantity.Should().Be(5, "the last 5 of 5 are exactly reservable");
        (await db.StockTransactions.CountAsync(t => t.BookingRequestId == proposal.RequestId && t.TransactionType == StockTransactionType.Reserve))
            .Should().Be(2);

        var decision = await db.ApprovalDecisions.AsNoTracking().SingleAsync(d => d.QuotationId == proposal.QuotationId);
        decision.Decision.Should().Be(ApprovalDecisionType.Approved);
        var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityId == proposal.QuotationId);
        audit.Action.Should().Be("QuotationApproved");
        audit.EntityType.Should().Be("Quotation");
        audit.UserId.Should().Be(fx.LibrarianId);
        audit.Details.Should().Contain(bookings[0].Id.ToString()).And.Contain(reservations[0].Id.ToString());

        await AssertOneQueuedEmailAsync(db, proposal.RequestId, "BookingApproved", "Your StudyHive booking is approved");
    }

    [Fact]
    public async Task A_Room_Clash_Returns_409_And_Writes_Nothing()
    {
        var roomId = await fx.SeedRoomAsync();
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 2, [(markers, 3)], NextDays(2));

        // Someone else books the SECOND slot first — so the first slot's booking is created inside
        // the transaction before the clash, and must be rolled back with everything else.
        await SeedForeignBookingAsync(roomId, proposal.Slots[1].StartsAt.AddMinutes(30), proposal.Slots[1].EndsAt.AddMinutes(30));

        var response = await DecideAsync(proposal.QuotationId, "Approved");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("room-conflict");
        await AssertNothingWrittenAsync(proposal, markers);
    }

    [Fact]
    public async Task An_Oversell_Returns_409_And_Rolls_Back_The_Room_Bookings()
    {
        var roomId = await fx.SeedRoomAsync();
        var plenty = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var scarce = await fx.SeedConsumableAsync(stock: 2, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [(plenty, 3), (scarce, 3)], NextDays(1));

        var response = await DecideAsync(proposal.QuotationId, "Approved");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("insufficient-stock");
        await AssertNothingWrittenAsync(proposal, plenty, scarce);
    }

    [Fact]
    public async Task A_Second_Decision_On_The_Same_Quotation_Returns_409()
    {
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));

        (await DecideAsync(proposal.QuotationId, "Approved")).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await DecideAsync(proposal.QuotationId, "Rejected", "Changed my mind");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Content.ReadAsStringAsync()).Should().Contain("already-decided");
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        (await db.ApprovalDecisions.CountAsync(d => d.QuotationId == proposal.QuotationId)).Should().Be(1);
        (await db.Quotations.SingleAsync(q => q.Id == proposal.QuotationId)).Status.Should().Be(QuotationStatus.Approved);
        await AssertOneQueuedEmailAsync(db, proposal.RequestId, "BookingApproved", "Your StudyHive booking is approved");
    }

    [Fact]
    public async Task Concurrent_Approvals_Of_The_Same_Quotation_Commit_Exactly_Once()
    {
        var roomId = await fx.SeedRoomAsync();
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [(markers, 4)], NextDays(1));

        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => DecideAsync(proposal.QuotationId, "Approved")));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(2);
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        (await db.RoomBookings.CountAsync(b => b.BookingRequestId == proposal.RequestId)).Should().Be(1);
        (await db.ApprovalDecisions.CountAsync(d => d.QuotationId == proposal.QuotationId)).Should().Be(1);
        (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == markers)).ReservedQuantity.Should().Be(4);
        (await db.EmailNotifications.CountAsync(e => e.BookingRequestId == proposal.RequestId)).Should().Be(1, "only the committed decision queues an email");
    }

    [Theory]
    [InlineData("Rejected", BookingRequestStatus.Rejected, "QuotationRejected", "BookingRejected", "Your StudyHive booking request was rejected")]
    [InlineData("RevisionRequested", BookingRequestStatus.RevisionRequested, "RevisionRequested", "BookingRevisionRequested", "Your StudyHive booking request needs changes")]
    public async Task Reject_Or_Revision_Updates_Statuses_Releases_Pending_Stock_And_Books_Nothing(
        string decision, BookingRequestStatus expectedRequestStatus, string expectedAuditAction, string expectedTemplate, string expectedSubject)
    {
        var roomId = await fx.SeedRoomAsync();
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [(markers, 4)], NextDays(1));

        var response = await DecideAsync(proposal.QuotationId, decision, "Please use a smaller room.");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        (await db.Quotations.SingleAsync(q => q.Id == proposal.QuotationId)).Status.Should().Be(QuotationStatus.Rejected);
        (await db.BookingRequests.SingleAsync(r => r.Id == proposal.RequestId)).Status.Should().Be(expectedRequestStatus);
        (await db.WorkflowExecutions.SingleAsync(w => w.Id == proposal.WorkflowId)).Status.Should().Be(WorkflowStatus.Rejected);
        (await db.RoomBookings.AnyAsync(b => b.BookingRequestId == proposal.RequestId)).Should().BeFalse();
        (await db.StockReservations.SingleAsync(r => r.BookingRequestItemId == proposal.ItemIds[0])).Status
            .Should().Be(StockReservationStatus.Released);
        (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == markers)).ReservedQuantity.Should().Be(0);
        var decisionRow = await db.ApprovalDecisions.SingleAsync(d => d.QuotationId == proposal.QuotationId);
        decisionRow.Comments.Should().Be("Please use a smaller room.");
        (await db.AuditLogs.SingleAsync(a => a.EntityId == proposal.QuotationId)).Action.Should().Be(expectedAuditAction);
        await AssertOneQueuedEmailAsync(db, proposal.RequestId, expectedTemplate, expectedSubject);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("RevisionRequested")]
    public async Task Reject_Or_Revision_Without_Comments_Is_400(string decision)
    {
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));

        var response = await DecideAsync(proposal.QuotationId, decision, comments: "  ");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deciding_An_Unknown_Quotation_Is_404()
    {
        (await DecideAsync(Guid.NewGuid(), "Approved")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_A_Librarian_Can_Decide_Or_Read_Approvals()
    {
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));

        foreach (var token in new[] { fx.StudentToken, fx.StoreOfficerToken, fx.AdminToken })
        {
            var client = fx.Client(token);
            (await client.PostAsJsonAsync("/api/approvals", Decision(proposal.QuotationId, "Approved"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync("/api/approvals")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/approvals/{proposal.QuotationId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        var anonymous = fx.Client(null);
        (await anonymous.PostAsJsonAsync("/api/approvals", Decision(proposal.QuotationId, "Approved"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/approvals")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var scope = fx.Services.CreateScope();
        (await fx.Db(scope).Quotations.SingleAsync(q => q.Id == proposal.QuotationId)).Status.Should().Be(QuotationStatus.Proposed);
    }

    [Fact]
    public async Task The_Queue_Lists_Pending_First_Filters_By_Status_And_Shows_Detail()
    {
        var roomId = await fx.SeedRoomAsync();
        var decided = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));
        (await DecideAsync(decided.QuotationId, "Rejected", "No.")).StatusCode.Should().Be(HttpStatusCode.Created);
        var pending = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));
        var client = fx.Client(fx.LibrarianToken);

        // Pending-first: the newest decided item sorts after every pending one, whatever the dates.
        var all = await client.GetFromJsonAsync<PagedResultShape<ApprovalQueueItemShape>>("/api/approvals?pageSize=100&sortDir=asc", TestSupport.JsonOptions);
        var statuses = all!.Items.Select(i => i.Status).ToList();
        statuses.TakeWhile(s => s == "Pending").Count().Should().Be(statuses.Count(s => s == "Pending"));
        all.Items.Should().Contain(i => i.QuotationId == pending.QuotationId && i.Status == "Pending" && i.Decision == null);

        var pendingOnly = await client.GetFromJsonAsync<PagedResultShape<ApprovalQueueItemShape>>("/api/approvals?status=Pending&pageSize=100", TestSupport.JsonOptions);
        pendingOnly!.Items.Should().OnlyContain(i => i.Status == "Pending").And.Contain(i => i.QuotationId == pending.QuotationId);
        pendingOnly.Items.Should().NotContain(i => i.QuotationId == decided.QuotationId);

        var rejected = await client.GetFromJsonAsync<PagedResultShape<ApprovalQueueItemShape>>("/api/approvals?status=rejected&pageSize=100", TestSupport.JsonOptions);
        var rejectedItem = rejected!.Items.Single(i => i.QuotationId == decided.QuotationId);
        rejectedItem.Status.Should().Be("Rejected");
        rejectedItem.Decision!.Comments.Should().Be("No.");

        (await client.GetAsync("/api/approvals?status=Maybe")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/approvals?sortBy=nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var detail = await client.GetFromJsonAsync<ApprovalDetailShape>($"/api/approvals/{pending.QuotationId}", TestSupport.JsonOptions);
        detail!.Item.Status.Should().Be("Pending");
        detail.Item.BookingRequestId.Should().Be(pending.RequestId);

        // The librarian can see whose booking this is (name, student number, email).
        using (var scope = fx.Services.CreateScope())
        {
            var student = await fx.Db(scope).StudentProfiles.Include(p => p.User)
                .SingleAsync(p => p.Id == fx.StudentProfileId);
            detail.Item.StudentName.Should().Be(student.User.FullName);
            detail.Item.StudentNumber.Should().Be(student.StudentNumber);
            detail.Item.StudentEmail.Should().Be(student.User.Email);
            var found = await client.GetFromJsonAsync<PagedResultShape<ApprovalQueueItemShape>>(
                $"/api/approvals?pageSize=100&search={Uri.EscapeDataString(student.StudentNumber)}", TestSupport.JsonOptions);
            found!.Items.Should().Contain(i => i.QuotationId == pending.QuotationId);

            // The booking request itself carries the same identity, and the requests list can be
            // searched by student number or name.
            var request = await client.GetFromJsonAsync<BookingRequestResponseShape>(
                $"/api/booking-requests/{pending.RequestId}", TestSupport.JsonOptions);
            request!.StudentName.Should().Be(student.User.FullName);
            request.StudentNumber.Should().Be(student.StudentNumber);
            request.StudentEmail.Should().Be(student.User.Email);
            var byNumber = await client.GetFromJsonAsync<PagedResultShape<BookingRequestResponseShape>>(
                $"/api/booking-requests?pageSize=100&search={Uri.EscapeDataString(student.StudentNumber)}", TestSupport.JsonOptions);
            byNumber!.Items.Should().Contain(r => r.Id == pending.RequestId && r.StudentName == student.User.FullName);
            var byName = await client.GetFromJsonAsync<PagedResultShape<BookingRequestResponseShape>>(
                $"/api/booking-requests?pageSize=100&search={Uri.EscapeDataString(student.User.FullName)}", TestSupport.JsonOptions);
            byName!.Items.Should().Contain(r => r.Id == pending.RequestId);
        }
        detail.LineItems.Should().ContainSingle(l => l.ItemType == "Room" && l.RoomId == roomId && l.RoomBookingId == null);
        detail.LineItems.Sum(l => l.LineTotal).Should().Be(detail.Item.TotalAmount);

        (await client.GetAsync($"/api/approvals/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>PLAN.md Day 2 exit gate, through the real API: submit → PendingApproval with a real
    /// quotation → approve → a room booking and Reserved stock exist.</summary>
    [Fact]
    public async Task End_To_End_Submit_Then_Approve_Books_The_Room_And_Reserves_The_Stock()
    {
        var roomId = await fx.SeedRoomAsync(hourlyRate: 20m);
        fx.PinnedRoomId = roomId;
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 2m);
        var student = fx.Client(await fx.CreateFreshStudentAsync());
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(NextDays(1)));

        var created = await student.PostAsJsonAsync("/api/booking-requests", new
        {
            objective = "End-to-end approval",
            groupSize = 4,
            preferredDateFrom = day,
            preferredDateTo = day,
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 120,
            budget = 100m,
            items = new[] { new { consumableId = markers, quantity = 3 } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var requestId = (await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions))!.Id;
        (await student.PostAsync($"/api/booking-requests/{requestId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.Accepted);

        var status = await WaitForStatusAsync(student, requestId, "PendingApproval");
        status.Should().Be("PendingApproval");

        var librarian = fx.Client(fx.LibrarianToken);
        var queue = await librarian.GetFromJsonAsync<PagedResultShape<ApprovalQueueItemShape>>("/api/approvals?status=Pending&pageSize=100", TestSupport.JsonOptions);
        var item = queue!.Items.Single(i => i.BookingRequestId == requestId);
        item.TotalAmount.Should().Be(46m); // 2h x 20 + 3 x 2

        (await DecideAsync(item.QuotationId, "Approved")).StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        var booking = await db.RoomBookings.AsNoTracking().SingleAsync(b => b.BookingRequestId == requestId);
        booking.RoomId.Should().Be(roomId);
        booking.Status.Should().Be(RoomBookingStatus.Confirmed);
        var reservation = await db.StockReservations.AsNoTracking().SingleAsync(r => r.ConsumableId == markers);
        reservation.Status.Should().Be(StockReservationStatus.Reserved);
        (await db.BookingRequests.AsNoTracking().SingleAsync(r => r.Id == requestId)).Status.Should().Be(BookingRequestStatus.Approved);
        (await db.QuotationLineItems.AsNoTracking().SingleAsync(l => l.QuotationId == item.QuotationId && l.ItemType == QuotationLineItemType.Room))
            .RoomBookingId.Should().Be(booking.Id);
    }

    private async Task AssertNothingWrittenAsync(Proposal proposal, params Guid[] consumableIds)
    {
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        (await db.Quotations.AsNoTracking().SingleAsync(q => q.Id == proposal.QuotationId)).Status.Should().Be(QuotationStatus.Proposed);
        (await db.QuotationLineItems.AsNoTracking().Where(l => l.QuotationId == proposal.QuotationId).AllAsync(l => l.RoomBookingId == null))
            .Should().BeTrue();
        (await db.BookingRequests.AsNoTracking().SingleAsync(r => r.Id == proposal.RequestId)).Status.Should().Be(BookingRequestStatus.PendingApproval);
        (await db.WorkflowExecutions.AsNoTracking().SingleAsync(w => w.Id == proposal.WorkflowId)).Status.Should().Be(WorkflowStatus.PendingApproval);
        (await db.RoomBookings.AnyAsync(b => b.BookingRequestId == proposal.RequestId)).Should().BeFalse();
        (await db.StockReservations.Where(r => proposal.ItemIds.Contains(r.BookingRequestItemId)).AllAsync(r => r.Status == StockReservationStatus.Pending))
            .Should().BeTrue();
        (await db.Consumables.AsNoTracking().Where(c => consumableIds.Contains(c.Id)).AllAsync(c => c.ReservedQuantity == 0)).Should().BeTrue();
        (await db.StockTransactions.AnyAsync(t => t.BookingRequestId == proposal.RequestId)).Should().BeFalse();
        (await db.ApprovalDecisions.AnyAsync(d => d.QuotationId == proposal.QuotationId)).Should().BeFalse();
        (await db.AuditLogs.AnyAsync(a => a.EntityId == proposal.QuotationId)).Should().BeFalse();
        (await db.EmailNotifications.AnyAsync(e => e.BookingRequestId == proposal.RequestId)).Should().BeFalse("a rolled-back decision queues no email");
    }

    /// <summary>Exactly one Queued, never-attempted row for the request, addressed to its student.</summary>
    private static async Task AssertOneQueuedEmailAsync(StudyHiveDbContext db, Guid requestId, string template, string subject)
    {
        var studentEmail = await db.BookingRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => r.Student.User.Email)
            .SingleAsync();
        var email = (await db.EmailNotifications.AsNoTracking().Where(e => e.BookingRequestId == requestId).ToListAsync())
            .Should().ContainSingle().Subject;
        email.ToEmail.Should().Be(studentEmail);
        email.Template.Should().Be(template);
        email.Subject.Should().Be(subject);
        email.Status.Should().Be(EmailNotificationStatus.Queued);
        email.AttemptCount.Should().Be(0);
        email.NextAttemptAt.Should().BeNull();
        email.SentAt.Should().BeNull();
        email.ProviderMessageId.Should().BeNull();
    }

    /// <summary>A confirmed booking for some other request, inserted directly — the "someone else got
    /// there first" half of a room clash.</summary>
    private async Task SeedForeignBookingAsync(Guid roomId, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        var now = DateTimeOffset.UtcNow;
        var day = DateOnly.FromDateTime(startsAt.UtcDateTime);
        var other = new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = fx.StudentProfileId,
            Objective = "Someone else's booking",
            GroupSize = 2,
            PreferredDateFrom = day,
            PreferredDateTo = day,
            PreferredTimeFrom = new TimeOnly(8, 0),
            PreferredTimeTo = new TimeOnly(18, 0),
            SessionsRequired = 1,
            SessionDurationMinutes = 90,
            Budget = 100m,
            Status = BookingRequestStatus.Approved,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.BookingRequests.Add(other);
        db.RoomBookings.Add(new RoomBooking
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            BookingRequestId = other.Id,
            StartsAt = startsAt.ToUniversalTime(),
            EndsAt = endsAt.ToUniversalTime(),
            Status = RoomBookingStatus.Confirmed,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> WaitForStatusAsync(HttpClient client, Guid requestId, string wanted)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var last = "";
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<WorkflowStatusResponseShape>($"/api/booking-requests/{requestId}/status", TestSupport.JsonOptions);
            last = status!.Status;
            if (last == wanted || last is "Failed" or "Rejected") return last;
            await Task.Delay(150);
        }
        return last;
    }
}

internal sealed class ApprovalDecisionShape
{
    public Guid Id { get; init; }
    public Guid QuotationId { get; init; }
    public Guid DecidedBy { get; init; }
    public string DecidedByRole { get; init; } = "";
    public string Decision { get; init; } = "";
    public string? Comments { get; init; }
}

internal sealed class ApprovalQueueItemShape
{
    public Guid QuotationId { get; init; }
    public Guid BookingRequestId { get; init; }
    public string StudentName { get; init; } = "";
    public string StudentNumber { get; init; } = "";
    public string StudentEmail { get; init; } = "";
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = "";
    public ApprovalDecisionShape? Decision { get; init; }
}

internal sealed class ApprovalLineItemShape
{
    public string ItemType { get; init; } = "";
    public Guid? RoomId { get; init; }
    public Guid? RoomBookingId { get; init; }
    public decimal LineTotal { get; init; }
}

internal sealed class ApprovalDetailShape
{
    public ApprovalQueueItemShape Item { get; init; } = new();
    public List<ApprovalLineItemShape> LineItems { get; init; } = [];
}
