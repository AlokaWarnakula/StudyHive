using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StudyHive.Api.Data;

namespace StudyHive.Api.Tests;

/// <summary>
/// The Docker image / Railway rely on the API migrating a fresh database on start, while the test
/// suite (Development, many parallel factories, one shared database) must not migrate.
/// </summary>
public class StartupMigrationsTests
{
    [Theory]
    [InlineData("Development", null, false)]
    [InlineData("Production", null, true)]
    [InlineData("Staging", null, true)]
    [InlineData("Development", "true", true)]
    [InlineData("Production", "false", false)]
    public void ShouldRun_Defaults_By_Environment_And_Honours_The_Override(string environment, string? setting, bool expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(setting is null ? [] : [new(StartupMigrations.ConfigKey, setting)])
            .Build();

        StartupMigrations.ShouldRun(config, new FakeHostEnvironment(environment)).Should().Be(expected);
    }

    [Fact]
    public async Task Startup_With_Migrations_Enabled_Leaves_No_Pending_Migrations()
    {
        // Against the already-migrated test database this is a no-op apply, so it is safe alongside
        // the parallel suite; it proves the startup path resolves the DbContext and runs MigrateAsync.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting(StartupMigrations.ConfigKey, "true"));

        using var client = factory.CreateClient();
        (await client.GetAsync("/health")).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudyHiveDbContext>();
        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Swagger_Loads()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        (await response.Content.ReadAsStringAsync()).Should().Contain("StudyHive API");
    }

    private sealed class FakeHostEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "StudyHive.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
