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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", database.Engine.Provider.ToString());
        builder.UseSetting("Database:ConnectionString", database.ConnectionString);
        builder.UseSetting("Database:SeedDemoData", "false");
    }
}
