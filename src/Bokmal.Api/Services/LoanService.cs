using Bokmal.Database;
using Bokmal.Database.Engines;
using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Api.Services;

public enum BorrowOutcome
{
    Borrowed,
    BookNotFound,
    NoCopyAvailable,
    AlreadyBorrowed,
    TooManyActiveLoans
}

public enum ReturnOutcome
{
    Returned,
    LoanNotFound,
    NotYourLoan,
    AlreadyReturned
}

public sealed record BorrowResult(BorrowOutcome Outcome, Loan? Loan = null);

/// <summary>A loan together with what it is a loan of.</summary>
public sealed record LoanView(Loan Loan, Book Book, int CopyNumber);

public sealed record ReturnResult(ReturnOutcome Outcome, Loan? Loan = null);

/// <summary>
/// Borrowing and returning.
/// </summary>
public sealed class LoanService(
    BokmalDbContext context,
    IDatabaseEngine engine,
    TimeProvider timeProvider,
    ILogger<LoanService> logger)
{
    /// <summary>
    /// Everything this borrower has out or has had out, newest first. Current and past
    /// loans come back in one list rather than two queries; which is which is a matter of
    /// whether the loan has been returned, and the caller can say that better than a
    /// parameter can.
    /// </summary>
    public async Task<IReadOnlyList<LoanView>> GetLoansForAsync(Guid borrowerId, CancellationToken cancellationToken)
    {
        return await context.Loans
            .Where(l => l.BorrowerId == borrowerId)
            .OrderByDescending(l => l.BorrowedAt)
            .Select(l => new LoanView(l, l.BookCopy.Book, l.BookCopy.CopyNumber))
            .ToListAsync(cancellationToken);
    }

    public Task<LoanView?> GetLoanViewAsync(Guid loanId, CancellationToken cancellationToken)
        => context.Loans
            .Where(l => l.Id == loanId)
            .Select(l => new LoanView(l, l.BookCopy.Book, l.BookCopy.CopyNumber))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lends out a copy of a book.
    ///
    /// The interesting part is what stops two people being handed the same physical copy.
    /// Reading "is a copy free" and then writing "it is mine now" is a check-then-act race:
    /// under the isolation level everything defaults to, both requests read the same free
    /// copy before either writes, and both proceed. A transaction alone does not fix that
    /// -- it makes the write atomic, not the decision exclusive.
    ///
    /// So the decision is not trusted. The claim is a conditional update that repeats the
    /// condition in its WHERE clause and reports how many rows it actually changed. Zero
    /// rows means somebody else got there first, and we simply try the next copy. That
    /// works identically on every engine, unlike row locks, which are spelled differently
    /// everywhere and do not exist at all in SQLite.
    ///
    /// There is a unique index behind all this that permits only one unreturned loan per
    /// copy. This method does not rely on it and never catches its violation: it is a
    /// backstop against a bug in some other code path, and if it ever fires that is what it
    /// should look like.
    /// </summary>
    public async Task<BorrowResult> BorrowAsync(Guid borrowerId, string bookSlug, CancellationToken cancellationToken)
    {
        await using var transaction = await engine.BeginWriteTransactionAsync(context, cancellationToken);

        var book = await context.Books
            .SingleOrDefaultAsync(b => b.Slug == bookSlug, cancellationToken);

        if (book is null)
            return new BorrowResult(BorrowOutcome.BookNotFound);

        // These checks exist to produce a helpful answer, not to keep the data correct.
        // Correctness is the conditional update below.
        var activeBookIds = await context.Loans
            .Where(l => l.BorrowerId == borrowerId && l.ReturnedAt == null)
            .Select(l => l.BookCopy.BookId)
            .ToListAsync(cancellationToken);

        if (activeBookIds.Contains(book.Id))
            return new BorrowResult(BorrowOutcome.AlreadyBorrowed);

        if (activeBookIds.Count >= LoanPolicy.MaxActiveLoansPerBorrower)
            return new BorrowResult(BorrowOutcome.TooManyActiveLoans);

        var candidateCopyIds = await context.BookCopies
            .Where(c => c.BookId == book.Id && c.Status == CopyStatuses.Available)
            .OrderBy(c => c.CopyNumber)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        foreach (var copyId in candidateCopyIds)
        {
            // Compare-and-swap. The WHERE re-states the condition the decision rested on,
            // so the write can only land if that condition still holds.
            var claimed = await context.BookCopies
                .Where(c => c.Id == copyId && c.Status == CopyStatuses.Available)
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(c => c.Status, CopyStatuses.OnLoan),
                    cancellationToken);

            if (claimed == 0)
            {
                // Lost the race for this copy. The shelf may still have another one.
                logger.LogInformation("Copy {CopyId} was claimed by another request; trying the next", copyId);
                continue;
            }

            var borrowedAt = timeProvider.GetUtcNow().UtcDateTime;
            var loan = new Loan
            {
                Id = BokmalId.New(),
                BookCopyId = copyId,
                BorrowerId = borrowerId,
                BorrowedAt = borrowedAt,
                DueAt = borrowedAt.AddDays(LoanPolicy.LoanPeriodDays),
                ReturnedAt = null
            };

            context.Loans.Add(loan);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new BorrowResult(BorrowOutcome.Borrowed, loan);
        }

        return new BorrowResult(BorrowOutcome.NoCopyAvailable);
    }

    /// <summary>
    /// Takes a loan back and puts the copy on the shelf.
    ///
    /// Same shape as borrowing, mirrored: the copy is released with a conditional update so
    /// that returning twice cannot free a copy somebody else has since borrowed.
    /// </summary>
    public async Task<ReturnResult> ReturnAsync(Guid borrowerId, Guid loanId, CancellationToken cancellationToken)
    {
        await using var transaction = await engine.BeginWriteTransactionAsync(context, cancellationToken);

        var loan = await context.Loans
            .SingleOrDefaultAsync(l => l.Id == loanId, cancellationToken);

        if (loan is null)
            return new ReturnResult(ReturnOutcome.LoanNotFound);

        // Deliberately distinct from "not found": borrowers should not be able to probe
        // for other people's loan ids, but the API should not lie to a developer either.
        // The controller collapses both to a 404 for callers.
        if (loan.BorrowerId != borrowerId)
            return new ReturnResult(ReturnOutcome.NotYourLoan);

        if (loan.ReturnedAt is not null)
            return new ReturnResult(ReturnOutcome.AlreadyReturned);

        loan.ReturnedAt = timeProvider.GetUtcNow().UtcDateTime;

        var released = await context.BookCopies
            .Where(c => c.Id == loan.BookCopyId && c.Status == CopyStatuses.OnLoan)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(c => c.Status, CopyStatuses.Available),
                cancellationToken);

        if (released == 0)
        {
            // The loan was open but the copy was not on loan. The two disagree, which
            // should be impossible, so fail rather than paper over it.
            throw new InvalidOperationException(
                $"Loan {loanId} was open but copy {loan.BookCopyId} was not marked as on loan.");
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReturnResult(ReturnOutcome.Returned, loan);
    }
}
