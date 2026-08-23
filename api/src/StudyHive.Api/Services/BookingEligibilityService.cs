using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Services;

public sealed record EligibilityResult(bool IsEligible, IReadOnlyList<string> Reasons);

/// <summary>
/// Centralizes S1's eligibility rule (DOCS §11 handoff line: "active account, within weekly booking
/// limit, no outstanding penalties") in one place so the submit endpoint, the eligibility endpoint,
/// and the Planner Agent request all agree on the same verdict instead of re-implementing it.
/// </summary>
public interface IBookingEligibilityService
{
    Task<EligibilityResult> EvaluateAsync(Guid studentProfileId, CancellationToken ct);
}

public sealed class BookingEligibilityService(StudyHiveDbContext db) : IBookingEligibilityService
{
    public async Task<EligibilityResult> EvaluateAsync(Guid studentProfileId, CancellationToken ct)
    {
        var profile = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == studentProfileId, ct);
        if (profile is null)
        {
            return new EligibilityResult(false, ["Student profile not found."]);
        }

        var reasons = new List<string>();

        if (!profile.IsActive)
        {
            reasons.Add("Student profile is not active.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (profile.SuspendedUntil is { } suspendedUntil && suspendedUntil >= today)
        {
            reasons.Add($"Student is suspended until {suspendedUntil:yyyy-MM-dd}.");
        }

        if (profile.PenaltyPoints > 0)
        {
            reasons.Add("Student has outstanding penalty points.");
        }

        // Counted by WorkflowExecution.StartedAt — the actual submission moment, set once and never
        // updated — rather than BookingRequest.CreatedAt (a Draft's creation time). Counting by
        // CreatedAt let a student stockpile drafts and submit a batch of week-old ones later, none
        // of which would count against each other or a fresh submission (Codex security review, P1).
        // A WorkflowExecution only ever exists for a request that was really submitted, so no status
        // filter is needed: once 7 days pass since submission it naturally drops out of the window.
        var weekStart = DateTimeOffset.UtcNow.AddDays(-7);
        var submissionsThisWeek = await db.WorkflowExecutions
            .AsNoTracking()
            .CountAsync(w => w.BookingRequest.StudentId == studentProfileId && w.StartedAt >= weekStart, ct);

        if (submissionsThisWeek >= profile.MaxBookingsPerWeek)
        {
            reasons.Add($"Weekly booking limit reached ({profile.MaxBookingsPerWeek} per week).");
        }

        return new EligibilityResult(reasons.Count == 0, reasons);
    }
}
