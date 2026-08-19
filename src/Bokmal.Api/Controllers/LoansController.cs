using Bokmal.Api.Contracts;
using Bokmal.Api.Identity;
using Bokmal.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bokmal.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Produces("application/json")]
[RequireBorrower]
public sealed class LoansController(
    LoanService loans,
    ICurrentBorrower currentBorrower,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Everything the signed-in borrower has out now, and everything they have had.</summary>
    [HttpGet("me")]
    [ProducesResponseType<MyLoansDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MyLoansDto>> Mine(CancellationToken cancellationToken)
    {
        var borrower = await currentBorrower.RequireAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var all = (await loans.GetLoansForAsync(borrower.Id, cancellationToken))
            .Select(view => view.ToDto(now))
            .ToList();

        return Ok(new MyLoansDto(
            Current: all.Where(l => l.ReturnedAt is null).ToList(),
            Past: all.Where(l => l.ReturnedAt is not null).ToList()));
    }

    /// <summary>
    /// Borrow an available copy.
    ///
    /// Every reason this can fail is a 409 rather than a 400: the request was perfectly
    /// well formed, the library just was not in a state where it could be honoured. The
    /// distinction matters to the caller, because a 409 is worth retrying later and a 400
    /// never is.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<LoanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoanDto>> Borrow(
        [FromBody] BorrowRequest request,
        CancellationToken cancellationToken)
    {
        var borrower = await currentBorrower.RequireAsync(cancellationToken);
        var result = await loans.BorrowAsync(borrower.Id, request.BookSlug, cancellationToken);

        switch (result.Outcome)
        {
            case BorrowOutcome.Borrowed:
                var view = await loans.GetLoanViewAsync(result.Loan!.Id, cancellationToken);
                var dto = view!.ToDto(timeProvider.GetUtcNow().UtcDateTime);
                return CreatedAtAction(nameof(Mine), dto);

            case BorrowOutcome.BookNotFound:
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "No such book",
                    detail: $"The catalogue has no book with the slug '{request.BookSlug}'.");

            case BorrowOutcome.NoCopyAvailable:
                return Conflict(
                    "All copies are out",
                    "Every copy of this book is currently on loan. Try again once one comes back.");

            case BorrowOutcome.AlreadyBorrowed:
                return Conflict(
                    "Already borrowed",
                    "You already have this book out. Return it before borrowing it again.");

            case BorrowOutcome.TooManyActiveLoans:
                return Conflict(
                    "Loan limit reached",
                    $"You can have {Database.Entities.LoanPolicy.MaxActiveLoansPerBorrower} books out at a time. " +
                    "Return something before borrowing more.");

            default:
                throw new InvalidOperationException($"Unhandled borrow outcome '{result.Outcome}'.");
        }
    }

    /// <summary>Give a book back.</summary>
    [HttpPost("{loanId:guid}/return")]
    [ProducesResponseType<LoanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoanDto>> Return(Guid loanId, CancellationToken cancellationToken)
    {
        var borrower = await currentBorrower.RequireAsync(cancellationToken);
        var result = await loans.ReturnAsync(borrower.Id, loanId, cancellationToken);

        switch (result.Outcome)
        {
            case ReturnOutcome.Returned:
                var view = await loans.GetLoanViewAsync(result.Loan!.Id, cancellationToken);
                return Ok(view!.ToDto(timeProvider.GetUtcNow().UtcDateTime));

            // Someone else's loan is reported as missing rather than forbidden. Saying
            // "that is not yours" would confirm the id exists, which is a small thing to
            // leak but a free one to avoid.
            case ReturnOutcome.LoanNotFound:
            case ReturnOutcome.NotYourLoan:
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "No such loan",
                    detail: "You have no loan with that id.");

            case ReturnOutcome.AlreadyReturned:
                return Conflict(
                    "Already returned",
                    "This loan was closed earlier. Nothing more to do.");

            default:
                throw new InvalidOperationException($"Unhandled return outcome '{result.Outcome}'.");
        }
    }

    private ObjectResult Conflict(string title, string detail)
        => Problem(statusCode: StatusCodes.Status409Conflict, title: title, detail: detail);
}
