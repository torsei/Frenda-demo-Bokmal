using Bokmal.Database;
using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Api.Identity;

/// <summary>
/// Raised when an endpoint needs to know who is asking and cannot find out. Mapped to 401
/// by the exception handler.
/// </summary>
public sealed class NotSignedInException(string message) : Exception(message);

/// <summary>
/// Who the request is on behalf of.
/// </summary>
public interface ICurrentBorrower
{
    /// <summary>The borrower, or null if the request did not identify one.</summary>
    Task<Borrower?> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The borrower, assuming <see cref="RequireBorrowerAttribute"/> has already let the
    /// request through. Throwing here means an endpoint that needs a borrower forgot the
    /// attribute, which is a bug and should look like one.
    /// </summary>
    Task<Borrower> RequireAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Identifies the borrower from an <c>X-Borrower-Email</c> header.
///
/// This is **not authentication**. The header is a claim the client makes about itself and
/// nothing verifies it, so anyone can borrow books as anyone else. That is a deliberate
/// simplification -- the exercise asks for an app that knows who the current borrower is,
/// not for a login -- and it is called out in the README rather than left to be discovered.
///
/// What it does buy is shape. Every consumer depends on <see cref="ICurrentBorrower"/> and
/// none of them look at the header, so replacing this with a JWT or a cookie session means
/// writing one class and registering it. No controller and no service changes.
/// </summary>
public sealed class HeaderCurrentBorrower(
    IHttpContextAccessor httpContextAccessor,
    BokmalDbContext context) : ICurrentBorrower
{
    public const string HeaderName = "X-Borrower-Email";

    private Borrower? _resolved;
    private bool _hasResolved;

    public async Task<Borrower?> GetAsync(CancellationToken cancellationToken)
    {
        // Resolved once per request: several endpoints ask more than once and there is no
        // reason to go back to the database each time.
        if (_hasResolved)
            return _resolved;

        _hasResolved = true;

        var email = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(email))
            return _resolved = null;

        var normalised = email.Trim().ToLowerInvariant();

        return _resolved = await context.Borrowers
            .SingleOrDefaultAsync(b => b.Email == normalised, cancellationToken);
    }

    public async Task<Borrower> RequireAsync(CancellationToken cancellationToken)
        => await GetAsync(cancellationToken)
           ?? throw new NotSignedInException(
               $"No borrower was identified. Send a known library member's address in the {HeaderName} header.");
}
