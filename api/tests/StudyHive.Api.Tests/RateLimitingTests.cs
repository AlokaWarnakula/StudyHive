using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using StudyHive.Api.Controllers.Auth;
using StudyHive.Api.Controllers.BookingRequests;

namespace StudyHive.Api.Tests;

/// <summary>
/// Uses its own WebApplicationFactory instance (not shared via IClassFixture with
/// AuthControllerTests) — the fixed-window limiter is a singleton for the app's lifetime, so
/// sharing a factory would let this test's burst starve unrelated login/refresh calls elsewhere
/// in the suite.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Excessive_Registration_Attempts_From_One_Client_Get_429()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();
        var email = TestSupport.UniqueEmail("register-limit");
        Guid? userId = null;

        try
        {
            HttpResponseMessage? lastResponse = null;
            for (var i = 0; i < 31; i++)
            {
                lastResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
                {
                    Email = email,
                    Password = TestSupport.Password,
                    FullName = "Registration Limit Test",
                });

                if (i == 0)
                {
                    var registered = await lastResponse.Content.ReadFromJsonAsync<UserResponse>(TestSupport.JsonOptions);
                    userId = registered!.Id;
                }
            }

            lastResponse!.StatusCode.Should().Be((HttpStatusCode)429);
        }
        finally
        {
            if (userId is not null) await TestSupport.CleanupAsync(factory, userId.Value);
        }
    }

    [Fact]
    public async Task Excessive_Login_Attempts_From_One_Client_Get_429()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        // The policy allows 30/minute per client; TestServer requests all share one partition
        // (no real socket, so RemoteIpAddress is null), so 31 rapid attempts must trip it.
        for (var i = 0; i < 31; i++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest { Email = "nobody@studyhive.test", Password = "wrong" });
        }

        lastResponse!.StatusCode.Should().Be((HttpStatusCode)429);
    }

    /// <summary>Codex security review, P2: submit triggers a real agent-workflow run and was
    /// previously uncapped. The limiter is keyed by user id, so it trips regardless of each
    /// individual call's business-logic outcome (202 the first time, 409 after — see
    /// BookingRequestsControllerTests.Submitting_Twice_Returns_409).</summary>
    [Fact]
    public async Task Excessive_Submit_Attempts_From_One_User_Get_429()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var (user, _, token) = await TestSupport.CreateAndLoginStudentAsync(client);
        try
        {
            await TestSupport.CreateStudentProfileAsync(client, token);
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            var created = await client.PostAsJsonAsync("/api/booking-requests", new
            {
                objective = "Rate limit probe",
                groupSize = 2,
                preferredDateFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                preferredDateTo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                preferredTimeFrom = new TimeOnly(9, 0),
                preferredTimeTo = new TimeOnly(11, 0),
                sessionsRequired = 1,
                sessionDurationMinutes = 60,
                budget = 20m,
                items = Array.Empty<object>(),
            });
            var createdBody = await created.Content.ReadFromJsonAsync<BookingRequestResponse>(TestSupport.JsonOptions);

            HttpResponseMessage? lastResponse = null;
            // The policy allows 10/minute per user; 11 rapid attempts against the same request must
            // trip it regardless of whether each individual call is a 202 or a 409.
            for (var i = 0; i < 11; i++)
            {
                lastResponse = await client.PostAsync($"/api/booking-requests/{createdBody!.Id}/submit", null);
            }

            lastResponse!.StatusCode.Should().Be((HttpStatusCode)429);
        }
        finally
        {
            await TestSupport.CleanupAsync(factory, user.Id);
        }
    }
}
