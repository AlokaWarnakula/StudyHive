using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>
/// AUDIT A1: cancel and approve can never both win (C-01), a cancel cleans up its quotation,
/// reservations and workflow (CW-07), an approved booking can be cancelled before it starts (C-07),
/// ended bookings close as Completed or NoShow (C-06, C-15), and the request response carries its
/// room bookings.
/// </summary>
public class BookingLifecycleTests(ApprovalsFixture fx) : IClassFixture<ApprovalsFixture>
{
    // Far from ApprovalsControllerTests' days (that class has its own fixture, but rooms are
    // per-test anyway); each test books its own room.
    private static int nextDayOffset = 200;
    private static int NextDays(int sessions) => Interlocked.Add(ref nextDayOffset, sessions + 1);

    private Task<HttpResponseMessage> ApproveAsync(Guid quotationId) =>
        fx.Client(fx.LibrarianToken).PostAsJsonAsync("/api/approvals", new { quotationId, decision = "Approved", comments = (string?)null });

    private Task<HttpResponseMessage> CancelAsync(Guid requestId) =>
        fx.Client(fx.StudentToken).DeleteAsync($"/api/booking-requests/{requestId}");

    [Fact]
    public async Task Approving_A_Request_The_Student_Cancelled_Returns_409_And_Books_Nothing()
    {
        var roomId = await fx.SeedRoomAsync();
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [(markers, 2)], NextDays(1));

