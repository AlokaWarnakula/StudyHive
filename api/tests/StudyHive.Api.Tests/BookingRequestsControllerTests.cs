using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using StudyHive.Api.Common;
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

    /// <summary>Sets the student's penalty points via the Admin-only profile endpoint, then submits
    /// their draft as themselves. Returns the submit response so the caller can assert on it.</summary>
    private async Task<HttpResponseMessage> SubmitWithPenaltyPointsAsync(HttpClient client, int penaltyPoints)
    {
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
            penaltyPoints,
            suspendedUntil = (DateOnly?)null,
            isActive = true,
        });

        client.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        return await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);
    }

    /// <summary>DOCS Master Plan: eligible means "fewer than 3 penalty points", so 3 is the first
    /// value that blocks a submission.</summary>
    [Fact]
    public async Task Submit_Fails_With_422_At_Three_Penalty_Points()
    {
        var client = factory.CreateClient();

        var response = await SubmitWithPenaltyPointsAsync(client, penaltyPoints: 3);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>The boundary the plan actually draws: 2 points is still eligible. This is the case
    /// the old "any penalty point blocks you" rule got wrong.</summary>
    [Fact]
    public async Task Submit_Is_Allowed_At_Two_Penalty_Points()
    {
        var fake = new FakePlannerClient();
        await using var localFactory = CreateFactoryWithFakePlanner(fake);
        var client = localFactory.CreateClient();

        var (_, studentToken, profileId) = await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var (adminId, _, adminToken) = await TestSupport.CreateAndLoginStaffAsync(localFactory, client, UserRole.Admin);
        _createdUserIds.Add(adminId);
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        await client.PutAsJsonAsync($"/api/student-profiles/{profileId}", new
        {
            department = "Computing",
            yearOfStudy = 2,
            maxBookingsPerWeek = 3,
            penaltyPoints = 2,
            suspendedUntil = (DateOnly?)null,
            isActive = true,
        });

        client.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        var response = await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
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
        var roomId = Guid.NewGuid();
        await using var localFactory = CreateFactoryWithFakePlanner(fake, SchedulingInRoom(roomId));
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();
        await CreateSchedulingRoomAsync(localFactory, roomId, hourlyRate: 20m);

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
        await DeleteSchedulingRoomAsync(localFactory, roomId);
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

    // --- S4: validation step, quotation persistence and the quotation_line_items shape ----------

    [Fact]
    public async Task Submit_Persists_A_Proposed_Quotation_With_A_Room_Line_And_A_Consumable_Line()
    {
        var roomId = Guid.NewGuid();
        await using var localFactory = CreateFactoryWithFakePlanner(new FakePlannerClient(), SchedulingInRoom(roomId));
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();
        await CreateSchedulingRoomAsync(localFactory, roomId, hourlyRate: 100m);
        var consumableId = await CreateConsumableAsync(localFactory, unitPrice: 2.50m, stock: 20);

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var requestId = await CreateAndSubmitAsync(client, budget: 500m, consumableId, quantity: 4);

        var status = await WaitForTerminalStatusAsync(client, requestId, TimeSpan.FromSeconds(10));

        status.Status.Should().Be("PendingApproval");
        status.Steps.Should().HaveCount(4);
        status.Steps[3].AgentName.Should().Be("Validation");
        status.Steps[3].ValidationResult.Should().Be("Pass");

        using (var scope = localFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var quotation = await db.Quotations.AsNoTracking().Include(q => q.LineItems)
                .SingleAsync(q => q.BookingRequestId == requestId);

            quotation.Status.Should().Be(QuotationStatus.Proposed);
            quotation.Version.Should().Be(1);
            quotation.BudgetSnapshot.Should().Be(500m);
            quotation.RoomFee.Should().Be(200m);          // 2h x 100
            quotation.ConsumableCost.Should().Be(10m);    // 4 x 2.50
            quotation.TotalAmount.Should().Be(210m);      // generated by PostgreSQL
            quotation.WithinBudget.Should().BeTrue();
            quotation.LineItems.Sum(l => l.LineTotal).Should().Be(quotation.TotalAmount);

            var roomLine = quotation.LineItems.Single(l => l.ItemType == QuotationLineItemType.Room);
            roomLine.RoomId.Should().Be(roomId);
            roomLine.RoomBookingId.Should().BeNull("the booking is only created by the approval transaction");
            roomLine.ConsumableId.Should().BeNull();
            roomLine.Quantity.Should().Be(2m);
            roomLine.UnitPrice.Should().Be(100m);
            roomLine.LineTotal.Should().Be(200m);

            var consumableLine = quotation.LineItems.Single(l => l.ItemType == QuotationLineItemType.Consumable);
            consumableLine.ConsumableId.Should().Be(consumableId);
            consumableLine.RoomId.Should().BeNull();
            consumableLine.RoomBookingId.Should().BeNull();
            consumableLine.LineTotal.Should().Be(10m);

            (await db.StockReservations.AsNoTracking().SingleAsync(r => r.ConsumableId == consumableId))
                .Status.Should().Be(StockReservationStatus.Pending);
        }

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
        await DeleteConsumableAsync(localFactory, consumableId);
        await DeleteSchedulingRoomAsync(localFactory, roomId);
    }

    [Fact]
    public async Task Submit_Fails_With_VALIDATION_FAILED_And_Writes_No_Quotation_When_Over_Budget()
    {
        var roomId = Guid.NewGuid();
        await using var localFactory = CreateFactoryWithFakePlanner(new FakePlannerClient(), SchedulingInRoom(roomId));
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();
        await CreateSchedulingRoomAsync(localFactory, roomId, hourlyRate: 100m);
        var consumableId = await CreateConsumableAsync(localFactory, unitPrice: 1m, stock: 20);

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var requestId = await CreateAndSubmitAsync(client, budget: 50m, consumableId, quantity: 2);

        var status = await WaitForTerminalStatusAsync(client, requestId, TimeSpan.FromSeconds(10));

        status.Status.Should().Be("Failed");
        status.ErrorCode.Should().Be("VALIDATION_FAILED");
        status.ErrorMessage.Should().StartWith("Please revise this request before it can be approved:")
            .And.Contain("exceeds the budget of 50.00");
        status.Steps.Should().HaveCount(4);
        status.Steps[3].ValidationResult.Should().Be("Fail");

        using (var scope = localFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            (await db.Quotations.AnyAsync(q => q.BookingRequestId == requestId)).Should().BeFalse();
            (await db.BookingRequests.SingleAsync(r => r.Id == requestId)).Status.Should().Be(BookingRequestStatus.Failed);

            var reservation = await db.StockReservations.AsNoTracking().SingleAsync(r => r.ConsumableId == consumableId);
            reservation.Status.Should().Be(StockReservationStatus.Released);
            reservation.ReleasedAt.Should().NotBeNull();
            (await db.Consumables.AsNoTracking().SingleAsync(c => c.Id == consumableId)).ReservedQuantity.Should().Be(0);
        }

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
        await DeleteConsumableAsync(localFactory, consumableId);
        await DeleteSchedulingRoomAsync(localFactory, roomId);
    }

    [Fact]
    public async Task Submit_Fails_Safely_When_The_Validation_Agent_Is_Unreachable()
    {
        var roomId = Guid.NewGuid();
        var validation = new FakeValidationClient { ThrowOnValidate = new HttpRequestException("connection refused") };
        await using var localFactory = CreateFactoryWithFakePlanner(new FakePlannerClient(), SchedulingInRoom(roomId), validation);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();
        await CreateSchedulingRoomAsync(localFactory, roomId, hourlyRate: 10m);

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var requestId = await CreateAndSubmitAsync(client, budget: 500m);

        var status = await WaitForTerminalStatusAsync(client, requestId, TimeSpan.FromSeconds(10));

        status.Status.Should().Be("Failed");
        status.ErrorCode.Should().Be("STEP_RETRY_EXHAUSTED");
        status.ErrorMessage.Should().Contain("Validation call failed");
        var limits = localFactory.Services.GetRequiredService<IOptions<WorkflowLimitsOptions>>().Value;
        validation.Calls.Should().Be(limits.MaxRetriesPerStep + 1);
        await AssertNoQuotationAsync(localFactory, requestId);

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
        await DeleteSchedulingRoomAsync(localFactory, roomId);
    }

    [Fact]
    public async Task A_Validation_Response_Whose_Total_Does_Not_Match_Its_Lines_Is_Retried_And_Never_Persisted()
    {
        var roomId = Guid.NewGuid();
        var validation = new FakeValidationClient
        {
            OnValidate = req =>
            {
                var honest = FakeValidationClient.Price(req);
                return new ValidationResponse
                {
                    Valid = true,
                    Results = honest.Results,
                    Failures = [],
                    Quotation = new ValidationQuotation
                    {
                        RoomFee = honest.Quotation.RoomFee,
                        ConsumableCost = honest.Quotation.ConsumableCost,
                        Total = 0m, // "approve this for free"
                        LineItems = honest.Quotation.LineItems,
                    },
                };
            },
        };
        await using var localFactory = CreateFactoryWithFakePlanner(new FakePlannerClient(), SchedulingInRoom(roomId), validation);
        var client = localFactory.CreateClient();
        var userIds = new List<Guid>();
        await CreateSchedulingRoomAsync(localFactory, roomId, hourlyRate: 10m);

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        userIds.Add(user.Id);
        await TestSupport.CreateStudentProfileAsync(client, token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var requestId = await CreateAndSubmitAsync(client, budget: 500m);

        var status = await WaitForTerminalStatusAsync(client, requestId, TimeSpan.FromSeconds(10));

        status.Status.Should().Be("Failed");
        status.ErrorCode.Should().Be("STEP_RETRY_EXHAUSTED");
        status.ErrorMessage.Should().Contain("do not match its line items");
        var limits = localFactory.Services.GetRequiredService<IOptions<WorkflowLimitsOptions>>().Value;
        validation.Calls.Should().Be(limits.MaxRetriesPerStep + 1);
        await AssertNoQuotationAsync(localFactory, requestId);

        await TestSupport.CleanupAsync(localFactory, userIds.ToArray());
        await DeleteSchedulingRoomAsync(localFactory, roomId);
    }

    [Fact]
    public async Task Quotation_Line_Shape_Allows_A_Room_Line_Before_Its_Booking_And_Rejects_Malformed_Lines()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var requestId = (await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions))!.Id;
        var roomId = Guid.NewGuid();
        await CreateSchedulingRoomAsync(factory, roomId, hourlyRate: 33.33m);
        var consumableId = await CreateConsumableAsync(factory, unitPrice: 0.35m, stock: 10);

        try
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var quotationId = Guid.NewGuid();
            db.Quotations.Add(new Quotation
            {
                Id = quotationId,
                BookingRequestId = requestId,
                RoomFee = 27.66m,        // 0.83h x 33.33
                ConsumableCost = 1.05m,  // 3 x 0.35
                BudgetSnapshot = 50m,
                Status = QuotationStatus.Proposed,
                LineItems =
                [
                    new QuotationLineItem { Id = Guid.NewGuid(), ItemType = QuotationLineItemType.Room, RoomId = roomId, ItemName = "Room", Quantity = 0.83m, UnitPrice = 33.33m },
                    new QuotationLineItem { Id = Guid.NewGuid(), ItemType = QuotationLineItemType.Consumable, ConsumableId = consumableId, ItemName = "Item", Quantity = 3m, UnitPrice = 0.35m },
                ],
            });
            await db.SaveChangesAsync();

            var stored = await db.Quotations.AsNoTracking().Include(q => q.LineItems).SingleAsync(q => q.Id == quotationId);
            stored.LineItems.Select(l => l.LineTotal).Should().BeEquivalentTo([27.66m, 1.05m]);
            stored.TotalAmount.Should().Be(stored.LineItems.Sum(l => l.LineTotal));

            await AssertLineShapeRejectedAsync(factory, new QuotationLineItem
            {
                Id = Guid.NewGuid(), QuotationId = quotationId, ItemType = QuotationLineItemType.Room,
                ItemName = "Room with no room", Quantity = 1m, UnitPrice = 1m,
            });
            await AssertLineShapeRejectedAsync(factory, new QuotationLineItem
            {
                Id = Guid.NewGuid(), QuotationId = quotationId, ItemType = QuotationLineItemType.Consumable,
                ConsumableId = consumableId, RoomId = roomId, ItemName = "Consumable that names a room", Quantity = 1m, UnitPrice = 1m,
            });
            await AssertLineShapeRejectedAsync(factory, new QuotationLineItem
            {
                Id = Guid.NewGuid(), QuotationId = quotationId, ItemType = QuotationLineItemType.Room,
                RoomId = roomId, ConsumableId = consumableId, ItemName = "Room that names a consumable", Quantity = 1m, UnitPrice = 1m,
            });
        }
        finally
        {
            await TestSupport.CleanupAsync(factory, _createdUserIds.ToArray());
            _createdUserIds.Clear();
            await DeleteConsumableAsync(factory, consumableId);
            await DeleteSchedulingRoomAsync(factory, roomId);
        }
    }

    private static async Task AssertLineShapeRejectedAsync(WebApplicationFactory<Program> factory, QuotationLineItem line)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        db.QuotationLineItems.Add(line);

        var act = () => db.SaveChangesAsync();

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var postgres = thrown.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        postgres.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        postgres.ConstraintName.Should().Be("chk_line_shape");
    }

    private static async Task AssertNoQuotationAsync(WebApplicationFactory<Program> factory, Guid requestId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        (await db.Quotations.AnyAsync(q => q.BookingRequestId == requestId)).Should().BeFalse();
    }

    private static async Task<Guid> CreateAndSubmitAsync(HttpClient client, decimal budget, Guid? consumableId = null, int quantity = 0)
    {
        var created = await client.PostAsJsonAsync("/api/booking-requests", new
        {
            objective = "Group study session for a database systems assignment",
            groupSize = 4,
            preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            preferredTimeFrom = new TimeOnly(9, 0),
            preferredTimeTo = new TimeOnly(11, 0),
            sessionsRequired = 1,
            sessionDurationMinutes = 120,
            budget,
            notes = (string?)null,
            items = consumableId is { } id ? new[] { new { consumableId = id, quantity } } : [],
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var submit = await client.PostAsync($"/api/booking-requests/{body!.Id}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return body.Id;
    }

    /// <summary>Scheduling fake that always proposes the one test room, so the quotation's room
    /// line is deterministic even when other rooms (demo data, parallel tests) exist.</summary>
    private static FakeSchedulingAgentClient SchedulingInRoom(Guid roomId) => new()
    {
        OnPropose = request =>
        {
            var room = request.Rooms.Single(r => r.RoomId == roomId);
            var startsAt = new DateTimeOffset(
                request.PreferredDateFrom.ToDateTime(request.PreferredTimeFrom),
                TimeSpan.FromMinutes(330));
            return new SchedulingResponse
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
            };
        },
    };

    private static async Task<Guid> CreateConsumableAsync(WebApplicationFactory<Program> factory, decimal unitPrice, int stock)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        var consumable = new Consumable
        {
            Id = Guid.NewGuid(),
            Name = $"Workflow test item {Guid.NewGuid():N}",
            Unit = "pcs",
            UnitPrice = unitPrice,
            StockQuantity = stock,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Consumables.Add(consumable);
        await db.SaveChangesAsync();
        return consumable.Id;
    }

    private static async Task DeleteConsumableAsync(WebApplicationFactory<Program> factory, Guid consumableId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        await db.StockReservations.Where(r => r.ConsumableId == consumableId).ExecuteDeleteAsync();
        await db.Consumables.Where(c => c.Id == consumableId).ExecuteDeleteAsync();
    }

    private static WebApplicationFactory<Program> CreateFactoryWithFakePlanner(
        FakePlannerClient fake,
        FakeSchedulingAgentClient? scheduling = null,
        FakeValidationClient? validation = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPlannerClient>();
                services.AddSingleton<IPlannerClient>(fake);
                services.RemoveAll<ISchedulingAgentClient>();
                services.AddSingleton<ISchedulingAgentClient>(scheduling ?? new FakeSchedulingAgentClient());
                services.RemoveAll<IResourceClient>();
                services.AddSingleton<IResourceClient>(new FakeResourceClient());
                services.RemoveAll<IValidationClient>();
                services.AddSingleton<IValidationClient>(validation ?? new FakeValidationClient());
            });
        });

    private static async Task CreateSchedulingRoomAsync(WebApplicationFactory<Program> factory, Guid roomId, decimal hourlyRate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        var room = new StudyRoom
        {
            Id = roomId,
            Name = $"Workflow test room {Guid.NewGuid():N}",
            Building = "Test building",
            Floor = 1,
            Capacity = 50,
            HourlyRate = hourlyRate,
            QrCode = $"workflow-test-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.StudyRooms.Add(room);
        await db.SaveChangesAsync();
    }

    private static async Task DeleteSchedulingRoomAsync(
        WebApplicationFactory<Program> factory,
        Guid roomId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        await db.StudyRooms.Where(r => r.Id == roomId).ExecuteDeleteAsync();
    }

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

    /// <summary>Seeds, for one request: version 1 Rejected with a RevisionRequested decision, version 2
    /// (Proposed, or Approved with its own decision when <paramref name="decideLatest"/>), and a
    /// version 3 Draft the student must never be pointed at. Returns the version-2 id. The decisions
    /// are removed by <see cref="DeleteDecisionsAsync"/>, because approval_decisions is RESTRICT on
    /// both quotations and users.</summary>
    private async Task<Guid> SeedQuotationHistoryAsync(Guid requestId, Guid librarianId, bool decideLatest = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var now = DateTimeOffset.UtcNow;
        Quotation Quote(int version, QuotationStatus status, decimal consumableCost) => new()
        {
            Id = Guid.NewGuid(),
            BookingRequestId = requestId,
            Version = version,
            RoomFee = 0m,
            ConsumableCost = consumableCost,
            BudgetSnapshot = 50m,
            Status = status,
            CreatedAt = now.AddMinutes(version),
            UpdatedAt = now.AddMinutes(version),
        };
        var rejected = Quote(1, QuotationStatus.Rejected, 80m);
        var proposed = Quote(2, decideLatest ? QuotationStatus.Approved : QuotationStatus.Proposed, 40m);
        db.Quotations.AddRange(rejected, proposed, Quote(3, QuotationStatus.Draft, 10m));
        if (decideLatest)
        {
            db.ApprovalDecisions.Add(new ApprovalDecision
            {
                Id = Guid.NewGuid(),
                QuotationId = proposed.Id,
                DecidedBy = librarianId,
                DecidedByRole = "Librarian",
                Decision = ApprovalDecisionType.Approved,
                Comments = "Confirmed, keep the room tidy",
                DecidedAt = now.AddMinutes(3),
            });
        }
        db.ApprovalDecisions.Add(new ApprovalDecision
        {
            Id = Guid.NewGuid(),
            QuotationId = rejected.Id,
            DecidedBy = librarianId,
            DecidedByRole = "Librarian",
            Decision = ApprovalDecisionType.RevisionRequested,
            Comments = "Over budget: drop some markers",
            DecidedAt = now.AddMinutes(1),
        });
        await db.SaveChangesAsync();
        return proposed.Id;
    }

    private async Task DeleteDecisionsAsync(Guid requestId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        await db.ApprovalDecisions.Where(d => d.Quotation.BookingRequestId == requestId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Request_Has_No_Quotation_Or_Decision_Before_One_Is_Proposed()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions);

        var detail = await client.GetFromJsonAsync<BookingRequestS4Shape>($"/api/booking-requests/{createdBody!.Id}", TestSupport.JsonOptions);

        detail!.LatestQuotation.Should().BeNull();
        detail.LatestDecision.Should().BeNull();
    }

    [Fact]
    public async Task Student_And_Librarian_See_The_Latest_Quotation_Without_An_Older_Versions_Decision()
    {
        var client = factory.CreateClient();
        var (_, studentToken, _) = await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var requestId = (await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions))!.Id;
        var (librarianId, _, librarianToken) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        var proposedId = await SeedQuotationHistoryAsync(requestId, librarianId);

        try
        {
            foreach (var token in new[] { studentToken, librarianToken })
            {
                client.DefaultRequestHeaders.Authorization = new("Bearer", token);

                var detail = await client.GetFromJsonAsync<BookingRequestS4Shape>($"/api/booking-requests/{requestId}", TestSupport.JsonOptions);
                var listed = (await client.GetFromJsonAsync<PagedResultShape<BookingRequestS4Shape>>(
                    "/api/booking-requests?pageSize=100", TestSupport.JsonOptions))!.Items.Single(r => r.Id == requestId);

                foreach (var body in new[] { detail!, listed })
                {
                    // The newest non-Draft version, never the Draft behind it.
                    body.LatestQuotation!.Id.Should().Be(proposedId);
                    body.LatestQuotation.Version.Should().Be(2);
                    body.LatestQuotation.Status.Should().Be("Proposed");
                    body.LatestQuotation.TotalAmount.Should().Be(40m);
                    body.LatestQuotation.BudgetSnapshot.Should().Be(50m);
                    body.LatestQuotation.WithinBudget.Should().BeTrue();
                    body.LatestQuotation.Currency.Should().Be("LKR");
                    // Version 2 is undecided: version 1's RevisionRequested must not be carried over.
                    body.LatestDecision.Should().BeNull();
                }
            }
        }
        finally
        {
            await DeleteDecisionsAsync(requestId);
        }
    }

    [Fact]
    public async Task Latest_Decision_Is_The_Decision_On_The_Latest_Quotation()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var requestId = (await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions))!.Id;
        var (librarianId, _, _) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        var approvedId = await SeedQuotationHistoryAsync(requestId, librarianId, decideLatest: true);

        try
        {
            var detail = await client.GetFromJsonAsync<BookingRequestS4Shape>($"/api/booking-requests/{requestId}", TestSupport.JsonOptions);
            var listed = (await client.GetFromJsonAsync<PagedResultShape<BookingRequestS4Shape>>(
                "/api/booking-requests?pageSize=100", TestSupport.JsonOptions))!.Items.Single(r => r.Id == requestId);

            foreach (var body in new[] { detail!, listed })
            {
                body.LatestQuotation!.Id.Should().Be(approvedId);
                body.LatestQuotation.Status.Should().Be("Approved");
                body.LatestDecision!.Decision.Should().Be("Approved");
                body.LatestDecision.Comments.Should().Be("Confirmed, keep the room tidy");
            }
        }
        finally
        {
            await DeleteDecisionsAsync(requestId);
        }
    }

    [Fact]
    public async Task Another_Student_Cannot_Reach_A_Quotation_Or_Decision_Through_Booking_Requests()
    {
        var client = factory.CreateClient();
        await CreateEligibleStudentAsync(client);
        var created = await client.PostAsJsonAsync("/api/booking-requests", ValidRequestBody());
        var requestId = (await created.Content.ReadFromJsonAsync<BookingRequestResponseShape>(TestSupport.JsonOptions))!.Id;
        var (librarianId, _, _) = await TestSupport.CreateAndLoginStaffAsync(factory, client, UserRole.Librarian);
        _createdUserIds.Add(librarianId);
        var proposedId = await SeedQuotationHistoryAsync(requestId, librarianId);

        try
        {
            var (otherUser, _, otherToken) = await TestSupport.CreateAndLoginStudentAsync(client);
            _createdUserIds.Add(otherUser.Id);
            await TestSupport.CreateStudentProfileAsync(client, otherToken);
            client.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);

            var detail = await client.GetAsync($"/api/booking-requests/{requestId}");
            detail.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await detail.Content.ReadAsStringAsync()).Should().NotContain(proposedId.ToString());

            var list = await client.GetStringAsync("/api/booking-requests?pageSize=100");
            list.Should().NotContain(requestId.ToString());
            list.Should().NotContain(proposedId.ToString());
            list.Should().NotContain("Over budget: drop some markers");
        }
        finally
        {
            await DeleteDecisionsAsync(requestId);
        }
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

internal sealed class FakeResourceClient : IResourceClient
{
    public Task<ResourceResponse> PrepareReservationAsync(ResourceRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResourceResponse
        {
            AllAvailable = true,
            TotalCost = 0m,
            Items = []
        });
    }
}

internal sealed class BookingRequestS4Shape
{
    public Guid Id { get; init; }
    public BookingQuotationSummaryShape? LatestQuotation { get; init; }
    public BookingDecisionSummaryShape? LatestDecision { get; init; }
}

internal sealed class BookingQuotationSummaryShape
{
    public Guid Id { get; init; }
    public string Status { get; init; } = "";
    public int Version { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "";
    public decimal BudgetSnapshot { get; init; }
    public bool WithinBudget { get; init; }
}

internal sealed class BookingDecisionSummaryShape
{
    public string Decision { get; init; } = "";
    public string? Comments { get; init; }
    public DateTimeOffset DecidedAt { get; init; }
}
