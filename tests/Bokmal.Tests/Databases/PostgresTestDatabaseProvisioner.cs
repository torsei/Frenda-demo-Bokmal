using Bokmal.Database;
using Bokmal.Database.Config;
using Bokmal.Database.Engines;
using Bokmal.Database.Engines.Postgres;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bokmal.Tests.Databases;

/// <summary>
/// Provisions PostgreSQL by starting a container and cutting a fresh database out of it for
/// each test class.
///
/// One container for the whole run, not one per test: starting Postgres takes a second or
/// two and there are dozens of test classes. Isolation comes from each of them getting its
/// own database inside that container, which costs milliseconds.
///
/// Credentials are whatever Testcontainers generated. There is nothing to configure and
/// nothing to keep out of source control -- the container and its passwords exist for the
/// length of the test run.
/// </summary>
public sealed class PostgresTestDatabaseProvisioner : ITestDatabaseProvisioner
{
    private static readonly Lazy<PostgreSqlContainer> Container = new(() =>
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("bokmal_template")
            .Build();

        container.StartAsync().GetAwaiter().GetResult();

        return container;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public DatabaseProvider Provider => DatabaseProvider.Postgres;

    public TestDatabase Provision()
    {
        var engine = new PostgresDatabaseEngine();
        var admin = new NpgsqlConnectionStringBuilder(Container.Value.GetConnectionString());
        var databaseName = $"bokmal_test_{BokmalId.New():N}";

        Execute(admin.ConnectionString, $"CREATE DATABASE \"{databaseName}\"");

        var target = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName };
        DatabaseUpgrader.Upgrade(engine, target.ConnectionString);

        return new TestDatabase(engine, target.ConnectionString, () =>
        {
            // Connections are pooled per connection string, and PostgreSQL refuses to drop a
            // database anything is still attached to.
            NpgsqlConnection.ClearAllPools();
            Execute(admin.ConnectionString, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        });
    }

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
