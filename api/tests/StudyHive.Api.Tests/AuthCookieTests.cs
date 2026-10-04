using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Controllers.Auth;
using StudyHive.Api.Data;

namespace StudyHive.Api.Tests;

/// <summary>
/// PLAN.md D1 / AUDIT W-01: with <c>?client=web</c> the refresh token lives in an httpOnly cookie
/// scoped to /api/auth and never appears in a response body; the mobile body-token flow is unchanged.
/// Cookies are read and sent by hand so every attribute can be asserted.
/// </summary>
public class AuthCookieTests(AuthCookieTests.Factory factory)
    : IClassFixture<AuthCookieTests.Factory>, IAsyncLifetime
{
    /// <summary>Own factory: the auth rate limiter (30/min per path) is per server instance.</summary>
    public sealed class Factory : WebApplicationFactory<Program>;

    private const string CookieName = "studyhive_refresh";
    private const string AllowedOrigin = "http://localhost:5173"; // appsettings.Development.json
    private readonly List<string> _emails = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        // RefreshTokens cascade-delete with their owning User.
        await db.Users.Where(u => _emails.Contains(u.Email)).ExecuteDeleteAsync();
    }

    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private async Task<string> NewStudentAsync(HttpClient client)
    {
        var email = TestSupport.UniqueEmail("cookie");
        _emails.Add(email);
        await TestSupport.RegisterStudentAsync(client, email);
        return email;
    }

    private static async Task<HttpResponseMessage> WebLoginAsync(HttpClient client, string email) =>
        await client.PostAsJsonAsync("/api/auth/login?client=web",
            new LoginRequest { Email = email, Password = TestSupport.Password });

    private static Task<HttpResponseMessage> WebPostAsync(HttpClient client, string path, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        if (cookie is not null) request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        return client.SendAsync(request);
    }

    private static string? RefreshSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.SingleOrDefault(v => v.StartsWith($"{CookieName}=", StringComparison.Ordinal))
            : null;

    private static string CookieValue(string setCookie) => setCookie.Split(';')[0][(CookieName.Length + 1)..];

    private static bool IsDeletion(string setCookie) =>
        CookieValue(setCookie).Length == 0 && setCookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> BodyHasRefreshToken(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("refreshToken", out _);
    }

    [Fact]
    public async Task Web_Login_Sets_An_HttpOnly_Secure_Lax_Cookie_Scoped_To_Auth_And_Leaves_The_Token_Out_Of_The_Body()
    {
        var client = Client();
        var email = await NewStudentAsync(client);

        var response = await WebLoginAsync(client, email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var setCookie = RefreshSetCookie(response);
        setCookie.Should().NotBeNull();
        var attributes = setCookie!.ToLowerInvariant();
        attributes.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=lax").And.Contain("path=/api/auth");
        CookieValue(setCookie).Should().NotBeNullOrWhiteSpace();
        (await BodyHasRefreshToken(response)).Should().BeFalse();
        var tokens = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(TestSupport.JsonOptions);
        tokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Web_Refresh_Reads_The_Cookie_Rotates_It_And_Rejects_The_Old_One()
    {
        var client = Client();
        var email = await NewStudentAsync(client);
        var first = CookieValue(RefreshSetCookie(await WebLoginAsync(client, email))!);

        var refreshed = await WebPostAsync(client, "/api/auth/refresh?client=web", first);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BodyHasRefreshToken(refreshed)).Should().BeFalse();
        var second = CookieValue(RefreshSetCookie(refreshed)!);
        second.Should().NotBeNullOrWhiteSpace().And.NotBe(first);

        var replay = await WebPostAsync(client, "/api/auth/refresh?client=web", first);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        IsDeletion(RefreshSetCookie(replay)!).Should().BeTrue("a rejected cookie is cleared");
    }

    [Fact]
    public async Task Web_Refresh_Without_A_Cookie_Is_401()
    {
        var response = await WebPostAsync(Client(), "/api/auth/refresh?client=web", cookie: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Web_Refresh_Requires_A_Json_Body_So_A_Cross_Site_Form_Post_Cannot_Use_The_Cookie()
    {
        var client = Client();
        var email = await NewStudentAsync(client);
        var cookie = CookieValue(RefreshSetCookie(await WebLoginAsync(client, email))!);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh?client=web")
        {
            Content = new StringContent("refreshToken=x", Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        request.Headers.Add("Cookie", $"{CookieName}={cookie}");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await WebPostAsync(client, "/api/auth/refresh?client=web", cookie)).StatusCode
            .Should().Be(HttpStatusCode.OK, "the rejected form post did not consume the token");
    }

    [Fact]
    public async Task Web_Logout_Revokes_The_Cookie_Token_And_Clears_The_Cookie()
    {
        var client = Client();
        var email = await NewStudentAsync(client);
        var cookie = CookieValue(RefreshSetCookie(await WebLoginAsync(client, email))!);

        var logout = await WebPostAsync(client, "/api/auth/logout?client=web", cookie);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        IsDeletion(RefreshSetCookie(logout)!).Should().BeTrue();
        (await WebPostAsync(client, "/api/auth/refresh?client=web", cookie)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_Mobile_Body_Token_Flow_Is_Unchanged_And_Sets_No_Cookie()
    {
        var client = Client();
        var email = await NewStudentAsync(client);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = TestSupport.Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        RefreshSetCookie(login).Should().BeNull();
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokenResponse>(TestSupport.JsonOptions))!;
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        RefreshSetCookie(refresh).Should().BeNull();
        (await refresh.Content.ReadFromJsonAsync<AuthTokenResponse>(TestSupport.JsonOptions))!.RefreshToken
            .Should().NotBeNullOrWhiteSpace();

        (await client.PostAsJsonAsync("/api/auth/refresh", new { })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "without ?client=web the body token is still required");
        (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cors_Allows_Credentials_Only_For_A_Configured_Origin()
    {
        var client = Client();

        var allowed = await client.SendAsync(Preflight(AllowedOrigin));
        allowed.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(AllowedOrigin);
        allowed.Headers.GetValues("Access-Control-Allow-Credentials").Should().Equal("true");

        var foreign = await client.SendAsync(Preflight("https://evil.example"));
        foreign.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        foreign.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();

        static HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/refresh?client=web");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type");
            return request;
        }
    }
}
