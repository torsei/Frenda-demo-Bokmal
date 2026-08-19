using System.Data;
using DbUp;
using DbUp.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bokmal.Database.Engines.Sqlite;

/// <summary>
/// SQLite support. Chosen as the default because it makes the solution runnable with
/// nothing installed -- clone, run, done -- which matters more for a reviewer than
/// anything a server engine would buy us here.
///
/// This class and the Scripts/Sqlite folder are the complete extent of Bokmal's knowledge
/// of SQLite. Note in particular that SQLite serialises writers at the database level,
/// which would hide a check-then-act race in the borrow flow. The borrow flow therefore
/// does not rely on it: it compare-and-swaps, which behaves identically on every engine.
/// </summary>
public sealed class SqliteDatabaseEngine : IDatabaseEngine
{
    public DatabaseProvider Provider => DatabaseProvider.Sqlite;

    public UpgradeEngineBuilder CreateUpgradeBuilder(string connectionString)
        => DeployChanges.To.SqliteDatabase(connectionString);

    public void Configure(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseSqlite(connectionString);

    /// <summary>
    /// SQLite transactions are DEFERRED by default: they take no lock until their first
    /// write. Two transactions can therefore both read, and then the second one's write is
    /// refused outright -- SQLITE_BUSY with no retry possible, because letting it wait
    /// would deadlock. Waiting longer does not help; the request simply fails.
    ///
    /// BEGIN IMMEDIATE takes the write lock up front, so the second request queues behind
    /// the first and then finds the shelf changed under it, which is exactly the situation
    /// the borrow flow is written to handle.
    /// </summary>
    public async Task<IDbContextTransaction> BeginWriteTransactionAsync(
        DbContext context,
        CancellationToken cancellationToken = default)
    {
        var connection = (SqliteConnection)context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        return await context.Database.UseTransactionAsync(transaction, cancellationToken)
            ?? throw new InvalidOperationException("Failed to enlist the SQLite transaction.");
    }
}
