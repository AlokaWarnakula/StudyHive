using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;

namespace StudyHive.Api.Data;

/// <summary>
/// Seeds a deterministic, idempotent Development workspace: one login per role, a usable student
/// profile, the small preview consumables catalog shared with the mobile app, and representative S1
/// requests/workflows. The call site in Program.cs is guarded by IsDevelopment().
/// </summary>
public static class DevDataSeeder
{
    private static readonly Guid MarkersId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid PrintoutsId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid HdmiCableId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    private const string PlanJson = """
        {"steps":[{"step":1,"agent":"EligibilityAgent","action":"validate_eligibility"},{"step":2,"agent":"SchedulingAgent","action":"find_available_room"},{"step":3,"agent":"ResourceAgent","action":"reserve_consumables"},{"step":4,"agent":"ValidationAgent","action":"prepare_quotation"}]}
        """;

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var options = services.GetRequiredService<IOptions<DevSeedOptions>>().Value;
        if (options.Users.Count == 0) return;

        var db = services.GetRequiredService<StudyHiveDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();
        var seededUsers = new List<User>();

        // WebApplicationFactory starts one Development host per test class. Those hosts can seed
        // the same PostgreSQL database concurrently, so serialize the complete check/insert unit
        // across processes rather than relying on in-memory locks or catching unique violations.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1937449281)", ct);

        foreach (var seedUser in options.Users)
        {
            var email = seedUser.Email.Trim();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (user is null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    PasswordHash = passwordHasher.Hash(seedUser.Password),
                    FullName = seedUser.FullName,
                    Role = seedUser.Role,
                    IsActive = true,
                };
                db.Users.Add(user);
            }

