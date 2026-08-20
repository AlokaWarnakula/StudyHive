using StudyHive.Api.Data.Entities;

namespace StudyHive.Api.Common;

/// <summary>
/// Development-only demo accounts, bound from the "DevSeed" config section. Only ever read when
/// the host environment is Development — see Data/DevDataSeeder.cs and Program.cs. Never present
/// in appsettings.json (the non-Development base config).
/// </summary>
public sealed class DevSeedOptions
{
    public const string SectionName = "DevSeed";

    public List<DevSeedUser> Users { get; init; } = [];
}

public sealed class DevSeedUser
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string FullName { get; init; }
    public required UserRole Role { get; init; }
}
