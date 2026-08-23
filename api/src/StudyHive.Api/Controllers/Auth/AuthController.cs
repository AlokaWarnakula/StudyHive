using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;

namespace StudyHive.Api.Controllers.Auth;

/// <summary>
/// Shared authentication/authorization plumbing (DOCS §01: "compulsory shared plumbing, not a
/// business component"). No S1-S4 business logic lives here.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    StudyHiveDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IAuthorizationService authorizationService,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthEndpoints)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        if ((request.Department is null) != (request.YearOfStudy is null))
        {
            ModelState.AddModelError(nameof(request.Department), "Department and yearOfStudy must be supplied together.");
            return ValidationProblem(ModelState);
        }
        if (request.Department is not null && string.IsNullOrWhiteSpace(request.Department))
        {
            ModelState.AddModelError(nameof(request.Department), "Department cannot be blank.");
            return ValidationProblem(ModelState);
        }

        var normalizedEmail = request.Email.Trim();
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
        {
            return Problem(
                type: "https://studyhive.dev/errors/conflict",
                title: "Email already registered",
                statusCode: StatusCodes.Status409Conflict,
                detail: "An account with this email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            FullName = request.FullName.Trim(),
            Role = UserRole.Student,
            IsActive = true,
        };
        db.Users.Add(user);

        if (request.Department is not null && request.YearOfStudy is not null)
        {
            db.StudentProfiles.Add(new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                StudentNumber = $"REG-{user.Id:N}"[..20].ToUpperInvariant(),
                Department = request.Department.Trim(),
                YearOfStudy = request.YearOfStudy.Value,
            });
        }

        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, UserResponse.From(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthEndpoints)]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // Same generic failure for "no such user", "wrong password" and "inactive account" —
        // never confirm whether an email is registered.
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return InvalidCredentials();
        }

        return await IssueTokensAsync(user, ct);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.AuthEndpoints)]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Sha256Hex(request.RefreshToken);
        var existing = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        var now = DateTimeOffset.UtcNow;
        if (existing is null || existing.RevokedAt is not null || existing.ExpiresAt <= now || !existing.User.IsActive)
        {
            return Problem(
                type: "https://studyhive.dev/errors/unauthorized",
                title: "Invalid refresh token",
                statusCode: StatusCodes.Status401Unauthorized,
                detail: "The refresh token is invalid, expired or has already been used.");
        }

        // Rotate: revoke the presented token so it cannot be replayed, then issue a fresh pair.
        existing.RevokedAt = now;
        return await IssueTokensAsync(existing.User, ct);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        var tokenHash = TokenHasher.Sha256Hex(request.RefreshToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        // Idempotent and silent either way — logging out an already-revoked or unknown token
        // must not leak whether it ever existed.
        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await db.Users.FindAsync([User.GetUserId()], ct);
        return user is null ? NotFound() : Ok(UserResponse.From(user));
    }

    /// <summary>Admin-only user directory. See DOCS §01: Admin "manage users, roles" and §12 list conventions.</summary>
    [HttpGet("users")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PagedResult<UserResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers([FromQuery] PageQuery query, CancellationToken ct)
    {
        IQueryable<User> users = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = $"%{query.Search.Trim()}%";
            users = users.Where(u => EF.Functions.ILike(u.Email, search) || EF.Functions.ILike(u.FullName, search));
        }

        var sortDescending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<User>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => sortDescending ? users.OrderByDescending(u => u.CreatedAt) : users.OrderBy(u => u.CreatedAt),
            "email" => sortDescending ? users.OrderByDescending(u => u.Email) : users.OrderBy(u => u.Email),
            "fullname" => sortDescending ? users.OrderByDescending(u => u.FullName) : users.OrderBy(u => u.FullName),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }
        users = sorted;

        var totalItems = await users.CountAsync(ct);
        var items = await users
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => UserResponse.From(u))
            .ToListAsync(ct);

        return Ok(PagedResult<UserResponse>.Create(items, query.Page, query.PageSize, totalItems));
    }

    /// <summary>"Own record, or staff" — the canonical ownership pattern used throughout DOCS §06-09.</summary>
    [HttpGet("users/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        var authResult = await authorizationService.AuthorizeAsync(User, new OwnedUser(user.Id), "ResourceOwner");
        if (!authResult.Succeeded) return Forbid();

        return Ok(UserResponse.From(user));
    }

    private async Task<IActionResult> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = jwtTokenService.GenerateAccessToken(user);

        var refreshTokenValue = TokenHasher.GenerateOpaqueToken();
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenHasher.Sha256Hex(refreshTokenValue),
            ExpiresAt = refreshTokenExpiresAt,
        });
        await db.SaveChangesAsync(ct);

        return Ok(new AuthTokenResponse
        {
            AccessToken = accessToken.Value,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            User = UserResponse.From(user),
        });
    }

    private ObjectResult InvalidCredentials() => Problem(
        type: "https://studyhive.dev/errors/unauthorized",
        title: "Invalid credentials",
        statusCode: StatusCodes.Status401Unauthorized,
        detail: "The email or password is incorrect.");

    private sealed record OwnedUser(Guid OwnerUserId) : IOwnedByUser;
}
