using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Auth;

/// <summary>
/// Public self-registration always creates a Student account — there is no Role field. Staff
/// accounts (Librarian/StoreOfficer/Admin) are provisioned out of band (development seeds for
/// now; an Admin-facing user-management feature is future shared/S4 work), so open registration
/// can never mint a privileged account.
/// </summary>
public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required, MinLength(8), MaxLength(100)]
    public required string Password { get; init; }

    [Required, MaxLength(150)]
    public required string FullName { get; init; }

    // Optional for backwards compatibility with clients that still use the separate
    // student-profile onboarding endpoint. Current clients send both values so registration
    // leaves the student ready to create a booking request immediately.
    [MaxLength(80)]
    public string? Department { get; init; }

    [Range(1, 5)]
    public int? YearOfStudy { get; init; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

/// <summary>Same password rules as registration for the new one.</summary>
public sealed class ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required, MinLength(8), MaxLength(100)]
    public required string NewPassword { get; init; }
}

/// <summary>
/// Mobile sends the refresh token here. The web client (<c>?client=web</c>) sends <c>{}</c> and the
/// token comes from the httpOnly cookie instead; the JSON body is still required, which keeps a
/// cross-site form post (no CORS preflight) from using the cookie.
/// </summary>
public sealed class RefreshRequest
{
    public string? RefreshToken { get; init; }
}

/// <summary>Same shape and rules as <see cref="RefreshRequest"/>.</summary>
public sealed class LogoutRequest
{
    public string? RefreshToken { get; init; }
}

public sealed class AuthTokenResponse
{
    public required string AccessToken { get; init; }
    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    /// <summary>Left out for the web client, which gets it as an httpOnly cookie.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }

    public required DateTimeOffset RefreshTokenExpiresAt { get; init; }
    public required UserResponse User { get; init; }
}

public sealed class UserResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public required UserRole Role { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public static UserResponse From(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
    };
}
