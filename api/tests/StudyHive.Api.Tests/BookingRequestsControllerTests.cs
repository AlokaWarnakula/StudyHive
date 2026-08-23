using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudyHive.Api.Contracts;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Tests;

public class BookingRequestsControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly List<Guid> _createdUserIds = [];

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());

    private static object ValidRequestBody() => new
    {
        objective = "Group study session for a database systems assignment",
        groupSize = 4,
        preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        preferredTimeFrom = new TimeOnly(9, 0),
        preferredTimeTo = new TimeOnly(11, 0),
        sessionsRequired = 1,
        sessionDurationMinutes = 120,
        budget = 50m,
        notes = (string?)null,
        items = Array.Empty<object>(),
    };

    private async Task<(Guid UserId, string Token, Guid StudentProfileId)> CreateEligibleStudentAsync(HttpClient client)
    {
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        var profile = await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (user.Id, token, profile.Id);
    }

    [Fact]
    public async Task Student_Can_Create_A_Draft_Request()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);

        var response = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);
        body!.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Create_Without_A_Student_Profile_Returns_422()
    {
        var client = factory.CreateClient();
        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(user.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_With_End_Date_Before_Start_Date_Returns_400()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);

        var response = await client.PostAsJsonAsync("/api/booking-requests", new
        {
            objective = "Bad date range",
            groupSize = 2,
            preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 60,
            budget = 20m,
            items = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_With_Zero_Budget_Returns_400()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);

        var response = await client.PostAsJsonAsync("/api/booking-requests", new
        {
            objective = "Zero budget",
            groupSize = 2,
            preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 60,
            budget = 0m,
            items = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Student_Only_Sees_Their_Own_Requests_While_Librarian_Sees_All()
    {
        var client = factory.CreateClient();
        var (_, studentToken, _) = await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var (otherUser, _, otherToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(otherUser.Id);
        await TestSupport.CreateStudentProfileAsync(client, otherToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);

        var otherList = await client.GetFromJsonAsync<PagedResultShape<BookingRequestResponseShape>>(
            "/api/booking-requests?pageSize=100", TestSupport.JsonOptions);
        otherList!.Items.Should().NotContain(r => r.Id == createdBody!.Id);

        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", librarianToken);

        var librarianList = await client.GetFromJsonAsync<PagedResultShape<BookingRequestResponseShape>>(
            "/api/booking-requests?pageSize=100", TestSupport.JsonOptions);
        librarianList!.Items.Should().Contain(r => r.Id == createdBody!.Id);
    }

    [Fact]
    public async Task Student_Cannot_View_Another_Students_Request()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var (otherUser, _, otherToken) = await TestSupport.CreateAndLoginStudentAsync(client);
        _createdUserIds.Add(otherUser.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);

        var response = await client.GetAsync($"/api/booking-requests/{createdBody!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Draft_Request_Can_Be_Updated_But_Not_After_It_Is_No_Longer_Draft()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var updateResponse = await client.PutAsJsonAsync($"/api/booking-requests/{createdBody!.Id}", new
        {
            objective = "Updated objective",
            groupSize = 5,
            preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)),
            preferredTimeFrom = new TimeOnly(10, 0),
            preferredTimeTo = new TimeOnly(12, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 90,
            budget = 75m,
            items = Array.Empty<object>(),
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);
        updated!.Objective.Should().Be("Updated objective");

        var cancelResponse = await client.DeleteAsync($"/api/booking-requests/{createdBody.Id}");
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reUpdateResponse = await client.PutAsJsonAsync($"/api/booking-requests/{createdBody.Id}", ValidRequestBody());
        reUpdateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancelling_An_Already_Cancelled_Request_Returns_409()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        (await client.DeleteAsync($"/api/booking-requests/{createdBody!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var second = await client.DeleteAsync($"/api/booking-requests/{createdBody.Id}");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Submit_Fails_With_422_When_Student_Has_Penalty_Points()
    {
        var client = factory.CreateClient();
        var (_, studentToken, profileId) = await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/student-profiles/{profileId}", new
        {
            department = "Computing",
            yearOfStudy = 2,
            maxBookingsPerWeek = 3,
            penaltyPoints = 1,
            suspendedUntil = (DateOnly?)null,
            isActive = true,
        });

        client.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        var response = await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Submitting_Twice_Returns_409()
    {
        var fake = new FakePlannerClient();
        await using var localFactory = CreateFactoryWithFakePlanner(fake);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var first = await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var second = await client.PostAsync($"/api/booking-requests/{createdBody.Id}/submit", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
    }

    [Fact]
    public async Task Submit_Runs_The_Workflow_To_PendingApproval_With_Four_Step_Logs()
    {
        var fake = new FakePlannerClient
        {
            OnPlan = req => new PlannerResponse
            {
                PlanId = Guid.NewGuid(),
                Eligible = true,
                Reasons = [],
                Steps = [new PlannerStep { N = 1, Agent = "Planner", Action = "create_plan", Params = new Dictionary<string, object?>() }],
            },
        };
        await using var localFactory = CreateFactoryWithFakePlanner(fake);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var submitResponse = await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var status = await WaitForTerminalStatusAsync(client, createdBody.Id, TimeSpan.FromSeconds(10));

        status.Status.Should().Be("PendingApproval");
        status.Steps.Should().HaveCount(4);
        status.Steps.Select(s => s.AgentName).Should().Equal("Planner", "Scheduling", "Resource", "Validation");
        status.Steps.Should().OnlyContain(s => s.ValidationResult == "Pass");

        var requestResponse = await client.GetAsync($"/api/booking-requests/{createdBody.Id}");
        var requestBody = await requestResponse.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);
        requestBody!.Status.Should().Be("PendingApproval");

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
    }

    [Fact]
    public async Task Submit_Is_Rejected_When_The_Planner_Reports_Ineligible()
    {
        var fake = new FakePlannerClient
        {
            OnPlan = req => new PlannerResponse
            {
                PlanId = Guid.NewGuid(),
                Eligible = false,
                Reasons = ["Weekly booking limit reached."],
                Steps = [],
            },
        };
        await using var localFactory = CreateFactoryWithFakePlanner(fake);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);
        var status = await WaitForTerminalStatusAsync(client, createdBody.Id, TimeSpan.FromSeconds(10));

        status.Status.Should().Be("Rejected");
        status.ErrorCode.Should().Be("INELIGIBLE");

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
    }

    [Fact]
    public async Task Submit_Fails_Safely_When_The_Planner_Is_Unreachable()
    {
        var fake = new FakePlannerClient { ThrowOnPlan = new HttpRequestException("connection refused") };
        await using var localFactory = CreateFactoryWithFakePlanner(fake);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);
        var status = await WaitForTerminalStatusAsync(client, createdBody.Id, TimeSpan.FromSeconds(10));

        status.Status.Should().Be("Failed");
        status.ErrorCode.Should().Be("STEP_RETRY_EXHAUSTED");

        var requestResponse = await client.GetAsync($"/api/booking-requests/{createdBody.Id}");
        var requestBody = await requestResponse.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);
        requestBody!.Status.Should().Be("Failed");

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
    }

    [Fact]
    public async Task StoreOfficer_Cannot_List_Booking_Requests()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());

        var (storeOfficerId, _, storeOfficerToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.StoreOfficer);
        _createdUserIds.Add(storeOfficerId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", storeOfficerToken);

        var response = await client.GetAsync("/api/booking-requests?pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StoreOfficer_Cannot_View_Or_Track_Another_Students_Request()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var (storeOfficerId, _, storeOfficerToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.StoreOfficer);
        _createdUserIds.Add(storeOfficerId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", storeOfficerToken);

        (await client.GetAsync($"/api/booking-requests/{createdBody!.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/booking-requests/{createdBody.Id}/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Cannot_Read_Student_Booking_Requests()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);

        (await client.GetAsync("/api/booking-requests?pageSize=100")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/booking-requests/{createdBody!.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/booking-requests/{createdBody.Id}/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_With_Duplicate_Consumable_Ids_Returns_422_Not_500()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var consumableId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/booking-requests", new
        {
            objective = "Duplicate items",
            groupSize = 2,
            preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 60,
            budget = 20m,
            items = new[] { new { consumableId, quantity = 1 }, new { consumableId, quantity = 2 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_With_A_Nonexistent_Consumable_Id_Returns_422_Not_500()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);

        var response = await client.PostAsJsonAsync("/api/booking-requests", new
        {
            objective = "Unknown item",
            groupSize = 2,
            preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 60,
            budget = 20m,
            items = new[] { new { consumableId = Guid.NewGuid(), quantity = 1 } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Weekly_Quota_Is_Enforced_By_Submission_Time_Not_By_Backdated_Draft_Creation_Time()
    {
        var fake = new FakePlannerClient
        {
            OnPlan = req => new PlannerResponse
            {
                PlanId = Guid.NewGuid(),
                Eligible = true,
                Reasons = [],
                Steps = [new PlannerStep { N = 1, Agent = "Planner", Action = "create_plan", Params = new Dictionary<string, object?>() }],
            },
        };
        await using var localFactory = CreateFactoryWithFakePlanner(fake);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token); // MaxBookingsPerWeek defaults to 3
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var requestIds = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
            var body = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);
            requestIds.Add(body!.Id);
        }

        // Backdate every draft's CreatedAt to well outside the 7-day window. Under the old
        // CreatedAt-based count this let a student stockpile old drafts and submit all of them
        // later without ever tripping the weekly limit (Codex security review, P1) — the count now
        // comes from WorkflowExecution.StartedAt (set at submit time), so backdating the draft has
        // no effect on enforcement.
        using (var scope = localFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var requests = await db.BookingRequests.Where(r => requestIds.Contains(r.Id)).ToListAsync();
            foreach (var r in requests) r.CreatedAt = DateTimeOffset.UtcNow.AddDays(-30);
            await db.SaveChangesAsync();
        }

        var results = new List<HttpStatusCode>();
        foreach (var id in requestIds)
        {
            var response = await client.PostAsync($"/api/booking-requests/{id}/submit", null);
            results.Add(response.StatusCode);
        }

        results.Take(3).Should().AllSatisfy(status => status.Should().Be(HttpStatusCode.Accepted));
        results[3].Should().Be(HttpStatusCode.UnprocessableEntity);

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
    }

    [Fact]
    public async Task Concurrent_Submits_Cannot_Exceed_The_Weekly_Quota()
    {
        var fake = new FakePlannerClient
        {
            OnPlan = req => new PlannerResponse
            {
                PlanId = Guid.NewGuid(),
                Eligible = true,
                Reasons = [],
                Steps = [new PlannerStep { N = 1, Agent = "Planner", Action = "create_plan", Params = new Dictionary<string, object?>() }],
            },
        };
        await using var localFactory = CreateFactoryWithFakePlanner(fake);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token); // MaxBookingsPerWeek defaults to 3
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var requestIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
            var body = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);
            requestIds.Add(body!.Id);
        }

        // Fire all five submits at once for the same student — without the FOR UPDATE row lock in
        // BookingRequestsController.Submit, each request's eligibility check can read the count
        // before any of the others have committed their WorkflowExecution, letting all five pass
        // (Codex security review, P1).
        var responses = await Task.WhenAll(requestIds.Select(id => client.PostAsync($"/api/booking-requests/{id}/submit", null)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Accepted).Should().Be(3);
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(2);

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
    }

    private static WebApplicationFactory<Program> CreateFactoryWithFakePlanner(FakePlannerClient fake) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlannerClient>();
                services.AddSingleton<IPlannerClient>(fake);
            });
        });

    private static async Task<WorkflowStatusResponseShape> WaitForTerminalStatusAsync(HttpClient client, Guid requestId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var response = await client.GetAsync($"/api/booking-requests/{requestId}/status");
            if (response.IsSuccessStatusCode)
            {
                var status = await response.Content.ReadFromJsonAsync<WorkflowStatusResponseShape>(TestSupport.JsonOptions);
                if (status is { Status: "PendingApproval" or "Rejected" or "Failed" or "Completed" or "Approved" })
                {
                    return status;
                }
            }
            await Task.Delay(150);
        }
        throw new TimeoutException($"Workflow for request {requestId} did not reach a terminal status within {timeout}.");
    }
}

internal sealed class BookingRequestResponseShape
{
    public Guid Id { get; init; }
    public Guid StudentId { get; init; }
    public string Objective { get; init; } = "";
    public string Status { get; init; } = "";
    public decimal Budget { get; init; }
}

internal sealed class PagedResultShape<T>
{
    public List<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
}

internal sealed class WorkflowStatusResponseShape
{
    public Guid WorkflowId { get; init; }
    public string Status { get; init; } = "";
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public List<WorkflowStepLogShape> Steps { get; init; } = [];
}

internal sealed class WorkflowStepLogShape
{
    public int StepNumber { get; init; }
    public string AgentName { get; init; } = "";
    public string? ToolName { get; init; }
    public string? ValidationResult { get; init; }
}
