using System.ComponentModel.DataAnnotations;
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
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public sealed class RefreshRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}

public sealed class LogoutRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}

public sealed class AuthTokenResponse
{
    public required string AccessToken { get; init; }
    public required DateTimeOffset AccessTokenExpiresAt { get; init; }
    public required string RefreshToken { get; init; }
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
