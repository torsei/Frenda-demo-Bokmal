using Bokmal.Database.Entities;
using Bokmal.Tests.Databases;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Tests;

/// <summary>
/// The demo library is what the top list, the recommendations and the reading-time estimate
/// are computed from, and it is invented rather than authored. These tests assert the
/// properties the rest of the application is entitled to assume about it.
/// </summary>
public class DemoDataTests : IAsyncLifetime
{
    private readonly TestDatabase _db = TestDatabases.Create();

    public Task InitializeAsync() => _db.SeedDemoDataAsync();

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Every_seeded_row_is_reachable_by_its_own_id()
    {
        // Seeding through the model is what makes this hold. Written as SQL literals it did
        // not: EF writes GUIDs uppercase and SQLite compares text case-sensitively, so a
        // lowercase literal produced rows that existed but that no lookup by id could find
        // -- listing worked, every detail page returned 404. The test stays as a regression
        // guard in case anyone reaches for raw SQL again.
        using var context = _db.CreateContext();

        var bookIds = await context.Books.Select(b => b.Id).ToListAsync();
        var borrowerIds = await context.Borrowers.Select(b => b.Id).ToListAsync();
        var copyIds = await context.BookCopies.Select(c => c.Id).ToListAsync();

        Assert.NotEmpty(bookIds);

        Assert.Equal(bookIds.Count, await context.Books.CountAsync(b => bookIds.Contains(b.Id)));
        Assert.Equal(borrowerIds.Count, await context.Borrowers.CountAsync(b => borrowerIds.Contains(b.Id)));
        Assert.Equal(copyIds.Count, await context.BookCopies.CountAsync(c => copyIds.Contains(c.Id)));
    }

    [Fact]
    public async Task Copy_status_agrees_with_the_loans_that_are_still_open()
    {
        // Availability is read from book_copy.status, but the truth about who has what is
        // in the loan table. If the seed disagreed with itself the app would start in a
        // state it could never have reached by borrowing.
        using var context = _db.CreateContext();

        var copiesOnLoan = await context.BookCopies
            .Where(c => c.Status == CopyStatuses.OnLoan)
            .Select(c => c.Id)
            .ToListAsync();

        var copiesWithActiveLoan = await context.Loans
            .Where(l => l.ReturnedAt == null)
            .Select(l => l.BookCopyId)
            .ToListAsync();

        Assert.Equal(copiesOnLoan.Order(), copiesWithActiveLoan.Order());
    }

    [Fact]
    public async Task No_copy_has_more_than_one_open_loan()
    {
        using var context = _db.CreateContext();

        var duplicated = await context.Loans
            .Where(l => l.ReturnedAt == null)
            .GroupBy(l => l.BookCopyId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        Assert.Empty(duplicated);
    }

    [Fact]
    public async Task Loans_on_the_same_copy_never_overlap()
    {
        using var context = _db.CreateContext();

        var byCopy = (await context.Loans
                .Select(l => new { l.BookCopyId, l.BorrowedAt, l.ReturnedAt })
                .ToListAsync())
            .GroupBy(l => l.BookCopyId);

        foreach (var copy in byCopy)
        {
            var ordered = copy.OrderBy(l => l.BorrowedAt).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                Assert.NotNull(previous.ReturnedAt);
                Assert.True(
                    previous.ReturnedAt <= ordered[i].BorrowedAt,
                    $"Copy {copy.Key} was lent out again before the previous loan came back.");
            }
        }
    }

    [Fact]
    public async Task Seeded_history_obeys_the_same_rules_the_api_enforces()
    {
        using var context = _db.CreateContext();

        var active = await context.Loans
            .Where(l => l.ReturnedAt == null)
            .Select(l => new { l.BorrowerId, l.BookCopy.BookId })
            .ToListAsync();

        foreach (var borrower in active.GroupBy(l => l.BorrowerId))
        {
            Assert.True(borrower.Count() <= LoanPolicy.MaxActiveLoansPerBorrower);
            Assert.Equal(borrower.Select(l => l.BookId).Distinct().Count(), borrower.Count());
        }
    }

    [Fact]
    public async Task There_is_enough_history_for_the_discovery_features_to_say_something()
    {
        using var context = _db.CreateContext();

        // A top list needs a clear winner, recommendations need borrowers who read more
        // than one book, and the reading-time estimate needs returned loans to measure.
        Assert.True(await context.Loans.CountAsync(l => l.ReturnedAt != null) > 200);
        Assert.True(await context.BookCopies.CountAsync(c => c.Status == CopyStatuses.OnLoan) > 5);
        Assert.True(await context.BookCopies.CountAsync(c => c.Status == CopyStatuses.Available) > 20);

        var loansPerBook = await context.Loans
            .GroupBy(l => l.BookCopy.BookId)
            .Select(g => g.Count())
            .OrderByDescending(count => count)
            .ToListAsync();

        Assert.Equal(await context.Books.CountAsync(), loansPerBook.Count);
        Assert.True(loansPerBook[0] > loansPerBook[^1] * 2, "The top list would be a coin toss.");
    }
}
