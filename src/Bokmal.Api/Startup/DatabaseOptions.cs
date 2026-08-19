using System.ComponentModel.DataAnnotations;
using Bokmal.Database.Engines;

namespace Bokmal.Api.Startup;

/// <summary>
/// Bound from the "Database" configuration section.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Which engine to run against. Also selects the Scripts/ folder the migrations are
    /// read from, so the two can never point at different databases.
    /// </summary>
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.Sqlite;

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Whether to fill an empty database with the demo library. Off unless asked for, and
    /// refused outright outside a non-production environment regardless of this flag --
    /// see DemoDataPolicy.
    /// </summary>
    public bool SeedDemoData { get; init; }
}
