using Bokmal.Database.Engines;

namespace Bokmal.Tests.Databases;

/// <summary>
/// Conjures a disposable, isolated database for one test class to scribble in.
///
/// This is the test-side twin of <see cref="IDatabaseEngine"/>. Production only needs to
/// know how to connect and migrate; tests additionally need to know how to bring a
/// database into existence and tear it down, and that part differs wildly per engine --
/// a temp file for SQLite, a container or a template database for a server engine. Each
/// engine gets one implementation here and nothing engine-specific leaks into the tests
/// themselves.
/// </summary>
public interface ITestDatabaseProvisioner
{
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Returns a database that is empty apart from the schema the migration scripts build.
    /// Disposing it destroys the database.
    /// </summary>
    TestDatabase Provision();
}
