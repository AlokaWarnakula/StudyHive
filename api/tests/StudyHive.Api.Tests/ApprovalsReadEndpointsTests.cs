using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

/// <summary>S4 read endpoints: quotations, workflow executions, audit logs and the bookings report.
/// Reuses <see cref="ApprovalsFixture"/> (one login per role, seeded proposals, FK-ordered cleanup).</summary>
public class ApprovalsReadEndpointsTests(ApprovalsFixture fx) : IClassFixture<ApprovalsFixture>
{
    private static int nextDayOffset = 400;
    private static int NextDays() => Interlocked.Add(ref nextDayOffset, 2);

    private async Task<Proposal> SeedAsync(int itemQuantity = 0)
    {
        var roomId = await fx.SeedRoomAsync(hourlyRate: 40m);
        if (itemQuantity == 0)
        {
            return await fx.SeedProposalAsync(roomId, sessions: 1, [], NextDays());
        }
        var consumableId = await fx.SeedConsumableAsync(stock: 50, unitPrice: 1.25m);
        return await fx.SeedProposalAsync(roomId, sessions: 1, [(consumableId, itemQuantity)], NextDays());
    }

    private async Task ApproveAsync(Guid quotationId)
    {
        var response = await fx.Client(fx.LibrarianToken).PostAsJsonAsync("/api/approvals", new { quotationId, decision = "Approved" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    // --- quotations -------------------------------------------------------------------------------

    [Fact]
    public async Task Quotation_List_Is_Librarian_Only()
    {
        (await fx.Client(null).GetAsync("/api/quotations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var token in new[] { fx.StudentToken, fx.StoreOfficerToken, fx.AdminToken })
        {
            (await fx.Client(token).GetAsync("/api/quotations")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Quotation_List_Filters_Sorts_And_Pages()
    {
        var approved = await SeedAsync(itemQuantity: 4);
        var proposed = await SeedAsync();
        await ApproveAsync(approved.QuotationId);
        var client = fx.Client(fx.LibrarianToken);

        var one = await client.GetFromJsonAsync<PagedResultShape<QuotationSummaryShape>>(
            $"/api/quotations?bookingRequestId={approved.RequestId}", TestSupport.JsonOptions);
        one!.TotalItems.Should().Be(1);
        var item = one.Items.Single();
        item.Id.Should().Be(approved.QuotationId);
        item.Status.Should().Be("Approved");
        item.LineItemCount.Should().Be(2);
        item.TotalAmount.Should().Be(item.RoomFee + item.ConsumableCost);

        var proposedOnly = await client.GetFromJsonAsync<PagedResultShape<QuotationSummaryShape>>(
            "/api/quotations?status=proposed&pageSize=100", TestSupport.JsonOptions);
        proposedOnly!.Items.Should().OnlyContain(q => q.Status == "Proposed").And.Contain(q => q.Id == proposed.QuotationId);

        var page = await client.GetFromJsonAsync<PagedResultShape<QuotationSummaryShape>>(
            "/api/quotations?pageSize=1&sortBy=totalAmount&sortDir=asc", TestSupport.JsonOptions);
        page!.Items.Should().HaveCount(1);
        page.PageSize.Should().Be(1);
        page.TotalItems.Should().BeGreaterThanOrEqualTo(2);

        (await client.GetAsync("/api/quotations?status=Pending")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var badSort = await client.GetAsync("/api/quotations?sortBy=nope");
        badSort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badSort.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Quotation_Detail_Is_For_Librarians_And_The_Owning_Student_Only()
    {
        var proposal = await SeedAsync(itemQuantity: 3);
        var path = $"/api/quotations/{proposal.QuotationId}";

        var detail = await fx.Client(fx.LibrarianToken).GetFromJsonAsync<QuotationShape>(path, TestSupport.JsonOptions);
        detail!.BookingRequestId.Should().Be(proposal.RequestId);
        detail.Status.Should().Be("Proposed");
        detail.LineItems.Should().HaveCount(2);
        detail.LineItems.Sum(l => l.LineTotal).Should().Be(detail.TotalAmount);
        var roomLine = detail.LineItems.Single(l => l.ItemType == "Room");
        roomLine.RoomId.Should().Be(proposal.RoomId);
        roomLine.RoomBookingId.Should().BeNull();
        detail.LineItems.Single(l => l.ItemType == "Consumable").ConsumableId.Should().NotBeNull();

        (await fx.Client(fx.StudentToken).GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK, "the fixture student owns this request");
        var otherStudent = await fx.CreateFreshStudentAsync();
        (await fx.Client(otherStudent).GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fx.Client(fx.StoreOfficerToken).GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fx.Client(fx.AdminToken).GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fx.Client(null).GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await fx.Client(fx.LibrarianToken).GetAsync($"/api/quotations/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await fx.Client(fx.StudentToken).GetAsync($"/api/quotations/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task There_Is_No_Client_Facing_Quotation_Create()
    {
        var response = await fx.Client(fx.LibrarianToken).PostAsJsonAsync("/api/quotations", new { });

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // --- workflow executions ----------------------------------------------------------------------

    [Fact]
    public async Task Workflow_Executions_Are_Librarian_Only()
    {
        var proposal = await SeedAsync();
        (await fx.Client(null).GetAsync("/api/workflow-executions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var token in new[] { fx.StudentToken, fx.StoreOfficerToken, fx.AdminToken })
        {
            var client = fx.Client(token);
            (await client.GetAsync("/api/workflow-executions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/workflow-executions/{proposal.WorkflowId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/workflow-executions/{proposal.WorkflowId}/steps")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Workflow_Execution_List_Puts_Failed_Runs_First_And_Filters()
    {
        var pending = await SeedAsync();
        var failed = await SeedAsync();
        using (var scope = fx.Services.CreateScope())
        {
            var db = fx.Db(scope);
            var execution = await db.WorkflowExecutions.SingleAsync(w => w.Id == failed.WorkflowId);
            execution.Status = WorkflowStatus.Failed;
            execution.ErrorCode = "VALIDATION_FAILED";
            execution.ErrorMessage = "Please revise this request before it can be approved: over budget.";
            await db.SaveChangesAsync();
        }
        var client = fx.Client(fx.LibrarianToken);

        var all = await client.GetFromJsonAsync<PagedResultShape<WorkflowExecutionShape>>(
            "/api/workflow-executions?pageSize=100&sortDir=asc", TestSupport.JsonOptions);
        var statuses = all!.Items.Select(i => i.Status).ToList();
        statuses.TakeWhile(s => s == "Failed").Count().Should().Be(statuses.Count(s => s == "Failed"));

        var byCode = await client.GetFromJsonAsync<PagedResultShape<WorkflowExecutionShape>>(
            "/api/workflow-executions?errorCode=VALIDATION_FAILED&pageSize=100", TestSupport.JsonOptions);
        byCode!.Items.Should().Contain(i => i.Id == failed.WorkflowId).And.OnlyContain(i => i.ErrorCode == "VALIDATION_FAILED");

        var byStatus = await client.GetFromJsonAsync<PagedResultShape<WorkflowExecutionShape>>(
            "/api/workflow-executions?status=pendingapproval&pageSize=100", TestSupport.JsonOptions);
        byStatus!.Items.Should().Contain(i => i.Id == pending.WorkflowId).And.OnlyContain(i => i.Status == "PendingApproval");

        (await client.GetAsync("/api/workflow-executions?status=Nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/workflow-executions?sortBy=nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Workflow_Execution_Detail_And_Steps_Return_The_Logged_Json()
    {
        var proposal = await SeedAsync(itemQuantity: 2);
        var client = fx.Client(fx.LibrarianToken);

        var detail = await client.GetFromJsonAsync<WorkflowDetailShape>(
            $"/api/workflow-executions/{proposal.WorkflowId}", TestSupport.JsonOptions);
        detail!.Execution.Id.Should().Be(proposal.WorkflowId);
        detail.Execution.BookingRequestId.Should().Be(proposal.RequestId);
        detail.Execution.Status.Should().Be("PendingApproval");
        detail.Steps.Should().ContainSingle();

        var steps = await client.GetFromJsonAsync<List<WorkflowStepShape>>(
            $"/api/workflow-executions/{proposal.WorkflowId}/steps", TestSupport.JsonOptions);
        var step = steps!.Single();
        step.StepNumber.Should().Be(4);
        step.AgentName.Should().Be("Validation");
        step.ValidationResult.Should().Be("Pass");
        step.Input.Should().NotBeNull();
        step.Input!.Value.ValueKind.Should().Be(JsonValueKind.Object, "logged JSON is returned as JSON, not an escaped string");
        step.Input.Value.GetProperty("proposedSlots").GetArrayLength().Should().Be(1);

        (await client.GetAsync($"/api/workflow-executions/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/workflow-executions/{Guid.NewGuid()}/steps")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- audit logs -------------------------------------------------------------------------------

    [Fact]
    public async Task Audit_Logs_Are_Admin_Only()
    {
        (await fx.Client(null).GetAsync("/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var token in new[] { fx.StudentToken, fx.StoreOfficerToken, fx.LibrarianToken })
        {
            (await fx.Client(token).GetAsync("/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task An_Approval_Is_In_The_Audit_Log_And_Filters_Find_It()
    {
        var proposal = await SeedAsync(itemQuantity: 2);
        await ApproveAsync(proposal.QuotationId);
        var admin = fx.Client(fx.AdminToken);

        var byEntity = await admin.GetFromJsonAsync<PagedResultShape<AuditLogShape>>(
            $"/api/audit-logs?entityId={proposal.QuotationId}", TestSupport.JsonOptions);
        var entry = byEntity!.Items.Should().ContainSingle().Subject;
        entry.Action.Should().Be("QuotationApproved");
        entry.EntityType.Should().Be("Quotation");
        entry.UserId.Should().Be(fx.LibrarianId);
        entry.UserEmail.Should().NotBeNullOrEmpty();
        entry.Details!.Value.ValueKind.Should().Be(JsonValueKind.Object);
        entry.Details.Value.GetProperty("roomBookingIds").GetArrayLength().Should().Be(1);
        entry.Details.Value.GetProperty("stockReservationIds").GetArrayLength().Should().Be(1);

        var combined = await admin.GetFromJsonAsync<PagedResultShape<AuditLogShape>>(
            $"/api/audit-logs?action=quotationapproved&entityType=QUOTATION&userId={fx.LibrarianId}&pageSize=100", TestSupport.JsonOptions);
        combined!.Items.Should().Contain(a => a.EntityId == proposal.QuotationId)
            .And.OnlyContain(a => a.Action == "QuotationApproved" && a.UserId == fx.LibrarianId);

        var window = await admin.GetFromJsonAsync<PagedResultShape<AuditLogShape>>(
            $"/api/audit-logs?entityId={proposal.QuotationId}&from=2000-01-01T00:00:00Z&to=2000-02-01T00:00:00Z", TestSupport.JsonOptions);
        window!.TotalItems.Should().Be(0);

        (await admin.GetAsync("/api/audit-logs?sortBy=nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/audit-logs?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- bookings report --------------------------------------------------------------------------

    [Fact]
    public async Task Bookings_Report_Is_For_Librarians_And_Admins()
    {
        (await fx.Client(null).GetAsync("/api/reports/bookings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await fx.Client(fx.StudentToken).GetAsync("/api/reports/bookings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fx.Client(fx.StoreOfficerToken).GetAsync("/api/reports/bookings")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fx.Client(fx.LibrarianToken).GetAsync("/api/reports/bookings")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fx.Client(fx.AdminToken).GetAsync("/api/reports/bookings")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await fx.Client(fx.AdminToken).GetAsync("/api/reports/bookings?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bookings_Report_Counts_Agree_And_Include_An_Approved_Quotation()
    {
        var approved = await SeedAsync(itemQuantity: 4);
        await ApproveAsync(approved.QuotationId);
        decimal approvedTotal;
        using (var scope = fx.Services.CreateScope())
        {
            approvedTotal = (await fx.Db(scope).Quotations.SingleAsync(q => q.Id == approved.QuotationId)).TotalAmount;
        }

        // A fixed window that ends now, after the seeded request committed: rows other test classes
        // create while the report's queries run fall outside it, so its figures stay consistent.
        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-30);
        var report = await fx.Client(fx.LibrarianToken).GetFromJsonAsync<BookingsReportShape>(
            $"/api/reports/bookings?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}",
            TestSupport.JsonOptions);

        report!.ByStatus.Should().HaveCount(Enum.GetValues<BookingRequestStatus>().Length, "every status is listed, zeros included");
        report.TotalRequests.Should().Be(report.ByStatus.Sum(s => s.Count));
        report.TotalRequests.Should().Be(report.ByWeek.Sum(w => w.Requests));
        report.ByStatus.Single(s => s.Status == "Approved").Count.Should().BeGreaterThanOrEqualTo(1);
        report.ByWeek.Should().BeInAscendingOrder(w => w.WeekStart);
        report.ByWeek.Should().OnlyContain(w => w.WeekStart.DayOfWeek == DayOfWeek.Monday);

        report.Spend.ApprovedQuotations.Should().Be(report.ByWeek.Sum(w => w.Approved));
        report.Spend.TotalSpend.Should().Be(report.ByWeek.Sum(w => w.ApprovedSpend));
        report.Spend.TotalBudget.Should().Be(report.ByWeek.Sum(w => w.ApprovedBudget));
        report.Spend.WithinBudget.Should().Be(report.Spend.ApprovedQuotations - report.Spend.OverBudget);
        report.Spend.ApprovedQuotations.Should().BeGreaterThanOrEqualTo(1);
        report.Spend.TotalSpend.Should().BeGreaterThanOrEqualTo(approvedTotal);
    }

    [Fact]
    public async Task Bookings_Report_For_An_Empty_Window_Is_All_Zeros()
    {
        var report = await fx.Client(fx.AdminToken).GetFromJsonAsync<BookingsReportShape>(
            "/api/reports/bookings?from=2000-01-01T00:00:00Z&to=2000-02-01T00:00:00Z", TestSupport.JsonOptions);

        report!.TotalRequests.Should().Be(0);
        report.ByStatus.Should().OnlyContain(s => s.Count == 0);
        report.ByWeek.Should().BeEmpty();
        report.Spend.ApprovedQuotations.Should().Be(0);
        report.Spend.TotalSpend.Should().Be(0m);
    }
}

internal sealed class QuotationSummaryShape
{
    public Guid Id { get; init; }
    public Guid BookingRequestId { get; init; }
    public decimal RoomFee { get; init; }
    public decimal ConsumableCost { get; init; }
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = "";
    public int LineItemCount { get; init; }
}

internal sealed class QuotationLineShape
{
    public string ItemType { get; init; } = "";
    public Guid? RoomId { get; init; }
    public Guid? RoomBookingId { get; init; }
    public Guid? ConsumableId { get; init; }
    public decimal LineTotal { get; init; }
}

internal sealed class QuotationShape
{
    public Guid Id { get; init; }
    public Guid BookingRequestId { get; init; }
    public decimal TotalAmount { get; init; }
    public string Status { get; init; } = "";
    public List<QuotationLineShape> LineItems { get; init; } = [];
}

internal sealed class WorkflowExecutionShape
{
    public Guid Id { get; init; }
    public Guid BookingRequestId { get; init; }
    public string Status { get; init; } = "";
    public string? ErrorCode { get; init; }
}

internal sealed class WorkflowStepShape
{
    public int StepNumber { get; init; }
    public string AgentName { get; init; } = "";
    public string? ValidationResult { get; init; }
    public JsonElement? Input { get; init; }
}

internal sealed class WorkflowDetailShape
{
    public WorkflowExecutionShape Execution { get; init; } = new();
    public List<WorkflowStepShape> Steps { get; init; } = [];
}

internal sealed class AuditLogShape
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public string Action { get; init; } = "";
    public string EntityType { get; init; } = "";
    public Guid EntityId { get; init; }
    public JsonElement? Details { get; init; }
}

internal sealed class BookingStatusCountShape
{
    public string Status { get; init; } = "";
    public int Count { get; init; }
}

internal sealed class BookingsWeekShape
{
    public DateOnly WeekStart { get; init; }
    public int Requests { get; init; }
    public int Approved { get; init; }
    public decimal ApprovedSpend { get; init; }
    public decimal ApprovedBudget { get; init; }
}

internal sealed class BookingsSpendShape
{
    public int ApprovedQuotations { get; init; }
    public decimal TotalSpend { get; init; }
    public decimal TotalBudget { get; init; }
    public int WithinBudget { get; init; }
    public int OverBudget { get; init; }
}

internal sealed class BookingsReportShape
{
    public int TotalRequests { get; init; }
    public List<BookingStatusCountShape> ByStatus { get; init; } = [];
    public List<BookingsWeekShape> ByWeek { get; init; } = [];
    public BookingsSpendShape Spend { get; init; } = new();
}
