using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Services;

/// <summary>What a student's cancellation writes on the workflow it stops. There is no Cancelled
/// workflow status (ck_workflow_executions_status), so the workflow ends Rejected with this code.</summary>
public static class BookingCancellation
{
    public const string ErrorCode = "CANCELLED_BY_STUDENT";
    public const string ErrorMessage = "The student cancelled this request.";
}

/// <summary>
/// Closes room bookings whose slot has ended (AUDIT C-06, C-15): a Confirmed booking whose ends_at
/// has passed becomes Completed if the student checked in, otherwise NoShow. An Approved request
/// whose bookings are all Completed or NoShow becomes Completed, so it leaves the student's Active
/// list. Penalty points for no-shows are not applied in this release.
/// </summary>
public interface IBookingLifecycleService
{
    /// <summary>One sweep as of <paramref name="now"/>. Returns how many room bookings it closed.</summary>
    Task<int> CloseEndedBookingsAsync(DateTimeOffset now, CancellationToken ct);
}

public sealed class BookingLifecycleService(StudyHiveDbContext db) : IBookingLifecycleService
{
    public async Task<int> CloseEndedBookingsAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Set-based: each statement decides and writes in one step, so a check-in that commits
        // during the sweep is never marked NoShow (it is simply left Confirmed for the next sweep,
        // which then closes it as Completed).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var completed = await db.RoomBookings
            .Where(b => b.Status == RoomBookingStatus.Confirmed && b.EndsAt <= now && b.CheckedInAt != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, RoomBookingStatus.Completed)
                .SetProperty(b => b.UpdatedAt, now), ct);
        var noShows = await db.RoomBookings
            .Where(b => b.Status == RoomBookingStatus.Confirmed && b.EndsAt <= now && b.CheckedInAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, RoomBookingStatus.NoShow)
                .SetProperty(b => b.UpdatedAt, now), ct);

        // An Approved request with an ended booking and no Confirmed booking left is finished.
        await db.BookingRequests
            .Where(r => r.Status == BookingRequestStatus.Approved
                && db.RoomBookings.Any(b => b.BookingRequestId == r.Id && b.EndsAt <= now)
                && !db.RoomBookings.Any(b => b.BookingRequestId == r.Id && b.Status == RoomBookingStatus.Confirmed))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, BookingRequestStatus.Completed)
                .SetProperty(r => r.UpdatedAt, now), ct);

        await transaction.CommitAsync(ct);
        return completed + noShows;
    }
}

/// <summary>Runs <see cref="IBookingLifecycleService"/> every
/// <c>BookingLifecycle:IntervalMinutes</c> (default 5). The first sweep waits one interval.</summary>
public sealed class BookingLifecycleBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<BookingLifecycleBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = configuration.GetValue("BookingLifecycle:IntervalMinutes", 5);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, minutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<IBookingLifecycleService>();
                var closed = await lifecycle.CloseEndedBookingsAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (closed > 0) logger.LogInformation("Closed {Count} ended room bookings", closed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Room booking lifecycle sweep failed");
            }
        }
    }
}
