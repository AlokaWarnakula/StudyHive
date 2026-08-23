using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHive.Api.Controllers.Auth;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;

namespace StudyHive.Api.Tests;

/// <summary>Shared fixtures for controller tests that need more than one role. Public registration
/// (see AuthControllerTests) can only ever create a Student, so staff accounts are inserted directly.</summary>
internal static class TestSupport
{
    public const string Password = "Correct-Horse-Battery-Staple-1";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string UniqueEmail(string prefix = "test") => $"{prefix}-{Guid.NewGuid():N}@studyhive.test";

    public static async Task<UserResponse> RegisterStudentAsync(HttpClient client, string? email = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email ?? UniqueEmail("student"),
            Password = Password,
            FullName = "Test Student",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>(JsonOptions))!;
    }

    public static async Task<AuthTokenResponse> LoginAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions))!;
    }

    public static async Task<(UserResponse User, string Email, string AccessToken)> CreateAndLoginStudentAsync(HttpClient client)
    {
        var email = UniqueEmail("student");
        var user = await RegisterStudentAsync(client, email);
        var tokens = await LoginAsync(client, email);
        return (user, email, tokens.AccessToken);
    }

    public static async Task<(Guid UserId, string Email, string AccessToken)> CreateAndLoginStaffAsync(
        WebApplicationFactory<Program> factory, HttpClient client, UserRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = UniqueEmail(role.ToString().ToLowerInvariant());
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hasher.Hash(Password),
            FullName = $"Test {role}",
            Role = role,
            IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var tokens = await LoginAsync(client, email);
        return (user.Id, email, tokens.AccessToken);
    }

    public static async Task<StudentProfileResponseShape> CreateStudentProfileAsync(
        HttpClient client, string accessToken, string? studentNumber = null)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        var response = await client.PostAsJsonAsync("/api/student-profiles", new
        {
            studentNumber = studentNumber ?? $"S{Guid.NewGuid():N}"[..12],
            department = "Computing",
            yearOfStudy = 2,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StudentProfileResponseShape>(JsonOptions))!;
    }

    /// <summary>Deletes everything created under the given user ids, respecting FK order
    /// (WorkflowExecutions -> BookingRequests -> StudentProfiles -> Users; step logs and request
    /// items cascade with their parents).</summary>
    public static async Task CleanupAsync(WebApplicationFactory<Program> factory, params Guid[] userIds)
    {
        if (userIds.Length == 0) return;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();

        var profileIds = await db.StudentProfiles.Where(p => userIds.Contains(p.UserId)).Select(p => p.Id).ToListAsync();
        if (profileIds.Count > 0)
        {
            var requestIds = await db.BookingRequests.Where(r => profileIds.Contains(r.StudentId)).Select(r => r.Id).ToListAsync();
            if (requestIds.Count > 0)
            {
                db.WorkflowExecutions.RemoveRange(db.WorkflowExecutions.Where(w => requestIds.Contains(w.BookingRequestId)));
                await db.SaveChangesAsync();
                db.BookingRequests.RemoveRange(db.BookingRequests.Where(r => requestIds.Contains(r.Id)));
                await db.SaveChangesAsync();
            }
            db.StudentProfiles.RemoveRange(db.StudentProfiles.Where(p => profileIds.Contains(p.Id)));
            await db.SaveChangesAsync();
        }

        db.Users.RemoveRange(db.Users.Where(u => userIds.Contains(u.Id)));
        await db.SaveChangesAsync();
    }
}

/// <summary>Minimal shape for deserializing StudentProfileResponse in tests without a cross-project reference.</summary>
internal sealed class StudentProfileResponseShape
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string StudentNumber { get; init; } = "";
    public string Department { get; init; } = "";
    public int YearOfStudy { get; init; }
    public int MaxBookingsPerWeek { get; init; }
    public int PenaltyPoints { get; init; }
    public DateOnly? SuspendedUntil { get; init; }
    public bool IsActive { get; init; }
}
