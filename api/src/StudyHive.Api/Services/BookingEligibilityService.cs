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
    /// <summary>DOCS Master Plan: a student holding this many penalty points or more is not eligible.</summary>
    private const int MaxPenaltyPoints = 3;

    /// <summary>The plan counts a student's weekly submissions with
    /// <c>date_trunc('week', now() AT TIME ZONE 'Asia/Colombo')</c>, so the quota resets on Monday
    /// 00:00 Sri Lanka time — the university's own week — not on a rolling 7-day tail and not at
    /// UTC midnight. Everything else here works in UTC, so the Colombo week boundary is converted
    /// back to a UTC instant for comparison against <c>WorkflowExecution.StartedAt</c>.</summary>
    private static readonly TimeSpan ColomboOffset = TimeSpan.FromMinutes(330); // UTC+05:30, no DST

    private static DateTimeOffset CurrentWeekStart()
    {
        var nowInColombo = DateTimeOffset.UtcNow.ToOffset(ColomboOffset);
        // date_trunc('week', ...) is ISO-8601: weeks start on Monday.
        var daysSinceMonday = ((int)nowInColombo.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var mondayMidnight = new DateTimeOffset(nowInColombo.Date, ColomboOffset).AddDays(-daysSinceMonday);
        return mondayMidnight.ToUniversalTime();
    }

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

        // DOCS Master Plan, S1 eligibility: "A student is eligible when they are active, not
        // suspended, under max_bookings_per_week, and hold fewer than 3 penalty points." Three is
        // the threshold, not one — a student carrying 1 or 2 points may still book. The §11 handoff
        // line's looser phrase "no outstanding penalties" is a summary of this rule, not a second
        // rule, and reading it literally used to reject students the plan allows.
        if (profile.PenaltyPoints >= MaxPenaltyPoints)
        {
            reasons.Add($"Student has {profile.PenaltyPoints} penalty points (limit is {MaxPenaltyPoints}).");
        }

        // Counted by WorkflowExecution.StartedAt — the actual submission moment, set once and never
        // updated — rather than BookingRequest.CreatedAt (a Draft's creation time). Counting by
        // CreatedAt let a student stockpile drafts and submit a batch of week-old ones later, none
        // of which would count against each other or a fresh submission (Codex security review, P1).
        // A WorkflowExecution only ever exists for a request that was really submitted, so no status
        // filter is needed: once the week rolls over it naturally drops out of the window.
        var weekStart = CurrentWeekStart();
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
