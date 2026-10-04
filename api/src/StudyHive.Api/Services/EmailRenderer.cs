using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Services;

/// <summary>
/// Renders an email_notifications row into a message at send time. Rows store no body, only the
/// template and booking request, so the text always reflects the committed data:
/// <list type="bullet">
/// <item>BookingApproved: the librarian's comment, every room booking (room, Asia/Colombo time)
/// and the approved quotation total.</item>
/// <item>BookingRejected / BookingRevisionRequested: the librarian's comment on the latest decision.</item>
/// <item>BookingValidationFailed: the latest workflow's error_message, which is the revision note.</item>
/// <item>BookingCancelled: the cancelled room bookings (room, Asia/Colombo time).</item>
/// <item>MaintenanceConflict: each Confirmed booking a maintenance window overlaps, with the window's
/// reason and times.</item>
/// </list>
/// Returns null when the row can never be rendered (its booking request is gone or the data it
/// needs does not exist), and the dispatcher fails the row instead of retrying it.
/// </summary>
public sealed class EmailRenderer(StudyHiveDbContext db)
{
    /// <summary>Asia/Colombo is a fixed UTC+05:30 with no daylight saving, so a fixed offset is
    /// exact and avoids depending on the container's time zone database.</summary>
    private static readonly TimeSpan Colombo = TimeSpan.FromMinutes(330);

    public async Task<EmailMessage?> RenderAsync(EmailNotification email, CancellationToken ct)
    {
        if (email.BookingRequestId is not { } requestId) return null;

        var request = await db.BookingRequests.AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new { r.Id, r.Objective, StudentName = r.Student.User.FullName })
            .SingleOrDefaultAsync(ct);
        if (request is null) return null;

        var paragraphs = email.Template switch
        {
            EmailTemplates.BookingApproved => await ApprovedAsync(requestId, ct),
            EmailTemplates.BookingRejected => await DecisionAsync(requestId, ApprovalDecisionType.Rejected,
                "A librarian rejected your booking request.", ct),
            EmailTemplates.BookingRevisionRequested => await DecisionAsync(requestId, ApprovalDecisionType.RevisionRequested,
                "A librarian asked you to change your booking request before it can be approved.", ct),
            EmailTemplates.BookingValidationFailed => await ValidationFailedAsync(requestId, ct),
            EmailTemplates.BookingCancelled => await CancelledAsync(requestId, ct),
            EmailTemplates.MaintenanceConflict => await MaintenanceConflictAsync(requestId, ct),
            _ => null,
        };
        if (paragraphs is null) return null;

        var all = new List<string> { $"Hello {request.StudentName},", $"Your request: {request.Objective}" };
        all.AddRange(paragraphs);
        all.Add("Open the StudyHive app to see the details.");