        (await CancelAsync(proposal.RequestId)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var approve = await ApproveAsync(proposal.QuotationId);

        approve.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        (await db.BookingRequests.SingleAsync(r => r.Id == proposal.RequestId)).Status.Should().Be(BookingRequestStatus.Cancelled);
        (await db.RoomBookings.AnyAsync(b => b.BookingRequestId == proposal.RequestId)).Should().BeFalse();
        (await db.ApprovalDecisions.AnyAsync(d => d.QuotationId == proposal.QuotationId)).Should().BeFalse();
        (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == markers)).ReservedQuantity.Should().Be(0);
        (await db.EmailNotifications.AnyAsync(e => e.BookingRequestId == proposal.RequestId)).Should().BeFalse();
    }

    [Fact]
    public async Task A_Quotation_Whose_Request_Is_No_Longer_Pending_Returns_409_request_not_pending()
    {
        // Rows cancelled before this fix still have a Proposed quotation: the request check catches them.
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));
        using (var scope = fx.Services.CreateScope())
        {
            await fx.Db(scope).BookingRequests.Where(r => r.Id == proposal.RequestId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, BookingRequestStatus.Cancelled));
        }

        var approve = await ApproveAsync(proposal.QuotationId);

        approve.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await approve.Content.ReadAsStringAsync()).Should().Contain("request-not-pending");
        var queue = await fx.Client(fx.LibrarianToken).GetStringAsync($"/api/approvals?status=Pending&pageSize=100");
        queue.Should().NotContain(proposal.QuotationId.ToString());
    }

    [Fact]
    public async Task Cancelling_A_PendingApproval_Request_Supersedes_Its_Quotation_Releases_Stock_And_Stops_The_Workflow()
    {
        var roomId = await fx.SeedRoomAsync();
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [(markers, 3)], NextDays(1));

        (await CancelAsync(proposal.RequestId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        (await db.Quotations.SingleAsync(q => q.Id == proposal.QuotationId)).Status.Should().Be(QuotationStatus.Superseded);
        (await db.StockReservations.SingleAsync(r => r.BookingRequestItemId == proposal.ItemIds[0])).Status
            .Should().Be(StockReservationStatus.Released);
        var workflow = await db.WorkflowExecutions.SingleAsync(w => w.Id == proposal.WorkflowId);
        workflow.Status.Should().Be(WorkflowStatus.Rejected);
        workflow.ErrorCode.Should().Be("CANCELLED_BY_STUDENT");
        workflow.CompletedAt.Should().NotBeNull();

        var queue = await fx.Client(fx.LibrarianToken).GetStringAsync($"/api/approvals?status=Pending&pageSize=100");
        queue.Should().NotContain(proposal.QuotationId.ToString());
    }

    [Fact]
    public async Task Cancelling_An_Approved_Booking_Before_It_Starts_Releases_The_Room_And_Stock()
    {
        var roomId = await fx.SeedRoomAsync();
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 2, [(markers, 4)], NextDays(2));
        (await ApproveAsync(proposal.QuotationId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var cancel = await CancelAsync(proposal.RequestId);

        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent, await cancel.Content.ReadAsStringAsync());
        using var scope = fx.Services.CreateScope();
        var db = fx.Db(scope);
        (await db.BookingRequests.SingleAsync(r => r.Id == proposal.RequestId)).Status.Should().Be(BookingRequestStatus.Cancelled);
        var bookings = await db.RoomBookings.AsNoTracking().Where(b => b.BookingRequestId == proposal.RequestId).ToListAsync();
        bookings.Should().HaveCount(2).And.OnlyContain(b => b.Status == RoomBookingStatus.Cancelled);
        var reservation = await db.StockReservations.AsNoTracking().SingleAsync(r => r.BookingRequestItemId == proposal.ItemIds[0]);
        reservation.Status.Should().Be(StockReservationStatus.Released);
        (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == markers)).ReservedQuantity.Should().Be(0);
        var release = await db.StockTransactions.AsNoTracking()
            .SingleAsync(t => t.StockReservationId == reservation.Id && t.TransactionType == StockTransactionType.Release);
        release.Quantity.Should().Be(4);
        var email = await db.EmailNotifications.AsNoTracking()
            .SingleAsync(e => e.BookingRequestId == proposal.RequestId && e.Template == "BookingCancelled");
        email.Subject.Should().Be("Your StudyHive booking is cancelled");
        email.Status.Should().Be(EmailNotificationStatus.Queued);
    }

    [Fact]
    public async Task Cancelling_An_Approved_Booking_After_It_Started_Returns_409_And_Changes_Nothing()
    {
        var roomId = await fx.SeedRoomAsync();
        var markers = await fx.SeedConsumableAsync(stock: 10, unitPrice: 1m);
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [(markers, 2)], NextDays(1));
        (await ApproveAsync(proposal.QuotationId)).StatusCode.Should().Be(HttpStatusCode.Created);
        var now = DateTimeOffset.UtcNow;
        using (var scope = fx.Services.CreateScope())
        {
            await fx.Db(scope).RoomBookings.Where(b => b.BookingRequestId == proposal.RequestId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.StartsAt, now.AddMinutes(-10)).SetProperty(b => b.EndsAt, now.AddMinutes(80)));
        }

        var cancel = await CancelAsync(proposal.RequestId);

        cancel.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await cancel.Content.ReadAsStringAsync()).Should().Contain("This booking has already started");
        using var verify = fx.Services.CreateScope();
        var db = fx.Db(verify);
        (await db.BookingRequests.SingleAsync(r => r.Id == proposal.RequestId)).Status.Should().Be(BookingRequestStatus.Approved);
        (await db.RoomBookings.SingleAsync(b => b.BookingRequestId == proposal.RequestId)).Status.Should().Be(RoomBookingStatus.Confirmed);
        (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == markers)).ReservedQuantity.Should().Be(2);
    }

    [Fact]
    public async Task Ended_Bookings_Close_As_Completed_Or_NoShow_And_The_Request_Completes()
    {
        // Moved to 2001 so the sweep, called "as of" 2001, can only see this test's bookings.
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 2, [], NextDays(2));
        (await ApproveAsync(proposal.QuotationId)).StatusCode.Should().Be(HttpStatusCode.Created);
        var day = new DateTimeOffset(2001, 3, 5, 4, 30, 0, TimeSpan.Zero);
        Guid checkedInId, missedId;
        using (var scope = fx.Services.CreateScope())
        {
            var db = fx.Db(scope);
            var ids = await db.RoomBookings.Where(b => b.BookingRequestId == proposal.RequestId)
                .OrderBy(b => b.StartsAt).Select(b => b.Id).ToListAsync();
            (checkedInId, missedId) = (ids[0], ids[1]);
            await db.RoomBookings.Where(b => b.Id == checkedInId).ExecuteUpdateAsync(s => s
                .SetProperty(b => b.StartsAt, day).SetProperty(b => b.EndsAt, day.AddHours(2))
                .SetProperty(b => b.CheckedInAt, day.AddMinutes(5)));
            await db.RoomBookings.Where(b => b.Id == missedId).ExecuteUpdateAsync(s => s
                .SetProperty(b => b.StartsAt, day.AddDays(1)).SetProperty(b => b.EndsAt, day.AddDays(1).AddHours(2)));
        }

        // Between the two slots: only the first has ended, so the request stays Approved.
        await SweepAsync(day.AddHours(3));
        await AssertAsync(checkedInId, RoomBookingStatus.Completed, missedId, RoomBookingStatus.Confirmed, BookingRequestStatus.Approved);

        await SweepAsync(day.AddDays(1).AddHours(3));
        await AssertAsync(checkedInId, RoomBookingStatus.Completed, missedId, RoomBookingStatus.NoShow, BookingRequestStatus.Completed);

        async Task SweepAsync(DateTimeOffset asOf)
        {
            using var scope = fx.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IBookingLifecycleService>().CloseEndedBookingsAsync(asOf, default);
        }

        async Task AssertAsync(Guid firstId, RoomBookingStatus first, Guid secondId, RoomBookingStatus second, BookingRequestStatus request)
        {
            using var scope = fx.Services.CreateScope();
            var db = fx.Db(scope);
            (await db.RoomBookings.AsNoTracking().SingleAsync(b => b.Id == firstId)).Status.Should().Be(first);
            (await db.RoomBookings.AsNoTracking().SingleAsync(b => b.Id == secondId)).Status.Should().Be(second);
            (await db.BookingRequests.AsNoTracking().SingleAsync(r => r.Id == proposal.RequestId)).Status.Should().Be(request);
        }
    }

    [Fact]
    public async Task The_Request_Response_Carries_Its_Room_Bookings()
    {
        var roomId = await fx.SeedRoomAsync();
        var proposal = await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays(1));
        var student = fx.Client(fx.StudentToken);

        var before = await student.GetFromJsonAsync<RequestWithBookingsShape>($"/api/booking-requests/{proposal.RequestId}", TestSupport.JsonOptions);
        before!.RoomBookings.Should().BeEmpty();

        (await ApproveAsync(proposal.QuotationId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await student.GetFromJsonAsync<RequestWithBookingsShape>($"/api/booking-requests/{proposal.RequestId}", TestSupport.JsonOptions);
        var booking = detail!.RoomBookings.Should().ContainSingle().Subject;
        booking.RoomId.Should().Be(roomId);
        booking.RoomName.Should().StartWith("Approval test room");
        booking.StartsAt.Should().Be(proposal.Slots[0].StartsAt);
        booking.EndsAt.Should().Be(proposal.Slots[0].EndsAt);
        booking.Status.Should().Be("Confirmed");
        booking.CheckedInAt.Should().BeNull();

        var list = await student.GetFromJsonAsync<PagedShape>("/api/booking-requests?pageSize=100", TestSupport.JsonOptions);
        list!.Items.Single(r => r.Id == proposal.RequestId).RoomBookings.Should().ContainSingle(b => b.Id == booking.Id);
    }

    private sealed class RequestWithBookingsShape
    {
        public Guid Id { get; init; }
        public List<RoomBookingShape> RoomBookings { get; init; } = [];
    }

    private sealed class RoomBookingShape
    {
        public Guid Id { get; init; }
        public Guid RoomId { get; init; }
        public string RoomName { get; init; } = "";
        public DateTimeOffset StartsAt { get; init; }
        public DateTimeOffset EndsAt { get; init; }
        public string Status { get; init; } = "";
        public DateTimeOffset? CheckedInAt { get; init; }
    }

    private sealed class PagedShape
    {
        public List<RequestWithBookingsShape> Items { get; init; } = [];
    }
}
