using StudyHive.Api.Contracts;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

/// <summary>S2 test double used when API workflow tests should not call the FastAPI process.</summary>
public sealed class FakeSchedulingAgentClient : ISchedulingAgentClient
{
    public Func<SchedulingRequest, SchedulingResponse>? OnPropose { get; init; }
    public Exception? ThrowOnPropose { get; init; }

    public Task<SchedulingResponse> ProposeAsync(SchedulingRequest request, CancellationToken ct)
    {
        if (ThrowOnPropose is not null)
        {
            throw ThrowOnPropose;
        }

        if (OnPropose is not null)
        {
            return Task.FromResult(OnPropose(request));
        }

        var room = request.Rooms.FirstOrDefault(r =>
            r.IsActive && r.Capacity >= request.GroupSize);
        if (room is null)
        {
            return Task.FromResult(new SchedulingResponse
            {
                Slots = [],
                Conflicts = ["No suitable room is available."],
            });
        }

        var startsAt = new DateTimeOffset(
            request.PreferredDateFrom.ToDateTime(request.PreferredTimeFrom),
            TimeSpan.FromMinutes(330));

        return Task.FromResult(new SchedulingResponse
        {
            Slots =
            [
                new SchedulingSlot
                {
                    RoomId = room.RoomId,
                    RoomName = room.RoomName,
                    StartsAt = startsAt,
                    EndsAt = startsAt.AddMinutes(request.SessionDurationMinutes),
                    HourlyRate = room.HourlyRate,
                },
            ],
            Conflicts = [],
        });
    }
}
