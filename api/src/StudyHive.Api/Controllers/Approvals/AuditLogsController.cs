using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyHive.Api.Common;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Controllers.Approvals;

/// <summary>
/// S4: the append-only audit log (W-08). Read-only by design: rows are written by the operations
/// they record (today the approval decision in ApprovalsController), never through this API.
///
/// House rules (DOCS/S2_S3_S4_UI_Interface_Map.md): lists take [FromQuery] PageQuery and return
/// PagedResult&lt;T&gt; with filtering, sorting, counting and paging in SQL; unknown sortBy is a 400;
/// errors are RFC 7807 from the global handler.
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize]
public sealed class AuditLogsController(StudyHiveDbContext db) : ControllerBase
{
    /// <summary>List audit entries, newest first. Filters: `action` and `entityType` match exactly
    /// (case-insensitive), `entityId`, `userId`, and a `from`/`to` window on createdAt.</summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PagedResult<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] PageQuery query,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        IQueryable<AuditLog> logs = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var wanted = action.Trim().ToLower();
            logs = logs.Where(a => a.Action.ToLower() == wanted);
        }
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var wanted = entityType.Trim().ToLower();
            logs = logs.Where(a => a.EntityType.ToLower() == wanted);
        }
        if (entityId is { } entity)
        {
            logs = logs.Where(a => a.EntityId == entity);
        }
        if (userId is { } user)
        {
            logs = logs.Where(a => a.UserId == user);
        }
        if (from is { } fromValue && to is { } toValue && toValue <= fromValue)
        {
            ModelState.AddModelError(nameof(to), "to must be later than from.");
            return ValidationProblem(ModelState);
        }
        if (from is { } start)
        {
            var startUtc = start.ToUniversalTime();
            logs = logs.Where(a => a.CreatedAt >= startUtc);
        }
        if (to is { } end)
        {
            var endUtc = end.ToUniversalTime();
            logs = logs.Where(a => a.CreatedAt < endUtc);
        }

        var descending = !string.Equals(query.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<AuditLog>? sorted = query.SortBy?.ToLowerInvariant() switch
        {
            null or "" or "createdat" => descending ? logs.OrderByDescending(a => a.CreatedAt) : logs.OrderBy(a => a.CreatedAt),
            "action" => descending ? logs.OrderByDescending(a => a.Action) : logs.OrderBy(a => a.Action),
            "entitytype" => descending ? logs.OrderByDescending(a => a.EntityType) : logs.OrderBy(a => a.EntityType),
            _ => null,
        };
        if (sorted is null)
        {
            ModelState.AddModelError(nameof(query.SortBy), $"Unknown sortBy value '{query.SortBy}'.");
            return ValidationProblem(ModelState);
        }

        var totalItems = await sorted.CountAsync(ct);
        var rows = await sorted.ThenBy(a => a.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                UserEmail = a.User == null ? null : a.User.Email,
                a.CorrelationId,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Details,
                a.IpAddress,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        // `details` is jsonb; it is returned as JSON rather than as an escaped string.
        var items = rows.Select(a => new AuditLogResponse
        {
            Id = a.Id,
            UserId = a.UserId,
            UserEmail = a.UserEmail,
            CorrelationId = a.CorrelationId,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Details = ParseDetails(a.Details),
            IpAddress = a.IpAddress,
            CreatedAt = a.CreatedAt,
        }).ToList();

        return Ok(PagedResult<AuditLogResponse>.Create(items, query.Page, query.PageSize, totalItems));
    }

    private static JsonElement? ParseDetails(string? details)
    {
        if (details is null) return null;
        using var document = JsonDocument.Parse(details);
        return document.RootElement.Clone();
    }
}

/// <summary>Mirrors <c>audit_logs</c> (web/src/api/approvals.ts AuditLogEntry), plus the actor's email.
/// <c>userId</c> is null when the acting account was deleted (the FK is ON DELETE SET NULL).</summary>
public sealed class AuditLogResponse
{
    public required Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public Guid? CorrelationId { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
    public JsonElement? Details { get; init; }
    public string? IpAddress { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
