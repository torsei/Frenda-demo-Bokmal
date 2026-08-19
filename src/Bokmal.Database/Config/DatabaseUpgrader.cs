using System.Reflection;
using Bokmal.Database.Engines;
using DbUp.Engine.Output;
using Microsoft.Extensions.Logging;

namespace Bokmal.Database.Config;

/// <summary>
/// Applies the SQL migration scripts embedded in this assembly.
///
/// Scripts are organised per engine (Scripts/&lt;Provider&gt;/) because raw SQL is always
/// dialect-bound -- it is the one part of the solution that cannot be written once and
/// moved. Which dialect is in play is decided by the <see cref="IDatabaseEngine"/> handed
/// in; this class only knows that scripts exist and in what order to run them.
/// </summary>
public static class DatabaseUpgrader
{
    public static void Upgrade(IDatabaseEngine engine, string connectionString, ILogger? logger = null)
    {
        var upgrader = engine.CreateUpgradeBuilder(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.Contains($".Scripts.{engine.Provider}.", StringComparison.Ordinal))
            .LogTo(new MicrosoftUpgradeLog(logger))
            .WithTransactionPerScript()
            .Build();

        var pending = upgrader.GetScriptsToExecute();
        if (pending.Count == 0)
        {
            logger?.LogInformation("Database is up to date");
            return;
        }

        logger?.LogInformation("Database upgrade: {Count} pending script(s)", pending.Count);

        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException("Database upgrade failed.", result.Error);

        logger?.LogInformation("Database upgrade complete");
    }

    private sealed class MicrosoftUpgradeLog(ILogger? logger) : IUpgradeLog
    {
        public void LogTrace(string format, params object[] args) => logger?.LogTrace(format, args);
        public void LogDebug(string format, params object[] args) => logger?.LogDebug(format, args);
        public void LogInformation(string format, params object[] args) => logger?.LogInformation(format, args);
        public void LogWarning(string format, params object[] args) => logger?.LogWarning(format, args);
        public void LogError(string format, params object[] args) => logger?.LogError(format, args);
        public void LogError(Exception ex, string format, params object[] args) => logger?.LogError(ex, format, args);
    }
}
