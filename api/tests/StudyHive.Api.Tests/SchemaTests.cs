using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Tests;

/// <summary>
/// Applies the InitialCreate migration to a real PostgreSQL database once per test run, then
/// exposes a fresh DbContext per test. Requires a reachable Postgres — `docker compose up -d db`
/// locally, or the CI workflow's postgres service (both use the same default connection string).
/// </summary>
public class SchemaTestFixture : IAsyncLifetime
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=studyhive;Username=studyhive;Password=studyhive_dev";

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public static StudyHiveDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StudyHiveDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new StudyHiveDbContext(options);
    }
}

/// <summary>
/// Verifies the locked schema from DOCS/StudyHive_Master_Project_Relay_Plan.html §10 against a
/// real database — the two DB-level guarantees the plan calls out as non-negotiable: the stock
/// oversell CHECK and the no-double-booking EXCLUDE constraint, plus a model/migration drift check.
/// </summary>
public class SchemaTests : IClassFixture<SchemaTestFixture>
{
    // The fixture constructor parameter is required by IClassFixture to trigger migration setup
    // via InitializeAsync before any test runs, even though tests build their own DbContext.
    public SchemaTests(SchemaTestFixture fixture) { }

    [Fact]
    public async Task Model_Has_No_Pending_Migrations()
    {
        await using var context = SchemaTestFixture.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty("the applied InitialCreate migration must match the current EF Core model");
    }

    [Fact]
    public async Task Consumable_ReservedQuantity_Exceeding_StockQuantity_Is_Rejected_By_CheckConstraint()
    {
        await using var context = SchemaTestFixture.CreateContext();

        var consumable = new Consumable
        {
            Id = Guid.NewGuid(),
            Name = $"Test Marker {Guid.NewGuid():N}",
            Unit = "piece",
            UnitPrice = 10m,
            StockQuantity = 5,
            ReservedQuantity = 10, // violates chk_never_oversold: reserved_quantity <= stock_quantity
        };
        context.Consumables.Add(consumable);

        var act = async () => await context.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>();
        var pgException = exception.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        pgException.SqlState.Should().Be(PostgresErrorCodes.CheckViolation); // 23514
        pgException.ConstraintName.Should().Be("chk_never_oversold");

        // The failed insert must not have persisted.
        (await context.Consumables.CountAsync(c => c.Id == consumable.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Overlapping_Confirmed_RoomBookings_Are_Rejected_And_Transaction_Rolls_Back()
    {
        await using var context = SchemaTestFixture.CreateContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"schema-test-{Guid.NewGuid():N}@studyhive.test",
            PasswordHash = "not-a-real-hash",
            FullName = "Schema Test Student",
            Role = UserRole.Student,
        };
        var student = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            StudentNumber = $"T{Guid.NewGuid():N}"[..12],
            Department = "Computing",
            YearOfStudy = 2,
        };
        var bookingRequest = new BookingRequest
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            Objective = "Schema test double-booking check",
            GroupSize = 2,
            PreferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            PreferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow),
            PreferredTimeFrom = new TimeOnly(9, 0),
            PreferredTimeTo = new TimeOnly(17, 0),
            SessionDurationMinutes = 60,
            Budget = 100m,
        };
        var room = new StudyRoom
        {
            Id = Guid.NewGuid(),
            Name = $"Schema-Test-{Guid.NewGuid():N}"[..20],
            Building = "Test Building",
            Floor = 1,
            Capacity = 10,
            HourlyRate = 5m,
            QrCode = $"QR-{Guid.NewGuid():N}",
        };
        context.AddRange(user, student, bookingRequest, room);
        await context.SaveChangesAsync();

        try
        {
            var slotStart = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
            var firstBooking = new RoomBooking
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                BookingRequestId = bookingRequest.Id,
                StartsAt = slotStart,
                EndsAt = slotStart.AddHours(1),
                Status = RoomBookingStatus.Confirmed,
            };

            await using var transaction = await context.Database.BeginTransactionAsync();

            context.RoomBookings.Add(firstBooking);
            await context.SaveChangesAsync();

            // Overlaps the first booking by 30 minutes on the same room — must be rejected by
            // the no_double_booking EXCLUDE USING gist constraint.
            var overlappingBooking = new RoomBooking
            {
                Id = Guid.NewGuid(),
                RoomId = room.Id,
                BookingRequestId = bookingRequest.Id,
                StartsAt = slotStart.AddMinutes(30),
                EndsAt = slotStart.AddMinutes(90),
                Status = RoomBookingStatus.Confirmed,
            };
            context.RoomBookings.Add(overlappingBooking);

            var act = async () => await context.SaveChangesAsync();

            var exception = await act.Should().ThrowAsync<DbUpdateException>();
            var pgException = exception.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
            pgException.SqlState.Should().Be(PostgresErrorCodes.ExclusionViolation); // 23P01
            pgException.ConstraintName.Should().Be("no_double_booking");

            await transaction.RollbackAsync();

            // The whole transaction rolled back, so even the first (valid) booking must be gone.
            await using var verifyContext = SchemaTestFixture.CreateContext();
            (await verifyContext.RoomBookings.CountAsync(b => b.RoomId == room.Id)).Should().Be(0);
        }
        finally
        {
            await using var cleanup = SchemaTestFixture.CreateContext();
            cleanup.RoomBookings.RemoveRange(cleanup.RoomBookings.Where(b => b.RoomId == room.Id));
            await cleanup.SaveChangesAsync();
            cleanup.BookingRequests.Remove(cleanup.BookingRequests.Single(b => b.Id == bookingRequest.Id));
            cleanup.StudyRooms.Remove(cleanup.StudyRooms.Single(r => r.Id == room.Id));
            await cleanup.SaveChangesAsync();
            cleanup.StudentProfiles.Remove(cleanup.StudentProfiles.Single(s => s.Id == student.Id));
            await cleanup.SaveChangesAsync();
            cleanup.Users.Remove(cleanup.Users.Single(u => u.Id == user.Id));
            await cleanup.SaveChangesAsync();
        }
    }
}
