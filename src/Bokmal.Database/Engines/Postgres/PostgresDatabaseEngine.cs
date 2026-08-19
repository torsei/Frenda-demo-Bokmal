using DbUp;
using DbUp.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bokmal.Database.Engines.Postgres;

/// <summary>
/// PostgreSQL support.
///
/// Bokmal runs on SQLite by default, so this exists for a specific reason: it is what makes
/// the portability claim checkable instead of merely stated. Running the test suite against
/// this engine exercises the borrow flow on a database that lets two writers proceed at
/// once, which is the situation the compare-and-swap is written for and the one SQLite
/// cannot produce.
///
/// Everything above <see cref="IDatabaseEngine"/> is untouched by adding it. This class and
/// Scripts/Postgres are the whole of it.
/// </summary>
public sealed class PostgresDatabaseEngine : IDatabaseEngine
{
    public DatabaseProvider Provider => DatabaseProvider.Postgres;

    public UpgradeEngineBuilder CreateUpgradeBuilder(string connectionString)
        => DeployChanges.To.PostgresqlDatabase(connectionString);

    public void Configure(DbContextOptionsBuilder builder, string connectionString)
        => builder.UseNpgsql(connectionString);

    /// <summary>
    /// A plain transaction is enough here.
    ///
    /// Unlike SQLite, PostgreSQL does not serialise writers, so two borrow requests really do
    /// run at the same time: both read a free copy, and both try to claim it. Nothing about
    /// opening the transaction prevents that -- the conditional update in the borrow flow is
    /// what decides who wins, and this is the engine on which that actually gets tested.
    /// </summary>
    public async Task<IDbContextTransaction> BeginWriteTransactionAsync(
        DbContext context,
        CancellationToken cancellationToken = default)
        => await context.Database.BeginTransactionAsync(cancellationToken);
}
