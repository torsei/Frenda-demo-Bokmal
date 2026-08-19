using Bokmal.Database;
using Bokmal.Database.Config;
using Bokmal.Database.Engines;
using Bokmal.Database.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bokmal.Api.Startup;

public static class DatabaseStartup
{
    public static IServiceCollection AddBokmalDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The engine is resolved once from configuration. Everything downstream depends on
        // the interface, so this registration is the only line that knows which database
        // is in play.
        services.AddSingleton<IDatabaseEngine>(provider =>
            DatabaseEngines.For(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Provider));

        services.AddDbContext<BokmalDbContext>((provider, builder) =>
        {
            var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            provider.GetRequiredService<IDatabaseEngine>().Configure(builder, options.ConnectionString);
        });

        return services;
    }

    /// <summary>
    /// Brings the database up to date and, where appropriate, fills it with the demo
    /// library. Runs before the first request is served: a half-migrated database serving
    /// traffic is worse than a slow start.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app, bool seedDemoData = true)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var engine = app.Services.GetRequiredService<IDatabaseEngine>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bokmal.Startup");

        DatabaseUpgrader.Upgrade(engine, options.ConnectionString, logger);

        var decision = seedDemoData
            ? DemoDataPolicy.Decide(options.SeedDemoData, app.Environment.EnvironmentName)
            : new SeedDecision(false, "migration-only run");

        if (!decision.ShouldSeed)
        {
            logger.LogInformation("Not seeding demo data: {Reason}", decision.Reason);
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var seeder = new DemoDataSeeder(
            scope.ServiceProvider.GetRequiredService<BokmalDbContext>(),
            scope.ServiceProvider.GetRequiredService<TimeProvider>(),
            scope.ServiceProvider.GetRequiredService<ILogger<DemoDataSeeder>>());

        await seeder.SeedIfEmptyAsync();
    }
}
