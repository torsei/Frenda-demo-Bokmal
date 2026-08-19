namespace Bokmal.Api.Services;

/// <summary>
/// How long a book tends to take, and how much that figure is worth.
/// <paramref name="BasedOnLoans"/> is exposed so the interface can be honest about a
/// number derived from four readers instead of presenting it like a fact.
/// </summary>
public sealed record ReadingTimeEstimate(int TypicalDays, int BasedOnLoans, bool FromHistory);

public static class ReadingTimeEstimator
{
    /// <summary>Below this, the sample says more about the individuals than the book.</summary>
    public const int MinimumLoansForHistory = 4;

    /// <summary>Fallback pace when a book has no history worth the name.</summary>
    public const double AssumedPagesPerDay = 35;

    /// <summary>
    /// Estimates from how long other borrowers actually kept the book.
    ///
    /// Two honest caveats, both worth knowing before trusting the number. It measures loan
    /// length, not reading time -- a book finished in a weekend and returned three weeks
    /// later counts as three weeks. And it uses the **median**, not the average: a handful
    /// of borrowers keep a book for months, and an average would let four of them drag the
    /// estimate for everybody else up by a week. The median ignores how far out the
    /// stragglers are, which is exactly the behaviour wanted here.
    /// </summary>
    public static ReadingTimeEstimate Estimate(IReadOnlyCollection<TimeSpan> completedLoans, int pageCount)
    {
        if (completedLoans.Count < MinimumLoansForHistory)
        {
            var assumed = Math.Max(1, (int)Math.Ceiling(pageCount / AssumedPagesPerDay));
            return new ReadingTimeEstimate(assumed, completedLoans.Count, FromHistory: false);
        }

        var days = completedLoans
            .Select(duration => duration.TotalDays)
            .OrderBy(days => days)
            .ToArray();

        var middle = days.Length / 2;
        var median = days.Length % 2 == 1
            ? days[middle]
            : (days[middle - 1] + days[middle]) / 2;

        return new ReadingTimeEstimate(Math.Max(1, (int)Math.Round(median)), days.Length, FromHistory: true);
    }
}