            seededUsers.Add(user);
        }

        await db.SaveChangesAsync(ct);

        var studentUser = seededUsers.FirstOrDefault(u => u.Role == UserRole.Student);
        if (studentUser is null)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var studentProfile = await db.StudentProfiles.SingleOrDefaultAsync(p => p.UserId == studentUser.Id, ct);
        if (studentProfile is null)
        {
            studentProfile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = studentUser.Id,
                StudentNumber = $"DEV-{studentUser.Id:N}"[..20].ToUpperInvariant(),
                Department = "Computing",
                YearOfStudy = 2,
            };
            db.StudentProfiles.Add(studentProfile);
            await db.SaveChangesAsync(ct);
        }

        var now = DateTimeOffset.UtcNow;
        await EnsureConsumablesAsync(db, now, ct);

        var requestSeeds = CreateRequestSeeds(studentProfile.Id, now);
        foreach (var seed in requestSeeds)
        {
            if (!await db.BookingRequests.AnyAsync(r => r.Id == seed.Request.Id, ct))
            {
                db.BookingRequests.Add(seed.Request);
            }
        }
        await db.SaveChangesAsync(ct);

        foreach (var seed in requestSeeds)
        {
            foreach (var item in seed.Items)
            {
                if (!await db.BookingRequestItems.AnyAsync(i => i.Id == item.Id, ct))
                {
                    db.BookingRequestItems.Add(item);
                }
            }

            if (seed.Workflow is null) continue;

            if (!await db.WorkflowExecutions.AnyAsync(w => w.Id == seed.Workflow.Id, ct))
            {
                db.WorkflowExecutions.Add(seed.Workflow);
            }

            foreach (var step in seed.Steps)
            {
                if (!await db.WorkflowStepLogs.AnyAsync(s => s.Id == step.Id, ct))
                {
                    db.WorkflowStepLogs.Add(step);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task EnsureConsumablesAsync(StudyHiveDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var seeds = new[]
        {
            new Consumable { Id = MarkersId, Name = "Whiteboard markers", Description = "Black and blue dry-erase markers.", Unit = "marker", UnitPrice = 60m, StockQuantity = 42, MinStockLevel = 10, CreatedAt = now, UpdatedAt = now },
            new Consumable { Id = PrintoutsId, Name = "A4 printouts", Description = "Black-and-white A4 printing.", Unit = "page", UnitPrice = 5m, StockQuantity = 1200, MinStockLevel = 200, CreatedAt = now, UpdatedAt = now },
            new Consumable { Id = HdmiCableId, Name = "HDMI cable", Description = "Temporarily out of stock.", Unit = "cable", UnitPrice = 0m, StockQuantity = 0, MinStockLevel = 2, CreatedAt = now, UpdatedAt = now },
        };

        foreach (var seed in seeds)
        {
            var existing = await db.Consumables.SingleOrDefaultAsync(c => c.Id == seed.Id, ct);
            if (existing is null)
            {
                db.Consumables.Add(seed);
                continue;
            }

            // These stable IDs form the shared Development preview contract with the mobile UI.
            // Reconcile them on every pass so an older dev database cannot drift from the app.
            existing.Name = seed.Name;
            existing.Description = seed.Description;
            existing.Unit = seed.Unit;
            existing.UnitPrice = seed.UnitPrice;
            existing.StockQuantity = seed.StockQuantity;
            existing.MinStockLevel = seed.MinStockLevel;
            existing.IsActive = seed.IsActive;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private static IReadOnlyList<RequestSeed> CreateRequestSeeds(Guid studentId, DateTimeOffset now)
    {
        var preferredDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(5));
        var definitions = new[]
        {
            new RequestDefinition(1, "Draft a peer tutoring session for calculus revision", BookingRequestStatus.Draft, now.AddHours(-1), null, []),
            new RequestDefinition(2, "Reserve a quiet room for the database systems project", BookingRequestStatus.Processing, now.AddDays(-10), WorkflowStatus.InProgress, []),
            new RequestDefinition(3, "Run a software engineering group presentation rehearsal", BookingRequestStatus.PendingApproval, now.AddHours(-3), WorkflowStatus.PendingApproval, [(MarkersId, 1), (HdmiCableId, 1)]),
            new RequestDefinition(4, "Host a data structures exam review session", BookingRequestStatus.Approved, now.AddDays(-18), WorkflowStatus.Approved, [(PrintoutsId, 25)]),
            new RequestDefinition(5, "Organize a late-evening robotics planning meeting", BookingRequestStatus.Rejected, now.AddDays(-24), WorkflowStatus.Rejected, []),
            new RequestDefinition(6, "Plan a mobile application design critique", BookingRequestStatus.Cancelled, now.AddDays(-12), null, []),
            new RequestDefinition(7, "Complete a distributed systems study workshop", BookingRequestStatus.Completed, now.AddDays(-32), WorkflowStatus.Completed, [(MarkersId, 2)]),
            new RequestDefinition(8, "Arrange an operating systems mock viva", BookingRequestStatus.Failed, now.AddDays(-15), WorkflowStatus.Failed, []),
        };

        return definitions.Select(definition =>
        {
            var requestId = RequestId(definition.Number);
            var request = new BookingRequest
            {
                Id = requestId,
                StudentId = studentId,
                Objective = definition.Objective,
                GroupSize = definition.Number is 3 or 7 ? 8 : 4,
                PreferredDateFrom = preferredDate.AddDays(definition.Number - 1),
                PreferredDateTo = preferredDate.AddDays(definition.Number),
                PreferredTimeFrom = new TimeOnly(9, 0),
                PreferredTimeTo = new TimeOnly(12, 0),
                SessionsRequired = definition.Number is 4 or 7 ? 2 : 1,
                SessionDurationMinutes = 90,
                Budget = definition.Number is 3 or 4 ? 75m : 40m,
                Notes = "Development preview data",
                Status = definition.RequestStatus,
                CreatedAt = definition.StartedAt.AddMinutes(-10),
                UpdatedAt = definition.StartedAt.AddMinutes(12),
            };

            var items = definition.Items.Select((item, index) => new BookingRequestItem
            {
                Id = ItemId(definition.Number, index + 1),
                BookingRequestId = requestId,
                ConsumableId = item.ConsumableId,
                Quantity = item.Quantity,
                CreatedAt = definition.StartedAt,
            }).ToArray();

            if (definition.WorkflowStatus is null)
            {
                return new RequestSeed(request, items, null, []);
            }

            var workflowId = WorkflowId(definition.Number);
            var stepCount = definition.WorkflowStatus is WorkflowStatus.InProgress or WorkflowStatus.Failed ? 2 : 4;
            var terminal = definition.WorkflowStatus is not (WorkflowStatus.InProgress or WorkflowStatus.PendingApproval);
            var workflow = new WorkflowExecution
            {
                Id = workflowId,
                BookingRequestId = requestId,
                Objective = definition.Objective,
                PlanJson = PlanJson,
                CurrentStep = stepCount,
                TotalSteps = 4,
                Attempt = 1,
                Status = definition.WorkflowStatus.Value,
                ErrorCode = definition.WorkflowStatus == WorkflowStatus.Failed ? "SCHEDULING_UNAVAILABLE" : null,
                ErrorMessage = definition.WorkflowStatus == WorkflowStatus.Failed ? "No suitable room was available for the requested window." : null,
                StartedAt = definition.StartedAt,
                CompletedAt = terminal ? definition.StartedAt.AddMinutes(stepCount * 2) : null,
                UpdatedAt = definition.StartedAt.AddMinutes(stepCount * 2),
            };

            var steps = Enumerable.Range(1, stepCount)
                .Select(stepNumber => CreateStep(workflowId, definition.Number, stepNumber, definition.WorkflowStatus.Value, definition.StartedAt))
                .ToArray();

            return new RequestSeed(request, items, workflow, steps);
        }).ToArray();
    }

    private static WorkflowStepLog CreateStep(
        Guid workflowId,
        int requestNumber,
        int stepNumber,
        WorkflowStatus workflowStatus,
        DateTimeOffset startedAt)
    {
        var failed = workflowStatus == WorkflowStatus.Failed && stepNumber == 2;
        var rejected = workflowStatus == WorkflowStatus.Rejected && stepNumber == 4;
        var agentName = stepNumber switch
        {
            1 => "EligibilityAgent",
            2 => "SchedulingAgent",
            3 => "ResourceAgent",
            _ => "ValidationAgent",
        };
        var toolName = stepNumber switch
        {
            1 => "validate_eligibility",
            2 => "find_available_room",
            3 => "reserve_consumables",
            _ => "prepare_quotation",
        };

        return new WorkflowStepLog
        {
            Id = StepId(requestNumber, stepNumber),
            WorkflowExecutionId = workflowId,
            StepNumber = stepNumber,
            Attempt = 1,
            AgentName = agentName,
            ToolName = toolName,
            InputJson = $"{{\"step\":{stepNumber},\"source\":\"development-preview\"}}",
            OutputJson = failed
                ? "{\"available\":false,\"reason\":\"No matching room\"}"
                : rejected
                    ? "{\"approved\":false,\"reason\":\"Requested time is outside service hours\"}"
                    : $"{{\"step\":{stepNumber},\"ok\":true,\"stub\":true}}",
            ValidationResult = failed ? StepValidationResult.Fail : rejected ? StepValidationResult.Warning : StepValidationResult.Pass,
            ValidationDetails = failed ? "Scheduling could not satisfy the requested window." : rejected ? "Manual approval rejected the proposal." : "Preview step completed.",
            DurationMs = 120 + (stepNumber * 35),
            ErrorMessage = failed ? "No suitable room was available." : null,
            CreatedAt = startedAt.AddMinutes(stepNumber * 2),
        };
    }

    private static Guid RequestId(int number) => Guid.Parse($"10000000-0000-0000-0000-{number:D12}");
    private static Guid WorkflowId(int number) => Guid.Parse($"20000000-0000-0000-0000-{number:D12}");
    private static Guid ItemId(int requestNumber, int itemNumber) => Guid.Parse($"400000{requestNumber:D2}-0000-0000-0000-{itemNumber:D12}");
    private static Guid StepId(int requestNumber, int stepNumber) => Guid.Parse($"500000{requestNumber:D2}-0000-0000-0000-{stepNumber:D12}");

    private sealed record RequestDefinition(
        int Number,
        string Objective,
        BookingRequestStatus RequestStatus,
        DateTimeOffset StartedAt,
        WorkflowStatus? WorkflowStatus,
        (Guid ConsumableId, int Quantity)[] Items);

    private sealed record RequestSeed(
        BookingRequest Request,
        BookingRequestItem[] Items,
        WorkflowExecution? Workflow,
        WorkflowStepLog[] Steps);
}
