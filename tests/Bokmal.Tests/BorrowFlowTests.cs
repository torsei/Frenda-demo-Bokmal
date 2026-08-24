using Bokmal.Api.Services;
using Bokmal.Database;
using Bokmal.Database.Entities;
using Bokmal.Tests.Databases;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Tests;

/// <summary>
/// The borrow flow. Every test here corresponds to one rule the library has, so a failure
/// says which rule broke rather than that something went wrong.
/// </summary>
public class BorrowFlowTests
{
    private const string Astrid = "astrid@example.se";
    private const string Bjorn = "bjorn@example.se";

    [Fact]
    public async Task Borrowing_takes_a_copy_off_the_shelf()
    {
        using var library = await Library.WithAsync(b => b.Book("dune", copies: 2).Borrower(Astrid));
        var astrid = await library.BorrowerIdAsync(Astrid);

        await using var context = library.CreateContext();
        var result = await library.CreateLoanService(context).BorrowAsync(astrid, await library.BookIdAsync("dune"), default);

        Assert.Equal(BorrowOutcome.Borrowed, result.Outcome);
        Assert.Equal(1, await library.AvailableCopiesAsync("dune"));
        Assert.Equal(CopyStatuses.OnLoan, await library.CopyStatusAsync("dune", 1));
    }

    [Fact]
    public async Task A_loan_is_due_back_after_the_loan_period()
    {
        using var library = await Library.WithAsync(b => b.Book("dune", copies: 1).Borrower(Astrid));
        var astrid = await library.BorrowerIdAsync(Astrid);

        await using var context = library.CreateContext();
        var result = await library.CreateLoanService(context).BorrowAsync(astrid, await library.BookIdAsync("dune"), default);

        var loan = result.Loan!;
        Assert.Equal(library.Clock.GetUtcNow().UtcDateTime, loan.BorrowedAt);
        Assert.Equal(loan.BorrowedAt.AddDays(LoanPolicy.LoanPeriodDays), loan.DueAt);
        Assert.Null(loan.ReturnedAt);
    }

    [Fact]
    public async Task Borrowing_a_book_with_no_free_copies_is_refused()
    {
        using var library = await Library.WithAsync(b => b.Book("dune", copies: 1).Borrower(Astrid).Borrower(Bjorn));
        var astrid = await library.BorrowerIdAsync(Astrid);
        var bjorn = await library.BorrowerIdAsync(Bjorn);

        await using var first = library.CreateContext();
        await library.CreateLoanService(first).BorrowAsync(astrid, await library.BookIdAsync("dune"), default);

        await using var second = library.CreateContext();
        var result = await library.CreateLoanService(second).BorrowAsync(bjorn, await library.BookIdAsync("dune"), default);

        Assert.Equal(BorrowOutcome.NoCopyAvailable, result.Outcome);
    }

    [Fact]
    public async Task A_borrower_cannot_hold_two_copies_of_the_same_book()
    {
        using var library = await Library.WithAsync(b => b.Book("dune", copies: 3).Borrower(Astrid));
        var astrid = await library.BorrowerIdAsync(Astrid);

        await using var first = library.CreateContext();
        await library.CreateLoanService(first).BorrowAsync(astrid, await library.BookIdAsync("dune"), default);

        await using var second = library.CreateContext();
        var result = await library.CreateLoanService(second).BorrowAsync(astrid, await library.BookIdAsync("dune"), default);

        Assert.Equal(BorrowOutcome.AlreadyBorrowed, result.Outcome);

        // The refusal must not have quietly taken a copy off the shelf on the way out.
        Assert.Equal(2, await library.AvailableCopiesAsync("dune"));
    }

    [Fact]
    public async Task A_borrower_is_held_to_the_loan_limit()
    {
        using var library = await Library.WithAsync(builder =>
        {
            builder.Borrower(Astrid);
            for (var i = 1; i <= LoanPolicy.MaxActiveLoansPerBorrower + 1; i++)
                builder.Book($"book-{i}", copies: 1);
        });

        var astrid = await library.BorrowerIdAsync(Astrid);

        for (var i = 1; i <= LoanPolicy.MaxActiveLoansPerBorrower; i++)
        {
            await using var context = library.CreateContext();
            var allowed = await library.CreateLoanService(context).BorrowAsync(astrid, await library.BookIdAsync($"book-{i}"), default);
            Assert.Equal(BorrowOutcome.Borrowed, allowed.Outcome);
        }

        await using var last = library.CreateContext();
        var refused = await library.CreateLoanService(last)
            .BorrowAsync(astrid, await library.BookIdAsync($"book-{LoanPolicy.MaxActiveLoansPerBorrower + 1}"), default);

        Assert.Equal(BorrowOutcome.TooManyActiveLoans, refused.Outcome);
    }

    [Fact]
    public async Task Borrowing_a_book_the_library_does_not_have_is_refused()
    {
        using var library = await Library.WithAsync(b => b.Book("dune", copies: 1).Borrower(Astrid));
        var astrid = await library.BorrowerIdAsync(Astrid);

        await using var context = library.CreateContext();
        var result = await library.CreateLoanService(context).BorrowAsync(astrid, BokmalId.New(), default);

        Assert.Equal(BorrowOutcome.BookNotFound, result.Outcome);
    }

