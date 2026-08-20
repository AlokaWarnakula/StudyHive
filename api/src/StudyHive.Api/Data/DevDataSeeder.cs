using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudyHive.Api.Common;
using StudyHive.Api.Data.Entities;
using StudyHive.Api.Security;

namespace StudyHive.Api.Data;

/// <summary>
/// Creates demo accounts (one per role) from configuration when running in Development, so a
/// fresh clone has a login for every role without anyone committing real credentials. Never runs
/// outside Development — see the guard at the call site in Program.cs.
/// </summary>
public static class DevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var options = services.GetRequiredService<IOptions<DevSeedOptions>>().Value;
        if (options.Users.Count == 0) return;

        var db = services.GetRequiredService<StudyHiveDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();

        foreach (var seedUser in options.Users)
        {
            var email = seedUser.Email.Trim();
            var exists = await db.Users.AnyAsync(u => u.Email == email, ct);
            if (exists) continue;

            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHasher.Hash(seedUser.Password),
                FullName = seedUser.FullName,
                Role = seedUser.Role,
                IsActive = true,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
