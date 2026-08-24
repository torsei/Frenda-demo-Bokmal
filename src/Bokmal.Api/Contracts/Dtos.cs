namespace Bokmal.Api.Contracts;

/// <summary>
/// The wire format. Kept separate from the entities so that the shape of the database and
/// the shape of the API can change independently -- and because these types are what the
/// TypeScript client is generated from, so anything exposed here becomes frontend surface.
/// </summary>
public sealed record BorrowerDto(Guid Id, string Email, string DisplayName, DateTime JoinedAt);

public sealed record AvailabilityDto(int TotalCopies, int AvailableCopies, int OnLoanCopies, bool IsAvailable);

/// <param name="TypicalDays">How long the book usually stays out.</param>
/// <param name="BasedOnLoans">
/// How many finished loans the figure rests on. Sent so the interface can distinguish a
/// well-supported estimate from a guess instead of presenting both with equal confidence.
/// </param>
/// <param name="FromHistory">False when there was too little history and page count was used instead.</param>
public sealed record ReadingTimeDto(int TypicalDays, int BasedOnLoans, bool FromHistory);

public sealed record BookSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string Author,
    string Genre,
    int PublishedYear,
    int PageCount,
    AvailabilityDto Availability,
    ReadingTimeDto ReadingTime);

public sealed record BookDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string Author,
    string Genre,
    int PublishedYear,
    int PageCount,
    string Description,
    AvailabilityDto Availability,
    ReadingTimeDto ReadingTime,
    IReadOnlyList<RecommendationDto> AlsoBorrowed);

/// <param name="SharedBorrowers">
/// How many members borrowed both books. Shown so a recommendation can say why it is here.
/// </param>
public sealed record RecommendationDto(BookSummaryDto Book, int SharedBorrowers);

public sealed record TopBookDto(BookSummaryDto Book, int BorrowCount);

/// <summary>
/// Suggestions with the book that prompted them, so the interface can say why. A blended
/// list with no explanation is an assertion; "because you read Dune" is an argument.
/// </summary>
public sealed record RecommendationGroupDto(BookSummaryDto BasedOn, IReadOnlyList<RecommendationDto> Books);

public sealed record LoanDto(
    Guid Id,
    Guid BookId,
    string BookSlug,
    string BookTitle,
    string Author,
    int CopyNumber,
    DateTime BorrowedAt,
    DateTime DueAt,
    DateTime? ReturnedAt,
    bool IsOverdue);

public sealed record MyLoansDto(IReadOnlyList<LoanDto> Current, IReadOnlyList<LoanDto> Past);

/// <summary>
/// Names the book by slug rather than by id, deliberately.
///
/// A slug is readable in a log, in a network tab and in a bug report from a colleague; an
/// opaque id needs a lookup every time. That is a small win, but it is one that recurs for
/// as long as the system is alive.
///
/// The usual objection is that a slug can change while an id cannot. Worth asking what
/// happens when it does: the request matches no book and comes back as a clean 404. It does
/// not borrow the wrong one. That holds as long as a slug is never reused for a different
/// title, which is the actual invariant here -- rename a book and you add an alias, you do
/// not hand its old name to something else.
///
/// Loans go the other way and are addressed by id, which is not an inconsistency: a book is
/// a public catalogue entry with a name people recognise, and a loan is a private record
/// with no natural name. Each is addressed by what it actually has.
/// </summary>
public sealed record BorrowRequest(string BookSlug);

public sealed record SignInRequest(string Email);
