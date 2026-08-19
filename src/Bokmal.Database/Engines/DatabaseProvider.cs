namespace Bokmal.Database.Engines;

/// <summary>
/// The database engines Bokmal knows how to run against. Bound from configuration
/// (Database:Provider) and used verbatim as the Scripts/ subfolder name, so the value and
/// the folder can never drift apart.
/// </summary>
public enum DatabaseProvider
{
    Sqlite,
    Postgres
}
