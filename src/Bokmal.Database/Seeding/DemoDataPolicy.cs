namespace Bokmal.Database.Seeding;

public readonly record struct SeedDecision(bool ShouldSeed, string Reason);

/// <summary>
/// Decides whether inventing a library full of imaginary borrowers is appropriate.
///
/// Two independent gates, because they fail in different ways. The configuration flag is
/// the normal control: demo data is opt-in, off unless a settings file asks for it. The
/// environment check is the backstop for the case that actually matters -- someone copies
/// a development appsettings file to a production host, or a deployment inherits the wrong
/// configuration. Config can be wrong; the environment name is the thing least likely to
/// be wrong at the same time.
///
/// Bokmal will never be deployed anywhere. The guard is here because a seeder that can
/// write hundreds of fake rows is exactly the kind of code that should not rely on nobody
/// making a mistake.
/// </summary>
public static class DemoDataPolicy
{
    public const string ProductionEnvironmentName = "Production";

    public static SeedDecision Decide(bool enabledInConfiguration, string environmentName)
    {
        if (string.Equals(environmentName, ProductionEnvironmentName, StringComparison.OrdinalIgnoreCase))
            return new SeedDecision(false,
                $"environment is {ProductionEnvironmentName}; demo data is never seeded there, " +
                "regardless of configuration");

        if (!enabledInConfiguration)
            return new SeedDecision(false, "Database:SeedDemoData is not enabled");

        return new SeedDecision(true, $"enabled in configuration and environment is {environmentName}");
    }
}
