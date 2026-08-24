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
/// Names the book by id, not by slug.
///
/// The rule across this API: reads address the catalogue by slug, because they end up in
/// URLs that people link and share; writes reference ids, because a command is a statement
/// about identity and that is what an id is for. So `GET /api/books/dune`, but this.
///
/// Readability was the argument for a slug here, and it is a real one -- an id in a log or a
/// network tab means nothing without a lookup. But it is a need that belongs on the server,
/// which knows the book once it has resolved it and can log the title, the copy and the due
/// date rather than just the slug. Solving it in the request would have meant carrying a
/// second identifier that nothing keeps honest.
/// </summary>
public sealed record BorrowRequest(Guid BookId);

public sealed record SignInRequest(string Email);
