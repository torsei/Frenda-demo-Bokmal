using Bokmal.Api.Startup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bokmal.Tests.Databases;

/// <summary>
/// Boots the real API in-process, pointed at a throwaway database.
///
/// Nothing is stubbed. The application starts the way it starts in production, including
/// running its migrations at startup -- which find nothing to do, since the test database was
/// built from the same scripts.
///
/// Demo data is switched off. These tests build the three books they need and say so; a
/// library of 24 titles and 469 loans would make every assertion a puzzle.
/// </summary>
public sealed class BokmalApi(TestDatabase database) : WebApplicationFactory<Program>
{
    private static string Key(string property) => $"{DatabaseOptions.SectionName}:{property}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Built from nameof rather than spelled out, so renaming a property on
        // DatabaseOptions breaks this at compile time instead of silently leaving the tests
        // pointed at whatever appsettings.json happens to say.
        builder.UseSetting(Key(nameof(DatabaseOptions.Provider)), database.Engine.Provider.ToString());
        builder.UseSetting(Key(nameof(DatabaseOptions.ConnectionString)), database.ConnectionString);
        builder.UseSetting(Key(nameof(DatabaseOptions.SeedDemoData)), "false");
    }
}
