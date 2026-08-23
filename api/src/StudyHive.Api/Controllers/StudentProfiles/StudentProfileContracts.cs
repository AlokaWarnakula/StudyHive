using System.ComponentModel.DataAnnotations;
using StudyHive.Api.Data.Entities;

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
    public required string StudentNumber { get; init; }
    public required string Department { get; init; }
    public required int YearOfStudy { get; init; }
    public required int MaxBookingsPerWeek { get; init; }
    public required int PenaltyPoints { get; init; }
    public required DateOnly? SuspendedUntil { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public static StudentProfileResponse From(StudentProfile profile) => new()
    {
        Id = profile.Id,
        UserId = profile.UserId,
        StudentNumber = profile.StudentNumber,
        Department = profile.Department,
        YearOfStudy = profile.YearOfStudy,
        MaxBookingsPerWeek = profile.MaxBookingsPerWeek,
        PenaltyPoints = profile.PenaltyPoints,
        SuspendedUntil = profile.SuspendedUntil,
        IsActive = profile.IsActive,
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt,
    };
}

public sealed class EligibilityResponse
{
    public required bool Eligible { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
}
