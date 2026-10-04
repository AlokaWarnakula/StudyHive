using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;
using StudyHive.Api.Services;

namespace StudyHive.Api.Controllers.StudentProfiles;

/// <summary>S1: student eligibility profile. See DOCS §11 API table.</summary>
[ApiController]
[Route("api/student-profiles")]
[Authorize]
public sealed class StudentProfilesController(
    StudyHiveDbContext db,
    IBookingEligibilityService eligibilityService,
    IAuditWriter audit) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(typeof(StudentProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOwnProfile(CreateStudentProfileRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        if (await db.StudentProfiles.AnyAsync(p => p.UserId == userId, ct))
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Profile already exists",
                statusCode: StatusCodes.Status409Conflict,
                detail: "This account already has a student profile.");
        }

        var studentNumber = request.StudentNumber.Trim();
        if (await db.StudentProfiles.AnyAsync(p => p.StudentNumber == studentNumber, ct))
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Student number already registered",
                statusCode: StatusCodes.Status409Conflict,
                detail: "This student number is already registered to another account.");
        }

        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StudentNumber = studentNumber,
            Department = request.Department.Trim(),
            YearOfStudy = request.YearOfStudy,
        };
        db.StudentProfiles.Add(profile);
        profile.User = await db.Users.SingleAsync(u => u.Id == userId, ct);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, StudentProfileResponse.From(profile));
    }

    [HttpGet("me")]
    [Authorize(Policy = "StudentOnly")]
    [ProducesResponseType(typeof(StudentProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwnProfile(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var profile = await db.StudentProfiles.AsNoTracking().Include(p => p.User).SingleOrDefaultAsync(p => p.UserId == userId, ct);
        return profile is null ? NotFound() : Ok(StudentProfileResponse.From(profile));
    }

    [HttpGet]
    [Authorize(Roles = $"{Roles.Librarian},{Roles.Admin}")]
    [ProducesResponseType(typeof(PagedResult<StudentProfileResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        IQueryable<StudentProfile> profiles = db.StudentProfiles.AsNoTracking().Include(p => p.User);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // CW-08: staff look students up by name or email as often as by number.
            var search = $"%{query.Search.Trim()}%";
            profiles = profiles.Where(p =>
                EF.Functions.ILike(p.StudentNumber, search) || EF.Functions.ILike(p.Department, search) ||
                EF.Functions.ILike(p.User.FullName, search) || EF.Functions.ILike(p.User.Email, search));
        }

        var sortDescending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<StudentProfile>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => sortDescending ? profiles.OrderByDescending(p => p.CreatedAt) : profiles.OrderBy(p => p.CreatedAt),
            "studentnumber" => sortDescending ? profiles.OrderByDescending(p => p.StudentNumber) : profiles.OrderBy(p => p.StudentNumber),
            "department" => sortDescending ? profiles.OrderByDescending(p => p.Department) : profiles.OrderBy(p => p.Department),
            "fullname" => sortDescending ? profiles.OrderByDescending(p => p.User.FullName) : profiles.OrderBy(p => p.User.FullName),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }
        profiles = sorted;

        var totalItems = await profiles.CountAsync(ct);
        var items = await profiles
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return Ok(PagedResult<StudentProfileResponse>.Create(
            items.Select(StudentProfileResponse.From).ToList(), query.Page, query.PageSize, totalItems));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StudentProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var profile = await db.StudentProfiles.AsNoTracking().Include(p => p.User).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (profile is null) return NotFound();

        if (!IsOwnerOrStaffReader(profile.UserId)) return Forbid();

        return Ok(StudentProfileResponse.From(profile));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(StudentProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateStudentProfileRequest request, CancellationToken ct)
    {
        var profile = await db.StudentProfiles.Include(p => p.User).SingleOrDefaultAsync(p => p.Id == id, ct);
        if (profile is null) return NotFound();

        profile.Department = request.Department.Trim();
        profile.YearOfStudy = request.YearOfStudy;
        profile.MaxBookingsPerWeek = request.MaxBookingsPerWeek;
        profile.PenaltyPoints = request.PenaltyPoints;
        profile.SuspendedUntil = request.SuspendedUntil;
        profile.IsActive = request.IsActive;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        audit.Write("StudentProfileUpdated", "StudentProfile", profile.Id, new
        {
            profile.MaxBookingsPerWeek, profile.PenaltyPoints, profile.SuspendedUntil, profile.IsActive, profile.Department, profile.YearOfStudy,
        });

        await db.SaveChangesAsync(ct);
        return Ok(StudentProfileResponse.From(profile));
    }

    [HttpGet("{id:guid}/eligibility")]
    [ProducesResponseType(typeof(EligibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEligibility(Guid id, CancellationToken ct)
    {
        var profile = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        if (profile is null) return NotFound();

        if (!IsOwnerOrStaffReader(profile.UserId)) return Forbid();

        var result = await eligibilityService.EvaluateAsync(id, excludeBookingRequestId: null, ct);
        return Ok(new EligibilityResponse
        {
            Eligible = result.IsEligible,
            Reasons = result.Reasons,
            UsedThisWeek = result.UsedThisWeek,
            MaxBookingsPerWeek = result.MaxBookingsPerWeek,
        });
    }

    /// <summary>Deliberately not the shared "any staff role" ResourceOwner policy — DOCS §11 scopes
    /// student-profile reads to "Student (own), Librarian", not StoreOfficer (Codex security
    /// review, P1).</summary>
    private bool IsOwnerOrStaffReader(Guid ownerUserId) =>
        (User.TryGetUserId(out var callerId) && callerId == ownerUserId)
        || User.IsInRole(Roles.Librarian);
}
