using Bokmal.Database;
using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Api.Services;

public sealed record TopBook(Book Book, int BorrowCount, BookAvailability Availability);

public sealed record Recommendation(Book Book, int SharedBorrowers, BookAvailability Availability);

/// <summary>
/// Finding the next book: what the library borrows most, and what people who liked this
/// one went on to read.
/// </summary>
public sealed class DiscoveryService(BokmalDbContext context)
{
    /// <summary>
    /// Below this, an overlap is a coincidence rather than a pattern.
    /// </summary>
    public const int MinimumSharedBorrowers = 3;

    public async Task<IReadOnlyList<TopBook>> TopBorrowedAsync(int limit, CancellationToken cancellationToken)
    {
        return await context.Books
            .Select(b => new
            {
                Book = b,
                BorrowCount = b.BookCopies.SelectMany(c => c.Loans).Count(),
                TotalCopies = b.BookCopies.Count(),
                AvailableCopies = b.BookCopies.Count(c => c.Status == CopyStatuses.Available)
            })
            .OrderByDescending(x => x.BorrowCount)
            .ThenBy(x => x.Book.Title)
            .Take(limit)
            .Select(x => new TopBook(
                x.Book,
                x.BorrowCount,
                new BookAvailability(x.TotalCopies, x.AvailableCopies)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// "Others who borrowed this also borrowed..."
    ///
    /// The obvious implementation -- count how many of this book's borrowers also borrowed
    /// each other book, rank by that count -- does not work. It recommends the library's
    /// bestsellers to everybody, because a book that half the members have read overlaps
    /// heavily with *everything*. The first version of this produced "readers of Dune also
    /// enjoyed The Girl with the Dragon Tattoo", which is true and useless.
    ///
    /// So the raw overlap is weighed against how popular the candidate is anyway. The
    /// question asked is not "how many readers do these two share" but "are this book's
    /// readers unusually likely to have read that one, compared with a member picked at
    /// random". A title everyone reads scores no better than chance and drops out; a title
    /// that specifically this book's readers gravitate to rises.
    ///
    /// A floor on the shared count keeps a book read by three people from topping the list
    /// on the strength of a coincidence.
    /// </summary>
    public async Task<IReadOnlyList<Recommendation>> RecommendationsForAsync(
        Guid bookId,
        int limit,
        CancellationToken cancellationToken)
    {
        // One distinct borrower/book pair per row: who has read what, ignoring how often.
        // Borrowing the same book three times says no more about taste than borrowing it
        // once, and would otherwise let one enthusiast outvote a genuine pattern.
        var readerships = await context.Loans
            .Select(l => new { l.BorrowerId, l.BookCopy.BookId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var readersByBook = readerships
            .GroupBy(r => r.BookId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.BorrowerId).ToHashSet());

        if (!readersByBook.TryGetValue(bookId, out var seedReaders) || seedReaders.Count == 0)
            return [];

        var totalReaders = readerships.Select(r => r.BorrowerId).Distinct().Count();

        var ranked = readersByBook
            .Where(entry => entry.Key != bookId)
            .Select(entry =>
            {
                var shared = entry.Value.Count(seedReaders.Contains);
                var candidateShare = (double)entry.Value.Count / totalReaders;
                var sharedShare = (double)shared / seedReaders.Count;

                return new
                {
                    BookId = entry.Key,
                    Shared = shared,
                    // How much more likely than chance. 1.0 means "no more than any other member".
                    Score = candidateShare > 0 ? sharedShare / candidateShare : 0
                };
            })
            .Where(x => x.Shared >= MinimumSharedBorrowers && x.Score > 1)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Shared)
            .Take(limit)
            .ToList();

        var recommendedIds = ranked.Select(r => r.BookId).ToList();

        var books = await context.Books
            .Where(b => recommendedIds.Contains(b.Id))
            .Select(b => new
            {
                Book = b,
                TotalCopies = b.BookCopies.Count(),
                AvailableCopies = b.BookCopies.Count(c => c.Status == CopyStatuses.Available)
            })
            .ToDictionaryAsync(x => x.Book.Id, cancellationToken);

        return ranked
            .Where(r => books.ContainsKey(r.BookId))
            .Select(r => new Recommendation(
                books[r.BookId].Book,
                r.Shared,
                new BookAvailability(books[r.BookId].TotalCopies, books[r.BookId].AvailableCopies)))
            .ToList();
    }
}
