using Microsoft.AspNetCore.Authorization;
using StudyHive.Api.Common;

namespace StudyHive.Api.Security;

/// <summary>
/// Implemented by a small per-endpoint wrapper around whatever resource a feature is checking
/// ownership of (e.g. a student's own booking request). Reused across S1-S4 so every "Student
/// (own), Librarian" style rule in the API table (DOCS §06-09) shares one policy instead of each
/// component hand-rolling its own owner check.
/// </summary>
public interface IOwnedByUser
{
    Guid OwnerUserId { get; }
}

public sealed class ResourceOwnerRequirement : IAuthorizationRequirement;

/// <summary>Succeeds when the caller owns the resource, or holds any staff role (Librarian/StoreOfficer/Admin).</summary>
public sealed class ResourceOwnerAuthorizationHandler : AuthorizationHandler<ResourceOwnerRequirement, IOwnedByUser>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ResourceOwnerRequirement requirement, IOwnedByUser resource)
    {
        if (context.User.IsInRole(Roles.Librarian) ||
            context.User.IsInRole(Roles.StoreOfficer) ||
            context.User.IsInRole(Roles.Admin))
        {
            context.Succeed(requirement);
        }
        else if (context.User.TryGetUserId(out var userId) && userId == resource.OwnerUserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
