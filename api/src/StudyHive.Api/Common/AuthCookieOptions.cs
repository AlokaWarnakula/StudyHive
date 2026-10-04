using Microsoft.AspNetCore.Http;

namespace StudyHive.Api.Common;

/// <summary>
/// Bound from the "AuthCookie" config section. Settings for the web client's refresh-token cookie
/// (PLAN.md D1, AUDIT W-01): login/refresh/logout with <c>?client=web</c> keep the refresh token in
/// this httpOnly cookie instead of the response body, so the staff dashboard survives a reload
/// without ever putting a token in localStorage. Locally web and API share the site "localhost",
/// so <c>Lax</c> works; on Railway they are different sites, so production needs <c>None</c>
/// (which browsers only accept together with <c>Secure</c>).
/// </summary>
public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookie";

    public string Name { get; init; } = "studyhive_refresh";

    /// <summary>Lax, Strict or None.</summary>
    public SameSiteMode SameSite { get; init; } = SameSiteMode.Lax;

    /// <summary>Browsers treat http://localhost as secure, so this stays on even locally.</summary>
    public bool Secure { get; init; } = true;

    /// <summary>Only the auth endpoints ever receive the cookie.</summary>
    public string Path { get; init; } = "/api/auth";
}
