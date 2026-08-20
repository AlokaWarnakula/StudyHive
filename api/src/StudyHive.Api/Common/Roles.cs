namespace StudyHive.Api.Common;

/// <summary>Role names used in JWT claims and [Authorize(Roles = ...)] policies. Keep in sync with web/mobile.</summary>
public static class Roles
{
    public const string Student = "Student";
    public const string Librarian = "Librarian";
    public const string StoreOfficer = "StoreOfficer";
    public const string Admin = "Admin";

    public static readonly string[] All = { Student, Librarian, StoreOfficer, Admin };
    public static readonly string[] Staff = { Librarian, StoreOfficer, Admin };
}
