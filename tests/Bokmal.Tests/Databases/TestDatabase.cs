using Bokmal.Database;
using Bokmal.Database.Engines;
using Bokmal.Database.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Tests.Databases;

/// <summary>
/// A live throwaway database, already migrated. Engine-agnostic: everything specific to
/// how it was created and how it gets destroyed was supplied by the provisioner.
/// </summary>
public sealed class TestDatabase(IDatabaseEngine engine, string connectionString, Action cleanup) : IDisposable
{
    public IDatabaseEngine Engine { get; } = engine;

    public string ConnectionString { get; } = connectionString;

    public BokmalDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<BokmalDbContext>();
        Engine.Configure(builder, ConnectionString);
        return new BokmalDbContext(builder.Options);
    }

    /// <summary>
    /// Fills the database with the demo library. Opt-in: most tests want an empty database
    /// and a three-row fixture they can reason about, and should not pay for several
    /// hundred loans they never look at.
    /// </summary>
    public async Task SeedDemoDataAsync(TimeProvider? timeProvider = null)
    {
        await using var context = CreateContext();
        await new DemoDataSeeder(context, timeProvider ?? TimeProvider.System).SeedIfEmptyAsync();
    }

    public void Dispose() => cleanup();
}
