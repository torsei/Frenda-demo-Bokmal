using Bokmal.Api.Contracts;
using Bokmal.Api.Identity;
using Bokmal.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Api.Controllers;

/// <summary>
/// Signing in, for a very generous value of "in".
///
/// There is no password and no token. Naming a member's address is enough to act as them,
/// which is what the exercise asks for -- an app that knows who the current borrower is,
/// without a real login. It would be indefensible in anything real and is flagged as such
/// in the README rather than left to be found.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class SessionController(
    BokmalDbContext context,
    ICurrentBorrower currentBorrower) : ControllerBase
{
    /// <summary>
    /// Looks up a member by address. The client stores whatever comes back and sends it on
    /// subsequent requests; nothing is kept server-side, so there is no session to expire.
    /// </summary>
    [HttpPost("session")]
    [ProducesResponseType<BorrowerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BorrowerDto>> SignIn(
        [FromBody] SignInRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Address required",
                detail: "Give the email address you are a member under.");

        var borrower = await context.Borrowers
            .SingleOrDefaultAsync(b => b.Email == email, cancellationToken);

        if (borrower is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Not a member",
                detail: $"No library member is registered as '{email}'.");

        return Ok(borrower.ToDto());
    }

    /// <summary>Who the current request is acting as, if anyone.</summary>
    [HttpGet("session")]
    [RequireBorrower]
    [ProducesResponseType<BorrowerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BorrowerDto>> Current(CancellationToken cancellationToken)
    {
        var borrower = await currentBorrower.RequireAsync(cancellationToken);

        return Ok(borrower.ToDto());
    }

    /// <summary>
    /// Every member of the library.
    ///
    /// This exists purely so the sign-in screen can offer a list to pick from -- with no
    /// real accounts there is no other way to discover a valid address. It would not
    /// survive contact with a real system, where it is a directory of everyone who uses
    /// the library, and it goes the moment authentication becomes real.
    /// </summary>
    [HttpGet("borrowers")]
    [ProducesResponseType<IReadOnlyList<BorrowerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BorrowerDto>>> Borrowers(CancellationToken cancellationToken)
    {
        var borrowers = await context.Borrowers
            .OrderBy(b => b.DisplayName)
            .ToListAsync(cancellationToken);

        return Ok(borrowers.Select(b => b.ToDto()).ToList());
    }
}
