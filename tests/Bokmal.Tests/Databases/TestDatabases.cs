using Bokmal.Database.Engines;

namespace Bokmal.Tests.Databases;

/// <summary>
/// Entry point for tests that need a database.
///
/// The whole suite runs against whichever engine BOKMAL_TEST_PROVIDER names, defaulting to
/// SQLite so that <c>dotnet test</c> works on a clean machine with nothing installed. The
/// tests themselves contain no engine-specific code, so pointing them at another engine is
/// what actually verifies the portability claim rather than merely asserting it.
/// </summary>
public static class TestDatabases
{
    private const string ProviderVariable = "BOKMAL_TEST_PROVIDER";

    private static readonly ITestDatabaseProvisioner[] Provisioners =
    [
        new SqliteTestDatabaseProvisioner(),
        new PostgresTestDatabaseProvisioner()
    ];

    public static DatabaseProvider Provider
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(ProviderVariable);

            if (string.IsNullOrWhiteSpace(configured))
                return DatabaseProvider.Sqlite;

            if (!Enum.TryParse<DatabaseProvider>(configured, ignoreCase: true, out var provider))
                throw new InvalidOperationException(
                    $"{ProviderVariable} is set to '{configured}', which is not a known provider. " +
                    $"Known providers: {string.Join(", ", Enum.GetNames<DatabaseProvider>())}.");

            return provider;
        }
    }

    public static TestDatabase Create()
    {
        var provider = Provider;

        var provisioner = Provisioners.SingleOrDefault(p => p.Provider == provider)
            ?? throw new InvalidOperationException(
                $"No test database provisioner is registered for provider '{provider}'.");

        return provisioner.Provision();
    }
}
