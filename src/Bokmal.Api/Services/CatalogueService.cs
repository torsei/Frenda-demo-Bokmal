using Bokmal.Database;
using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Api.Services;

public sealed record BookAvailability(int TotalCopies, int AvailableCopies)
{
    public int OnLoanCopies => TotalCopies - AvailableCopies;
    public bool IsAvailable => AvailableCopies > 0;
}

public sealed record CatalogueEntry(Book Book, BookAvailability Availability, ReadingTimeEstimate ReadingTime);

/// <summary>
/// Browsing the shelves.
/// </summary>
public sealed class CatalogueService(BokmalDbContext context, ReadingTimeService readingTimes)
{
    public async Task<IReadOnlyList<CatalogueEntry>> BrowseAsync(
        string? search,
        string? genre,
        CancellationToken cancellationToken)
    {
        var query = context.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Lowercase on both sides rather than relying on the database's default
            // collation, which is case-insensitive in SQLite, case-sensitive in Postgres
            // and whatever the server was installed with in SQL Server. Searching should
            // not behave differently depending on where the app is deployed.
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(b =>
                b.Title.ToLower().Contains(term) ||
                b.Author.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(b => b.Genre == genre);

        var books = await query
            .OrderBy(b => b.Title)
            .Select(b => new
            {
                Book = b,
                TotalCopies = b.BookCopies.Count(),
                AvailableCopies = b.BookCopies.Count(c => c.Status == CopyStatuses.Available)
            })
            .ToListAsync(cancellationToken);

        var bookIds = books.Select(b => b.Book.Id).ToList();
        var estimates = await readingTimes.EstimateAsync(bookIds, cancellationToken);

        return books
            .Select(b => new CatalogueEntry(
                b.Book,
                new BookAvailability(b.TotalCopies, b.AvailableCopies),
                estimates[b.Book.Id]))
            .ToList();
    }

    public async Task<CatalogueEntry?> FindAsync(string slug, CancellationToken cancellationToken)
    {
        var found = await context.Books
            .Where(b => b.Slug == slug)
            .Select(b => new
            {
                Book = b,
                TotalCopies = b.BookCopies.Count(),
                AvailableCopies = b.BookCopies.Count(c => c.Status == CopyStatuses.Available)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
            return null;

        var estimates = await readingTimes.EstimateAsync([found.Book.Id], cancellationToken);

        return new CatalogueEntry(
            found.Book,
            new BookAvailability(found.TotalCopies, found.AvailableCopies),
            estimates[found.Book.Id]);
    }

    /// <summary>
    /// How many copies of each of these books are on the shelf. Used where books arrive from
    /// somewhere other than a catalogue query -- a recommendation, say -- but still need a
    /// card that says whether the reader can act on it.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, BookAvailability>> AvailabilityForAsync(
        IReadOnlyList<Guid> bookIds,
        CancellationToken cancellationToken)
    {
        if (bookIds.Count == 0)
            return new Dictionary<Guid, BookAvailability>();

        var counts = await context.Books
            .Where(b => bookIds.Contains(b.Id))
            .Select(b => new
            {
                b.Id,
                TotalCopies = b.BookCopies.Count(),
                AvailableCopies = b.BookCopies.Count(c => c.Status == CopyStatuses.Available)
            })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.Id, c => new BookAvailability(c.TotalCopies, c.AvailableCopies));
    }

    public Task<List<string>> ListGenresAsync(CancellationToken cancellationToken)
        => context.Books.Select(b => b.Genre).Distinct().OrderBy(g => g).ToListAsync(cancellationToken);

}
