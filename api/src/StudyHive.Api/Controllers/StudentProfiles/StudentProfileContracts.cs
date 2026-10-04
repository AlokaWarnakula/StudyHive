using System.ComponentModel.DataAnnotations;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.StudentProfiles;

/// <summary>Self-service onboarding — a Student creates their own profile. Quota/penalty/active-state
/// fields are deliberately absent: only Admin (PUT) can change those.</summary>
public sealed class CreateStudentProfileRequest
{
    [Required, MaxLength(20)]
    public required string StudentNumber { get; init; }

    [Required, MaxLength(80)]
    public required string Department { get; init; }

    [Range(1, 5)]
    public int YearOfStudy { get; init; }
}

/// <summary>Student edits their own profile (PUT /me). Same limits as registration/onboarding;
/// quota/penalty/active-state fields are deliberately absent.</summary>
public sealed class UpdateOwnStudentProfileRequest
{
    [Required, MaxLength(150)]
    public required string FullName { get; init; }

    [Required, MaxLength(20)]
    public required string StudentNumber { get; init; }

    [Required, MaxLength(80)]
    public required string Department { get; init; }

    [Range(1, 5)]
    public int YearOfStudy { get; init; }
}

/// <summary>Admin-only. The full set of fields a staff member can adjust after onboarding.</summary>
public sealed class UpdateStudentProfileRequest
{
    [Required, MaxLength(80)]
    public required string Department { get; init; }

    [Range(1, 5)]
    public int YearOfStudy { get; init; }

    [Range(1, int.MaxValue)]
    public int MaxBookingsPerWeek { get; init; }

    [Range(0, int.MaxValue)]
    public int PenaltyPoints { get; init; }

    public DateOnly? SuspendedUntil { get; init; }

    public bool IsActive { get; init; }
}

public sealed class StudentProfileResponse
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    /// <summary>CW-08: who the profile belongs to, so staff can recognise the student.</summary>
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public required string StudentNumber { get; init; }
    public required string Department { get; init; }
    public required int YearOfStudy { get; init; }
    public required int MaxBookingsPerWeek { get; init; }
    public required int PenaltyPoints { get; init; }
    public required DateOnly? SuspendedUntil { get; init; }
    /// <summary>CW-08: suspended today, by the same rule eligibility uses (BookingEligibilityService.IsSuspended).</summary>
    public required bool IsSuspended { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>The profile's <c>User</c> must be loaded (Include) for the name and email.</summary>
    public static StudentProfileResponse From(StudentProfile profile) => new()
    {
        Id = profile.Id,
        UserId = profile.UserId,
        FullName = profile.User.FullName,
        Email = profile.User.Email,
        StudentNumber = profile.StudentNumber,
        Department = profile.Department,
        YearOfStudy = profile.YearOfStudy,
        MaxBookingsPerWeek = profile.MaxBookingsPerWeek,
        PenaltyPoints = profile.PenaltyPoints,
        SuspendedUntil = profile.SuspendedUntil,
        IsSuspended = BookingEligibilityService.IsSuspended(profile.SuspendedUntil),
        IsActive = profile.IsActive,
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt,
    };
}

public sealed class EligibilityResponse
{
    public required bool Eligible { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    /// <summary>C-16: requests submitted this Colombo week, and the weekly limit they count against.</summary>
    public required int UsedThisWeek { get; init; }
    public required int MaxBookingsPerWeek { get; init; }
}
