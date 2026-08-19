using Bokmal.Database.Engines.Postgres;
using Bokmal.Database.Engines.Sqlite;

namespace Bokmal.Database.Engines;

/// <summary>
/// The one switch statement in the solution that knows which engines exist. Adding an
/// engine is an entry here plus an <see cref="IDatabaseEngine"/> implementation.
/// </summary>
public static class DatabaseEngines
{
    public static IDatabaseEngine For(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => new SqliteDatabaseEngine(),
        DatabaseProvider.Postgres => new PostgresDatabaseEngine(),
        _ => throw new NotSupportedException(
            $"No database engine is registered for provider '{provider}'.")
    };
}
