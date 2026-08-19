using Bokmal.Api.Services;
using Bokmal.Database;
using Bokmal.Database.Entities;
using Bokmal.Tests.Databases;

namespace Bokmal.Tests;

/// <summary>
/// Giving books back. The interesting cases are all the ways a return can be wrong, because
/// each of them either loses a copy or hands one to the wrong person.
/// </summary>
public class ReturnFlowTests
{
    private const string Astrid = "astrid@example.se";
    private const string Bjorn = "bjorn@example.se";

    private static async Task<(Library Library, Guid BorrowerId, Guid LoanId)> WithOneLoanOut()
    {
        var library = await Library.WithAsync(b => b.Book("dune", copies: 1).Borrower(Astrid).Borrower(Bjorn));
        var astrid = await library.BorrowerIdAsync(Astrid);

        await using var context = library.CreateContext();
        var borrowed = await library.CreateLoanService(context).BorrowAsync(astrid, "dune", default);

        return (library, astrid, borrowed.Loan!.Id);
    }

    [Fact]
    public async Task Returning_puts_the_copy_back_on_the_shelf()
    {
        var (library, astrid, loanId) = await WithOneLoanOut();
        using var _ = library;

        library.Clock.Advance(TimeSpan.FromDays(5));

        await using var context = library.CreateContext();
        var result = await library.CreateLoanService(context).ReturnAsync(astrid, loanId, default);

        Assert.Equal(ReturnOutcome.Returned, result.Outcome);
        Assert.Equal(library.Clock.GetUtcNow().UtcDateTime, result.Loan!.ReturnedAt);
        Assert.Equal(1, await library.AvailableCopiesAsync("dune"));
        Assert.Equal(CopyStatuses.Available, await library.CopyStatusAsync("dune", 1));
    }

    [Fact]
    public async Task A_returned_copy_can_be_borrowed_by_somebody_else()
    {
        var (library, astrid, loanId) = await WithOneLoanOut();
        using var _ = library;

        var bjorn = await library.BorrowerIdAsync(Bjorn);

        await using (var blocked = library.CreateContext())
        {
            var refused = await library.CreateLoanService(blocked).BorrowAsync(bjorn, "dune", default);
            Assert.Equal(BorrowOutcome.NoCopyAvailable, refused.Outcome);
        }

        await using (var returning = library.CreateContext())
            await library.CreateLoanService(returning).ReturnAsync(astrid, loanId, default);

        await using var borrowing = library.CreateContext();
        var allowed = await library.CreateLoanService(borrowing).BorrowAsync(bjorn, "dune", default);

        Assert.Equal(BorrowOutcome.Borrowed, allowed.Outcome);
    }

    [Fact]
    public async Task Returning_the_same_loan_twice_is_refused()
    {
        var (library, astrid, loanId) = await WithOneLoanOut();
        using var _ = library;

        await using (var first = library.CreateContext())
            await library.CreateLoanService(first).ReturnAsync(astrid, loanId, default);

        await using var second = library.CreateContext();
        var result = await library.CreateLoanService(second).ReturnAsync(astrid, loanId, default);

        Assert.Equal(ReturnOutcome.AlreadyReturned, result.Outcome);
    }

    [Fact]
    public async Task Returning_a_loan_twice_cannot_free_a_copy_somebody_else_now_holds()
    {
        // The reason the second return is refused rather than shrugged off. Astrid returns,
        // Bjorn borrows the freed copy, Astrid's client retries its return -- and if that
        // went through, Bjorn's copy would be marked available while he still has it.
        var (library, astrid, loanId) = await WithOneLoanOut();
        using var _ = library;

        var bjorn = await library.BorrowerIdAsync(Bjorn);

        await using (var returning = library.CreateContext())
            await library.CreateLoanService(returning).ReturnAsync(astrid, loanId, default);

        await using (var borrowing = library.CreateContext())
            await library.CreateLoanService(borrowing).BorrowAsync(bjorn, "dune", default);

        await using (var retry = library.CreateContext())
            await library.CreateLoanService(retry).ReturnAsync(astrid, loanId, default);

        Assert.Equal(0, await library.AvailableCopiesAsync("dune"));
        Assert.Equal(CopyStatuses.OnLoan, await library.CopyStatusAsync("dune", 1));
    }

    [Fact]
    public async Task Somebody_elses_loan_cannot_be_returned()
    {
        var (library, _, loanId) = await WithOneLoanOut();
        using var __ = library;

        var bjorn = await library.BorrowerIdAsync(Bjorn);

        await using var context = library.CreateContext();
        var result = await library.CreateLoanService(context).ReturnAsync(bjorn, loanId, default);

        Assert.Equal(ReturnOutcome.NotYourLoan, result.Outcome);
        Assert.Equal(0, await library.AvailableCopiesAsync("dune"));
    }

    [Fact]
    public async Task Returning_a_loan_that_does_not_exist_is_refused()
    {
        var (library, astrid, _) = await WithOneLoanOut();
        using var __ = library;

        await using var context = library.CreateContext();
        var result = await library.CreateLoanService(context).ReturnAsync(astrid, BokmalId.New(), default);

        Assert.Equal(ReturnOutcome.LoanNotFound, result.Outcome);
    }
}
