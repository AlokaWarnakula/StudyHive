using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.BookingRequests;

/// <summary>S1: the booking request lifecycle, Draft through submit/workflow to a terminal status. See DOCS §11 API table.</summary>
[ApiController]
[Route("api/booking-requests")]
[Authorize]
public sealed class BookingRequestsController(
    StudyHiveDbContext db,
    IBookingEligibilityService eligibilityService,
    IWorkflowOrchestrationService workflowOrchestration,
    IWorkflowQueue workflowQueue) : ControllerBase
{
    /// <summary>Requests that still count against the weekly quota / can still be acted on by the student.</summary>
    private static readonly BookingRequestStatus[] CancellableStatuses =
    [
        BookingRequestStatus.Draft,
        BookingRequestStatus.Submitted,
        BookingRequestStatus.Processing,
        BookingRequestStatus.PendingApproval,
        BookingRequestStatus.RevisionRequested,
    ];

    [HttpPost]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(typeof(BookingRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateBookingRequestRequest request, CancellationToken ct)
    {
        var studentProfile = await GetOwnStudentProfileAsync(ct);
        if (studentProfile is null) return NoStudentProfileProblem();

        var itemsProblem = await ValidateItemsOrProblemAsync(request.Items, ct);
        if (itemsProblem is not null) return itemsProblem;

        var bookingRequest = new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = studentProfile.Id,
            Objective = request.Objective.Trim(),
            GroupSize = request.GroupSize,
            PreferredDateFrom = request.PreferredDateFrom,
            PreferredDateTo = request.PreferredDateTo,
            PreferredTimeFrom = request.PreferredTimeFrom,
            PreferredTimeTo = request.PreferredTimeTo,
            SessionsRequired = request.SessionsRequired,
            SessionDurationMinutes = request.SessionDurationMinutes,
            Budget = request.Budget,
            Notes = request.Notes?.Trim(),
            Status = BookingRequestStatus.Draft,
        };

        foreach (var item in request.Items)
        {
            bookingRequest.Items.Add(new BookingRequestItem
            {
                Id = Guid.NewGuid(),
                BookingRequestId = bookingRequest.Id,
                ConsumableId = item.ConsumableId,
                Quantity = item.Quantity,
            });
        }

        db.BookingRequests.Add(bookingRequest);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = bookingRequest.Id }, BookingRequestResponse.From(bookingRequest));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BookingRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, [FromQuery] string? status, CancellationToken ct)
    {
        IQueryable<BookingRequest> requests = db.BookingRequests.AsNoTracking().Include(r => r.Items);

        // DOCS §11 API table scopes this list to "Student (own), Librarian" — StoreOfficer has no
        // business need to see other students' requests, so it is explicitly denied rather than
        // silently falling through to "sees everything" (Codex security review, P1).
        if (User.IsInRole(Roles.Student))
        {
            var studentProfile = await GetOwnStudentProfileAsync(ct);
            if (studentProfile is null)
            {
                return Ok(PagedResult<BookingRequestResponse>.Create([], query.Page, query.PageSize, 0));
            }
            requests = requests.Where(r => r.StudentId == studentProfile.Id);
        }
        else if (!IsStaffReader(User))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BookingRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                ModelState.AddModelError(nameof(status), $"Unknown status value '{status}'.");
                return ValidationProblem(ModelState);
            }
            requests = requests.Where(r => r.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = $"%{query.Search.Trim()}%";
            requests = requests.Where(r => EF.Functions.ILike(r.Objective, search));
        }

        var sortDescending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<BookingRequest>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => sortDescending ? requests.OrderByDescending(r => r.CreatedAt) : requests.OrderBy(r => r.CreatedAt),
            "status" => sortDescending ? requests.OrderByDescending(r => r.Status) : requests.OrderBy(r => r.Status),
            "budget" => sortDescending ? requests.OrderByDescending(r => r.Budget) : requests.OrderBy(r => r.Budget),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }
        requests = sorted;

        var totalItems = await requests.CountAsync(ct);
        var items = await requests
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => BookingRequestResponse.From(r, null))
            .ToListAsync(ct);

        return Ok(PagedResult<BookingRequestResponse>.Create(items, query.Page, query.PageSize, totalItems));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.AsNoTracking().Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct)) return Forbid();

        var latestWorkflowId = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.BookingRequestId == id)
            .OrderByDescending(w => w.StartedAt)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(ct);

        return Ok(BookingRequestResponse.From(bookingRequest, latestWorkflowId));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(typeof(BookingRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateBookingRequestRequest request, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.Include(r => r.Items).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct, staffAllowed: false)) return Forbid();

        if (bookingRequest.Status != BookingRequestStatus.Draft)
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Only draft requests can be edited",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This request is '{bookingRequest.Status}' and can no longer be edited.");
        }

        var itemsProblem = await ValidateItemsOrProblemAsync(request.Items, ct);
        if (itemsProblem is not null) return itemsProblem;

        bookingRequest.Objective = request.Objective.Trim();
        bookingRequest.GroupSize = request.GroupSize;
        bookingRequest.PreferredDateFrom = request.PreferredDateFrom;
        bookingRequest.PreferredDateTo = request.PreferredDateTo;
        bookingRequest.PreferredTimeFrom = request.PreferredTimeFrom;
        bookingRequest.PreferredTimeTo = request.PreferredTimeTo;
        bookingRequest.SessionsRequired = request.SessionsRequired;
        bookingRequest.SessionDurationMinutes = request.SessionDurationMinutes;
        bookingRequest.Budget = request.Budget;
        bookingRequest.Notes = request.Notes?.Trim();
        bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;

        db.BookingRequestItems.RemoveRange(bookingRequest.Items);
        bookingRequest.Items.Clear();
        foreach (var item in request.Items)
        {
            bookingRequest.Items.Add(new BookingRequestItem
            {
                Id = Guid.NewGuid(),
                BookingRequestId = bookingRequest.Id,
                ConsumableId = item.ConsumableId,
                Quantity = item.Quantity,
            });
        }

        await db.SaveChangesAsync(ct);
        return Ok(BookingRequestResponse.From(bookingRequest));
    }

    /// <summary>Cancel — preserves the row for audit history (DOCS: requests are never physically
    /// deleted). A terminal request (Completed/Cancelled/Rejected/Failed) cannot be cancelled again.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct, staffAllowed: false)) return Forbid();

        if (!CancellableStatuses.Contains(bookingRequest.Status))
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Request cannot be cancelled",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This request is already '{bookingRequest.Status}'.");
        }

        bookingRequest.Status = BookingRequestStatus.Cancelled;
        bookingRequest.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "StudentOnly")]
    [EnableRateLimiting(RateLimitPolicies.WorkflowSubmit)]
    [ProducesResponseType(typeof(SubmitBookingRequestResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct, staffAllowed: false)) return Forbid();

        if (bookingRequest.Status != BookingRequestStatus.Draft)
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Only draft requests can be submitted",
                statusCode: StatusCodes.Status409Conflict,
                detail: $"This request is '{bookingRequest.Status}' and cannot be submitted again.");
        }

        // Serializes concurrent submissions from the same student so the weekly-quota check below
        // can't race two Submit calls past each other (Codex security review, P1): FOR UPDATE holds
        // a row lock on the student's own profile for the rest of this transaction, so a second
        // concurrent Submit for a different draft blocks here until the first one commits — by which
        // point its WorkflowExecution already counts toward the quota the second call evaluates.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.StudentProfiles
            .FromSqlInterpolated($"SELECT * FROM student_profiles WHERE id = {bookingRequest.StudentId} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync(ct);

        // Fail fast, synchronously — the workflow itself never re-litigates eligibility from
        // scratch, it only carries this same verdict to the Planner (see WorkflowOrchestrationService).
        var eligibility = await eligibilityService.EvaluateAsync(bookingRequest.StudentId, ct);
        if (!eligibility.IsEligible)
        {
            return Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Student is not eligible to submit a booking request",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: string.Join(" ", eligibility.Reasons));
        }

        var workflowId = await workflowOrchestration.StartAsync(id, ct);
        await transaction.CommitAsync(ct);

        await workflowQueue.EnqueueAsync(workflowId, ct);

        return Accepted(value: new SubmitBookingRequestResponse { WorkflowId = workflowId });
    }

    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(WorkflowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var bookingRequest = await db.BookingRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (bookingRequest is null) return NotFound();

        if (!await AuthorizeOwnerAsync(bookingRequest.StudentId, ct)) return Forbid();

        var execution = await db.WorkflowExecutions.AsNoTracking()
            .Include(w => w.StepLogs)
            .Where(w => w.BookingRequestId == id)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (execution is null)
        {
            return Problem(
                type: "https://studyhive.dev/errors/not-found",
                title: "No workflow has been started for this request",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(WorkflowStatusResponse.From(execution));
    }

    private async Task<StudentProfile?> GetOwnStudentProfileAsync(CancellationToken ct)
    {
        var userId = User.GetUserId();
        return await db.StudentProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
    }

    private ObjectResult NoStudentProfileProblem() => Problem(
        type: "https://studyhive.dev/errors/validation",
        title: "No student profile",
        statusCode: StatusCodes.Status422UnprocessableEntity,
        detail: "Create a student profile (POST /api/student-profiles) before creating booking requests.");

    /// <summary>Resolves the booking request's owning user id and checks it against an explicit
    /// Librarian allow-list — deliberately not the shared "any staff role" ResourceOwner
    /// policy, which would also let StoreOfficer read student booking data outside DOCS §11's
    /// documented "Student (own), Librarian" scope (Codex security review, P1). When
    /// <paramref name="staffAllowed"/> is false, only the owning student may act — used for write
    /// operations the API table restricts to "Student (own)".</summary>
    private async Task<bool> AuthorizeOwnerAsync(Guid studentProfileId, CancellationToken ct, bool staffAllowed = true)
    {
        if (staffAllowed && IsStaffReader(User)) return true;

        var ownerUserId = await db.StudentProfiles.AsNoTracking()
            .Where(p => p.Id == studentProfileId)
            .Select(p => p.UserId)
            .SingleOrDefaultAsync(ct);

        return User.TryGetUserId(out var callerId) && callerId == ownerUserId;
    }

    /// <summary>Every S1 endpoint's staff-read scope, per DOCS §11: Librarian only. Deliberately
    /// excludes StoreOfficer and Admin because neither has a documented need to read student booking
    /// data; Admin-only profile updates are separately protected by the AdminOnly policy.</summary>
    private static bool IsStaffReader(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Roles.Librarian);

    /// <summary>Rejects duplicate or nonexistent consumable ids as a controlled 422 instead of
    /// letting the DB's unique index / FK constraint turn bad client input into a 500 (Codex
    /// security review, P2).</summary>
    private async Task<IActionResult?> ValidateItemsOrProblemAsync(IReadOnlyList<BookingRequestItemRequest> items, CancellationToken ct)
    {
        if (items.Count == 0) return null;

        var ids = items.Select(i => i.ConsumableId).ToList();
        var duplicateIds = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
        {
            return Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Duplicate consumable in request items",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: $"Each consumable can only appear once per request: {string.Join(", ", duplicateIds)}.");
        }

        var existingIds = await db.Consumables.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);
        var missingIds = ids.Except(existingIds).ToList();
        if (missingIds.Count > 0)
        {
            return Problem(
                type: "https://studyhive.dev/errors/validation",
                title: "Unknown consumable",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                detail: $"These consumable ids do not exist: {string.Join(", ", missingIds)}.");
        }

        return null;
    }
}
