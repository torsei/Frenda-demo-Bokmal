using DbUp.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bokmal.Database.Engines;

/// <summary>
/// Everything Bokmal needs to know about a specific database engine, and the only place
/// engine-specific types are allowed to appear.
///
/// <see cref="DatabaseProvider"/> answers "which database"; an engine answers "how do I
/// talk to it". Outside this folder no code references Microsoft.EntityFrameworkCore.Sqlite,
/// DbUp.Sqlite or any equivalent -- it goes through this interface, so supporting another
/// engine means adding one implementation and one Scripts/ folder and changing nothing else.
/// </summary>
public interface IDatabaseEngine
{
    /// <summary>
    /// Identifies the engine and doubles as the Scripts/ subfolder its migrations live in.
    /// </summary>
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Starts a DbUp upgrade for this engine. The caller supplies the scripts and logging.
    /// </summary>
    UpgradeEngineBuilder CreateUpgradeBuilder(string connectionString);

    /// <summary>
    /// Points an EF Core context at this engine.
    /// </summary>
    void Configure(DbContextOptionsBuilder builder, string connectionString);

    /// <summary>
    /// Opens a transaction for an operation that will read a row, decide something from it
    /// and then write -- the borrow flow, essentially.
    ///
    /// This exists because engines disagree about what a transaction does before its first
    /// write, and the disagreement is only visible under concurrency. Giving the engine a
    /// say keeps that knowledge in the adapter instead of leaking a database quirk into the
    /// domain, which would then be wrong on the next database.
    /// </summary>
    Task<IDbContextTransaction> BeginWriteTransactionAsync(
        DbContext context,
        CancellationToken cancellationToken = default);
}