    [Fact]
    public async Task An_impossible_state_is_reported_rather_than_dressed_up_as_a_conflict()
    {
        // Manufactures the one thing the borrow flow refuses to paper over: a copy marked
        // available while a loan on it is still open. Only a bug elsewhere could produce it,
        // so the flow must not answer "try again later" -- the borrower would retry forever
        // against an inconsistency that never clears, and nothing would ever be logged.
        using var library = await Library.WithAsync(b => b.Book("dune", copies: 1).Borrower(Astrid).Borrower(Bjorn));

        var astrid = await library.BorrowerIdAsync(Astrid);
        var bjorn = await library.BorrowerIdAsync(Bjorn);
        var dune = await library.BookIdAsync("dune");

        await using (var setup = library.CreateContext())
        {
            var copyId = await setup.BookCopies.Where(c => c.BookId == dune).Select(c => c.Id).SingleAsync();

            setup.Loans.Add(new Loan
            {
                Id = BokmalId.New(),
                BookCopyId = copyId,
                BorrowerId = bjorn,
                BorrowedAt = library.Clock.GetUtcNow().UtcDateTime,
                DueAt = library.Clock.GetUtcNow().UtcDateTime.AddDays(LoanPolicy.LoanPeriodDays),
                ReturnedAt = null
            });

            // The copy stays Available, which is the corruption. The compare-and-swap will
            // happily claim it and the unique index will then refuse the second open loan.
            await setup.SaveChangesAsync();
        }

        await using var context = library.CreateContext();
        var service = library.CreateLoanService(context);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => service.BorrowAsync(astrid, dune, default));
    }

    /// <summary>
    /// Eight requests arrive at once for a book with one copy left. Exactly one may win.
    ///
    /// Be careful about what this proves. It asserts the property that matters to a
    /// borrower -- the same physical copy is never handed to two people -- and it would
    /// catch a whole class of mistakes. It does **not** prove that the conditional update is
    /// what prevents the double lending.
    ///
    /// That was measured rather than assumed: replacing the conditional update with a plain
    /// unconditional one -- the naive "I already checked it was free" version -- leaves this
    /// test green. SQLite allows a single writer at a time, so the second request blocks at
    /// BEGIN IMMEDIATE until the first commits and then reads a fresh, correct view of the
    /// shelf. The race the compare-and-swap defends against cannot be produced on SQLite at
    /// all, and the defence is therefore never exercised here.
    ///
    /// On an engine that does not serialise writers the naive version double-lends. Running
    /// this suite against such an engine is the only thing that can verify the mechanism;
    /// see the provider note in the README.
    /// </summary>
    [Fact]
    public async Task Only_one_of_many_simultaneous_requests_gets_the_last_copy()
    {
        const int contenders = 8;

        using var library = await Library.WithAsync(builder =>
        {
            builder.Book("dune", copies: 1);
            for (var i = 0; i < contenders; i++) builder.Borrower($"reader-{i}@example.se");
        });

        var borrowerIds = new List<Guid>();
        for (var i = 0; i < contenders; i++)
            borrowerIds.Add(await library.BorrowerIdAsync($"reader-{i}@example.se"));

        var dune = await library.BookIdAsync("dune");

        var attempts = borrowerIds.Select(async borrowerId =>
        {
            await using var context = library.CreateContext();
            return await library.CreateLoanService(context).BorrowAsync(borrowerId, dune, default);
        });

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r.Outcome == BorrowOutcome.Borrowed));
        Assert.Equal(contenders - 1, results.Count(r => r.Outcome == BorrowOutcome.NoCopyAvailable));
        Assert.Equal(0, await library.AvailableCopiesAsync("dune"));

        await using var check = library.CreateContext();
        Assert.Equal(1, await check.Loans.CountAsync(l => l.ReturnedAt == null));
    }

    [Fact]
    public async Task Simultaneous_requests_share_out_the_copies_that_exist()
    {
        // Three copies and six contenders: everyone who can be served is served, and the
        // losers are told the shelf is empty rather than being handed a duplicate.
        const int copies = 3;
        const int contenders = 6;

        using var library = await Library.WithAsync(builder =>
        {
            builder.Book("dune", copies);
            for (var i = 0; i < contenders; i++) builder.Borrower($"reader-{i}@example.se");
        });

        var borrowerIds = new List<Guid>();
        for (var i = 0; i < contenders; i++)
            borrowerIds.Add(await library.BorrowerIdAsync($"reader-{i}@example.se"));

        var dune = await library.BookIdAsync("dune");

        var results = await Task.WhenAll(borrowerIds.Select(async borrowerId =>
        {
            await using var context = library.CreateContext();
            return await library.CreateLoanService(context).BorrowAsync(borrowerId, dune, default);
        }));

        Assert.Equal(copies, results.Count(r => r.Outcome == BorrowOutcome.Borrowed));
        Assert.Equal(contenders - copies, results.Count(r => r.Outcome == BorrowOutcome.NoCopyAvailable));

        await using var check = library.CreateContext();
        var lentCopies = await check.Loans.Where(l => l.ReturnedAt == null)
            .Select(l => l.BookCopyId).ToListAsync();

        Assert.Equal(copies, lentCopies.Distinct().Count());
    }
}
