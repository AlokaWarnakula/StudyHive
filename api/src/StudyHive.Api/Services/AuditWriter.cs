using System.Text.Json;
using StudyHive.Api.Data;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;

namespace StudyHive.Api.Services;

/// <summary>
/// Adds one audit_logs row for a staff write (AUDIT CW-04). It only adds the row to the DbContext:
/// the caller's own SaveChanges commits it together with the change it describes, so a change and
/// its audit row always commit or roll back together.
/// </summary>
public interface IAuditWriter
{
    void Write(string action, string entityType, Guid entityId, object? details = null);
}

public sealed class AuditWriter(StudyHiveDbContext db, IHttpContextAccessor httpContextAccessor) : IAuditWriter
{
    private static readonly JsonSerializerOptions DetailsJson = new(JsonSerializerDefaults.Web);

    public void Write(string action, string entityType, Guid entityId, object? details = null)
    {
        var http = httpContextAccessor.HttpContext;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = http?.User.TryGetUserId(out var userId) == true ? userId : null,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details, DetailsJson),
            IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
