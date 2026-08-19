using Bokmal.Database;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Api.Services;

/// <summary>
/// Works out how long books take to read, for whichever books are about to be shown.
///
/// Shared by the catalogue, the top list and the recommendations. Every one of those puts
/// a book on screen next to a "usually takes about a week" line, and the number has to be
/// the same in all three -- a card that says four days in the top list and eleven on the
/// detail page is worse than no estimate at all.
/// </summary>
public sealed class ReadingTimeService(BokmalDbContext context)
{
    public async Task<IReadOnlyDictionary<Guid, ReadingTimeEstimate>> EstimateAsync(
        IReadOnlyList<Guid> bookIds,
        CancellationToken cancellationToken)
    {
        if (bookIds.Count == 0)
            return new Dictionary<Guid, ReadingTimeEstimate>();

        // Loan durations come back as rows and the median is taken in memory. That is not
        // laziness: a median is not expressible in LINQ, and the SQL for one differs per
        // engine -- PERCENTILE_CONT exists in Postgres and SQL Server but not in SQLite,
        // and all three spell date arithmetic differently. Pushing it down would buy
        // nothing at this size and cost the portability the rest of the code is built for.
        //
        // This reads two columns for the books on screen, not the whole table. If the
        // library grew until that mattered, the answer would be a maintained statistic per
        // book rather than cleverer SQL.
        var completed = await context.Loans
            .Where(l => l.ReturnedAt != null && bookIds.Contains(l.BookCopy.BookId))
            .Select(l => new
            {
                l.BookCopy.BookId,
                l.BorrowedAt,
                l.ReturnedAt
            })
            .ToListAsync(cancellationToken);

        var pageCounts = await context.Books
            .Where(b => bookIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.PageCount, cancellationToken);

        var durationsByBook = completed
            .GroupBy(l => l.BookId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.ReturnedAt!.Value - l.BorrowedAt).ToList());

        return bookIds
            .Distinct()
            .ToDictionary(
                bookId => bookId,
                bookId => ReadingTimeEstimator.Estimate(
                    durationsByBook.GetValueOrDefault(bookId, []),
                    pageCounts.GetValueOrDefault(bookId)));
    }
}
