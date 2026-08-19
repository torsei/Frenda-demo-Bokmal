using Bokmal.Database;
using Bokmal.Database.Config;
using Bokmal.Database.Engines;
using Bokmal.Database.Engines.Sqlite;
using Microsoft.Data.Sqlite;

namespace Bokmal.Tests.Databases;

/// <summary>
/// Provisions SQLite by dropping a uniquely named file in the temp directory.
///
/// Deliberately a file rather than an in-memory database: in-memory SQLite lives and dies
/// with a single connection, and the borrow-flow tests open several to exercise real
/// concurrent requests.
/// </summary>
public sealed class SqliteTestDatabaseProvisioner : ITestDatabaseProvisioner
{
    public DatabaseProvider Provider => DatabaseProvider.Sqlite;

    public TestDatabase Provision()
    {
        var engine = new SqliteDatabaseEngine();
        var path = Path.Combine(Path.GetTempPath(), $"bokmal-test-{BokmalId.New():N}.db");
        var connectionString = $"Data Source={path}";

        DatabaseUpgrader.Upgrade(engine, connectionString);

        return new TestDatabase(engine, connectionString, () =>
        {
            // Pooled connections keep a handle on the file and Windows will not delete it
            // while one is open.
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        });
    }
}
