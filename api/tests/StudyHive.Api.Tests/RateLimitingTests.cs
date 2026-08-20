using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using StudyHive.Api.Controllers.Auth;

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
}
