using Bokmal.Api.Services;
using Bokmal.Database.Entities;

namespace Bokmal.Api.Contracts;

public static class DtoMapping
{
    public static BorrowerDto ToDto(this Borrower borrower)
        => new(borrower.Id, borrower.Email, borrower.DisplayName, borrower.JoinedAt);

    public static AvailabilityDto ToDto(this BookAvailability availability)
        => new(availability.TotalCopies, availability.AvailableCopies, availability.OnLoanCopies, availability.IsAvailable);

    public static ReadingTimeDto ToDto(this ReadingTimeEstimate estimate)
        => new(estimate.TypicalDays, estimate.BasedOnLoans, estimate.FromHistory);

    public static BookSummaryDto ToSummary(this CatalogueEntry entry)
        => ToSummary(entry.Book, entry.Availability, entry.ReadingTime);

    public static BookSummaryDto ToSummary(Book book, BookAvailability availability, ReadingTimeEstimate readingTime)
        => new(
            book.Id,
            book.Slug,
            book.Title,
            book.Author,
            book.Genre,
            book.PublishedYear,
            book.PageCount,
            availability.ToDto(),
            readingTime.ToDto());

    public static BookDetailDto ToDetail(this CatalogueEntry entry, IReadOnlyList<RecommendationDto> alsoBorrowed)
        => new(
            entry.Book.Id,
            entry.Book.Slug,
            entry.Book.Title,
            entry.Book.Author,
            entry.Book.Genre,
            entry.Book.PublishedYear,
            entry.Book.PageCount,
            entry.Book.Description,
            entry.Availability.ToDto(),
            entry.ReadingTime.ToDto(),
            alsoBorrowed);

    public static LoanDto ToDto(this LoanView view, DateTime now)
        => new(
            view.Loan.Id,
            view.Book.Id,
            view.Book.Slug,
            view.Book.Title,
            view.Book.Author,
            view.CopyNumber,
            view.Loan.BorrowedAt,
            view.Loan.DueAt,
            view.Loan.ReturnedAt,
            // A returned loan is never overdue, however late it came back. Overdue is a
            // thing you can act on, and there is nothing left to do about a finished loan.
            IsOverdue: view.Loan.ReturnedAt is null && view.Loan.DueAt < now);
}
