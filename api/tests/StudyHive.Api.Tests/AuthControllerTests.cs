using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Controllers.Auth;
using StudyHive.Api.Data;

namespace StudyHive.Api.Tests;

public class AuthControllerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<string> _createdEmails = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_createdEmails.Count == 0) return;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        // RefreshTokens cascade-delete with their owning User (ON DELETE CASCADE).
        var userIds = await db.Users
            .Where(u => _createdEmails.Contains(u.Email))
            .Select(u => u.Id)
            .ToListAsync();
        db.StudentProfiles.RemoveRange(db.StudentProfiles.Where(p => userIds.Contains(p.UserId)));
        await db.SaveChangesAsync();
        db.Users.RemoveRange(db.Users.Where(u => userIds.Contains(u.Id)));
        await db.SaveChangesAsync();
    }

    private static string UniqueEmail() => $"auth-test-{Guid.NewGuid():N}@studyhive.test";
    private const string Password = "Correct-Horse-Battery-Staple-1";

    private async Task<UserResponse> RegisterAsync(HttpClient client, string email)
    {
        _createdEmails.Add(email);
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password,
            FullName = "Auth Test User",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions))!;
    }

    private static async Task<AuthTokenResponse> LoginAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task Register_Then_Login_Returns_Tokens_For_A_Student_Account()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();

        var registered = await RegisterAsync(client, email);
        registered.Role.Should().Be(Data.Entities.UserRole.Student); // public registration can never mint a staff account

        var tokens = await LoginAsync(client, email);
        tokens.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        tokens.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task Register_With_Onboarding_Fields_Creates_A_Student_Profile_Atomically()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        _createdEmails.Add(email);

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password,
            FullName = "Ready Student",
            Department = "Computing",
            YearOfStudy = 3,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var registered = (await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions))!;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var profile = await db.StudentProfiles.SingleAsync(p => p.UserId == registered.Id);
        profile.Department.Should().Be("Computing");
        profile.YearOfStudy.Should().Be(3);
        profile.StudentNumber.Should().StartWith("REG-");
    }

    [Fact]
    public async Task Register_With_Only_One_Onboarding_Field_Returns_400_And_Creates_Nothing()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        _createdEmails.Add(email);

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password,
            FullName = "Incomplete Student",
            Department = "Computing",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task Register_With_Duplicate_Email_Returns_409()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = Password,
            FullName = "Duplicate",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_401()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "not-the-password" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_With_Unknown_Email_Returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = UniqueEmail(), Password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_With_A_Revoked_Token_Returns_401()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);
        var tokens = await LoginAsync(client, email);

        // First use rotates (and revokes) the original refresh token.
        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replaying the now-revoked original token must fail.
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_With_An_Expired_Token_Returns_401()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);
        var tokens = await LoginAsync(client, email);

        var tokenHash = Security.TokenHasher.Sha256Hex(tokens.RefreshToken);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
            var refreshToken = await db.RefreshTokens.SingleAsync(t => t.TokenHash == tokenHash);
            refreshToken.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Revokes_The_Refresh_Token_So_It_Can_No_Longer_Be_Used()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);
        var tokens = await LoginAsync(client, email);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = tokens.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAfterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });

        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_Without_A_Token_Returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Student_Cannot_List_All_Users_AdminOnly_Policy_Rejects_The_Role()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await RegisterAsync(client, email);
        var tokens = await LoginAsync(client, email);

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await client.GetAsync("/api/auth/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_Cannot_View_Another_Students_Profile_Ownership_Check_Rejects_It()
    {
        var client = factory.CreateClient();
        var ownerEmail = UniqueEmail();
        var otherEmail = UniqueEmail();
        var owner = await RegisterAsync(client, ownerEmail);
        await RegisterAsync(client, otherEmail);
        var otherTokens = await LoginAsync(client, otherEmail);

        client.DefaultRequestHeaders.Authorization = new("Bearer", otherTokens.AccessToken);
        var response = await client.GetAsync($"/api/auth/users/{owner.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Student_Can_View_Their_Own_Profile_By_Id()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        var registered = await RegisterAsync(client, email);
        var tokens = await LoginAsync(client, email);

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var response = await client.GetAsync($"/api/auth/users/{registered.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions);
        body!.Id.Should().Be(registered.Id);
    }
}
