using Bokmal.Database;
using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Api.Services;

public sealed record TopBook(Book Book, int BorrowCount, BookAvailability Availability);

public sealed record Recommendation(Book Book, int SharedBorrowers, BookAvailability Availability);

/// <summary>Recommendations, and the book that prompted them.</summary>
public sealed record RecommendationGroup(Book BasedOn, IReadOnlyList<Recommendation> Recommendations);

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
    /// random". A title everyone reads scores no better than chance and drops out.
    ///
    /// A floor on the shared count keeps a book read by three people from topping the list
    /// on the strength of a coincidence.
    /// </summary>
    /// <param name="excludeBooksBorrowedBy">
    /// When set, titles this borrower has already had out are dropped from the result. The
    /// heading says "find your next book", and a book sitting on the reader's own bedside
    /// table is not a next book.
    ///
    /// Note that this filters the *output* only. Their loans still count towards the signal,
    /// because they are as much a data point about what goes with what as anyone else's --
    /// removing them from the statistics would make the recommendations worse for everybody,
    /// including them. Null for a visitor who has not signed in.
    /// </param>
    public async Task<IReadOnlyList<Recommendation>> RecommendationsForAsync(
        Guid bookId,
        Guid? excludeBooksBorrowedBy,
        int limit,
        CancellationToken cancellationToken)
    {
        var readerships = await LoadReadershipsAsync(cancellationToken);

        var ranked = Rank(readerships, bookId, excludeBooksBorrowedBy, limit, alreadySuggested: new HashSet<Guid>());

        return await DescribeAsync(ranked, cancellationToken);
    }

    /// <summary>
    /// A personal shelf: for each of the borrower's most recent finished books, a few titles
    /// its other readers went on to borrow.
    ///
    /// Built from the last few books rather than everything they have ever read, because a
    /// suggestion is more convincing when the reader can see what prompted it. "Because you
    /// read Dune" is an argument; a single blended list from four years of borrowing is an
    /// assertion.
    ///
    /// Groups never repeat a title. The same book turning up under two headings looks like a
    /// bug even when the arithmetic behind it is sound.
    /// </summary>
    public async Task<IReadOnlyList<RecommendationGroup>> ForBorrowerAsync(
        Guid borrowerId,
        int groups,
        int perGroup,
        CancellationToken cancellationToken)
    {
        // Finished loans only. "Because you read X" has to be true, and a book still out is
        // being read, not read.
        var recentlyRead = await context.Loans
            .Where(l => l.BorrowerId == borrowerId && l.ReturnedAt != null)
            .OrderByDescending(l => l.ReturnedAt)
            .Select(l => l.BookCopy.BookId)
            .ToListAsync(cancellationToken);

        var seeds = recentlyRead.Distinct().Take(groups).ToList();

        if (seeds.Count == 0)
            return [];

        // Loaded once and reused for every seed. Doing it per seed would repeat the same
        // read of the whole loan table for each heading on the page.
        var readerships = await LoadReadershipsAsync(cancellationToken);

        var alreadySuggested = new HashSet<Guid>();
        var ranked = new List<(Guid Seed, List<RankedCandidate> Candidates)>();

        foreach (var seed in seeds)
        {
            var candidates = Rank(readerships, seed, borrowerId, perGroup, alreadySuggested);
            if (candidates.Count == 0) continue;

            foreach (var candidate in candidates)
                alreadySuggested.Add(candidate.BookId);

            ranked.Add((seed, candidates));
        }

        if (ranked.Count == 0)
            return [];

        var described = await DescribeAsync(ranked.SelectMany(r => r.Candidates).ToList(), cancellationToken);
        var byBookId = described.ToDictionary(r => r.Book.Id);

        var seedBooks = await context.Books
            .Where(b => seeds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        return ranked
            .Select(r => new RecommendationGroup(
                seedBooks[r.Seed],
                r.Candidates.Where(c => byBookId.ContainsKey(c.BookId))
                    .Select(c => byBookId[c.BookId])
                    .ToList()))
            .Where(g => g.Recommendations.Count > 0)
            .ToList();
    }

    private sealed record RankedCandidate(Guid BookId, int Shared, double Score);

    private sealed record Readerships(
        Dictionary<Guid, HashSet<Guid>> ReadersByBook,
        Dictionary<Guid, HashSet<Guid>> BooksByReader,
        int TotalReaders);

    /// <summary>
    /// One distinct borrower/book pair per row: who has read what, ignoring how often.
    /// Borrowing the same book three times says no more about taste than borrowing it once,
    /// and would otherwise let one enthusiast outvote a genuine pattern.
    /// </summary>
    private async Task<Readerships> LoadReadershipsAsync(CancellationToken cancellationToken)
    {
        var pairs = await context.Loans
            .Select(l => new { l.BorrowerId, l.BookCopy.BookId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return new Readerships(
            pairs.GroupBy(p => p.BookId).ToDictionary(g => g.Key, g => g.Select(p => p.BorrowerId).ToHashSet()),
            pairs.GroupBy(p => p.BorrowerId).ToDictionary(g => g.Key, g => g.Select(p => p.BookId).ToHashSet()),
            pairs.Select(p => p.BorrowerId).Distinct().Count());
    }

    private static List<RankedCandidate> Rank(
        Readerships readerships,
        Guid bookId,
        Guid? excludeBooksBorrowedBy,
        int limit,
        IReadOnlySet<Guid> alreadySuggested)
    {
        if (!readerships.ReadersByBook.TryGetValue(bookId, out var seedReaders) || seedReaders.Count == 0)
            return [];

        var alreadyBorrowed = excludeBooksBorrowedBy is { } borrowerId
            ? readerships.BooksByReader.GetValueOrDefault(borrowerId, [])
            : [];

        return readerships.ReadersByBook
            .Where(entry => entry.Key != bookId
                            && !alreadyBorrowed.Contains(entry.Key)
                            && !alreadySuggested.Contains(entry.Key))
            .Select(entry =>
            {
                var shared = entry.Value.Count(seedReaders.Contains);
                var candidateShare = (double)entry.Value.Count / readerships.TotalReaders;
                var sharedShare = (double)shared / seedReaders.Count;

                return new RankedCandidate(
                    entry.Key,
                    shared,
                    // How much more likely than chance. 1.0 means "no more than any other member".
                    candidateShare > 0 ? sharedShare / candidateShare : 0);
            })
            .Where(x => x.Shared >= MinimumSharedBorrowers && x.Score > 1)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Shared)
            .Take(limit)
            .ToList();
    }

    private async Task<List<Recommendation>> DescribeAsync(
        List<RankedCandidate> ranked,
        CancellationToken cancellationToken)
    {
        if (ranked.Count == 0)
            return [];

        var ids = ranked.Select(r => r.BookId).ToList();

        var books = await context.Books
            .Where(b => ids.Contains(b.Id))
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