        return new EmailMessage(
            email.ToEmail,
            request.StudentName,
            email.Subject,
            HtmlBody: string.Concat(all.Select(p => $"<p>{WebUtility.HtmlEncode(p).Replace("\n", "<br>")}</p>")),
            TextBody: string.Join("\n\n", all));
    }

    private async Task<List<string>?> ApprovedAsync(Guid requestId, CancellationToken ct)
    {
        var quotation = await db.Quotations.AsNoTracking()
            .Where(q => q.BookingRequestId == requestId && q.Status == QuotationStatus.Approved)
            .Select(q => new
            {
                q.TotalAmount,
                q.Currency,
                Comments = q.ApprovalDecisions
                    .Where(d => d.Decision == ApprovalDecisionType.Approved)
                    .OrderByDescending(d => d.DecidedAt)
                    .Select(d => d.Comments)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(ct);
        if (quotation is null) return null;

        var bookings = await db.RoomBookings.AsNoTracking()
            .Where(b => b.BookingRequestId == requestId)
            .OrderBy(b => b.StartsAt)
            .Select(b => new { RoomName = b.Room.Name, b.StartsAt, b.EndsAt })
            .ToListAsync(ct);

        var lines = new List<string> { "Good news: a librarian approved your booking." };
        if (bookings.Count > 0)
        {
            var slots = new StringBuilder("Your room bookings:");
            foreach (var booking in bookings)
            {
                slots.Append('\n').Append($"- {booking.RoomName}: {FormatSlot(booking.StartsAt, booking.EndsAt)}");
            }
            lines.Add(slots.ToString());
        }
        lines.Add($"Total: {FormatMoney(quotation.Currency, quotation.TotalAmount)}");
        if (!string.IsNullOrWhiteSpace(quotation.Comments))
        {
            lines.Add($"Librarian's comment: {quotation.Comments}");
        }
        return lines;
    }

    private async Task<List<string>?> DecisionAsync(Guid requestId, ApprovalDecisionType decision, string opening, CancellationToken ct)
    {
        var comments = await db.ApprovalDecisions.AsNoTracking()
            .Where(d => d.Quotation.BookingRequestId == requestId && d.Decision == decision)
            .OrderByDescending(d => d.DecidedAt)
            .Select(d => new { d.Comments })
            .FirstOrDefaultAsync(ct);
        if (comments is null) return null;

        var lines = new List<string> { opening };
        if (!string.IsNullOrWhiteSpace(comments.Comments))
        {
            lines.Add($"Librarian's comment: {comments.Comments}");
        }
        return lines;
    }

    private async Task<List<string>?> ValidationFailedAsync(Guid requestId, CancellationToken ct)
    {
        var note = await db.WorkflowExecutions.AsNoTracking()
            .Where(w => w.BookingRequestId == requestId && w.Status == WorkflowStatus.Failed)
            .OrderByDescending(w => w.StartedAt)
            .Select(w => new { w.ErrorMessage })
            .FirstOrDefaultAsync(ct);
        if (note is null) return null;

        return
        [
            "Your booking request could not be approved as it stands, so it was not sent to a librarian.",
            $"What to change: {note.ErrorMessage ?? "See the app for details."}",
        ];
    }

    private async Task<List<string>?> CancelledAsync(Guid requestId, CancellationToken ct)
    {
        var bookings = await db.RoomBookings.AsNoTracking()
            .Where(b => b.BookingRequestId == requestId && b.Status == RoomBookingStatus.Cancelled)
            .OrderBy(b => b.StartsAt)
            .Select(b => new { RoomName = b.Room.Name, b.StartsAt, b.EndsAt })
            .ToListAsync(ct);
        if (bookings.Count == 0) return null;

        var slots = new StringBuilder("You cancelled your booking, so these room times are released:");
        foreach (var booking in bookings)
        {
            slots.Append('\n').Append($"- {booking.RoomName}: {FormatSlot(booking.StartsAt, booking.EndsAt)}");
        }
        return [slots.ToString()];
    }

    private async Task<List<string>?> MaintenanceConflictAsync(Guid requestId, CancellationToken ct)
    {
        var clashes = await (
                from b in db.RoomBookings.AsNoTracking()
                where b.BookingRequestId == requestId && b.Status == RoomBookingStatus.Confirmed
                from w in db.MaintenanceWindows.AsNoTracking()
                where w.RoomId == b.RoomId && w.StartsAt < b.EndsAt && w.EndsAt > b.StartsAt
                orderby b.StartsAt
                select new { RoomName = b.Room.Name, b.StartsAt, b.EndsAt, w.Reason, WindowStarts = w.StartsAt, WindowEnds = w.EndsAt })
            .ToListAsync(ct);
        // The window was changed or removed before the email went out: nothing to tell the student.
        if (clashes.Count == 0) return null;

        var text = new StringBuilder("The library has scheduled maintenance that overlaps your booking:");
        foreach (var clash in clashes)
        {
            text.Append('\n').Append($"- {clash.RoomName}: your booking {FormatSlot(clash.StartsAt, clash.EndsAt)}; " +
                $"maintenance ({clash.Reason}) {FormatSlot(clash.WindowStarts, clash.WindowEnds)}");
        }
        return [text.ToString(), "Please contact the library desk to move your booking."];
    }

    private static string FormatSlot(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        var start = startsAt.ToOffset(Colombo);
        var end = endsAt.ToOffset(Colombo);
        return $"{start.ToString("ddd d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)}–{end.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }

    private static string FormatMoney(string currency, decimal amount) =>
        $"{currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
}
