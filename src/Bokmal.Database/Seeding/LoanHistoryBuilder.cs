using Bokmal.Database.Entities;

namespace Bokmal.Database.Seeding;

/// <summary>
/// Invents a year and a bit of plausible borrowing.
///
/// The top list, the recommendations and the reading-time estimate are all derived from
/// loan history, so a library with an empty or uniformly random history would render three
/// of its five features as noise. The history is therefore shaped, and every knob below
/// exists to make one of those features say something true:
///
///   * Borrowers pick books along their taste, so co-borrowing patterns cluster and
///     "others also borrowed" has signal in it.
///   * Popular titles come back off the shelf quickly and quiet ones sit, so the top list
///     has a real ranking rather than a photo finish.
///   * Loan lengths come from page count and a plausible reading pace, plus a small share
///     of readers who keep a book for months. Those stragglers are precisely why the
///     estimate reports a median and not a mean.
///
/// Deterministic for a given seed, so a given database always comes out the same.
/// </summary>
public sealed class LoanHistoryBuilder(TimeProvider timeProvider, int randomSeed = 20260815)
{
    private const int HistoryDays = 440;

    /// <summary>Only a loan started this recently is left open at the end of the run.</summary>
    private const int PlausiblyStillOutDays = 26;

    private readonly Random _random = new(randomSeed);

    public IReadOnlyList<Loan> Build(
        IReadOnlyList<Borrower> borrowers,
        IReadOnlyDictionary<Guid, DemoBook> booksById,
        IReadOnlyList<BookCopy> copies,
        IReadOnlyDictionary<Guid, string> borrowerGenres)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var start = now.AddDays(-HistoryDays);

        var tasteIndex = BuildTasteIndex(borrowers, borrowerGenres);
        var loans = new List<Loan>();
        var openLoans = new List<Loan>();

        foreach (var copy in copies)
        {
            var book = booksById[copy.BookId];
            var cursor = start.AddDays(_random.Next(0, 46));

            while (cursor < now)
            {
                var borrower = ChooseBorrower(tasteIndex, book.Genre, borrowers);
                var borrowedAt = cursor;
                var returnedAt = borrowedAt.AddDays(ReadingDays(book));

                var loan = new Loan
                {
                    Id = BokmalId.New(),
                    BookCopyId = copy.Id,
                    BorrowerId = borrower.Id,
                    BorrowedAt = borrowedAt,
                    DueAt = borrowedAt.AddDays(LoanPolicy.LoanPeriodDays),
                    ReturnedAt = returnedAt
                };

                if (returnedAt >= now)
                {
                    // Still out. A copy borrowed eleven months ago and never returned is
                    // not a library, it is a theft report, so only recent ones stay open.
                    if ((now - borrowedAt).TotalDays >= PlausiblyStillOutDays)
                        break;

                    loan.ReturnedAt = null;
                    loans.Add(loan);
                    openLoans.Add(loan);
                    break;
                }

                loans.Add(loan);

                // The next borrower does not turn up the moment the book is back.
                cursor = returnedAt.AddDays(ShelfRestDays(book.Demand));
            }
        }

        CloseLoansThatBreakThePolicy(openLoans, booksById, copies, now);

        return loans;
    }

    /// <summary>
    /// The demo library must start in a state a borrower could actually have reached, so
    /// the invented history is held to the same rules the API enforces. Anything over the
    /// line is simply returned early rather than dropped, which keeps the history intact.
    /// </summary>
    private void CloseLoansThatBreakThePolicy(
        List<Loan> openLoans,
        IReadOnlyDictionary<Guid, DemoBook> booksById,
        IReadOnlyList<BookCopy> copies,
        DateTime now)
    {
        var bookByCopy = copies.ToDictionary(c => c.Id, c => c.BookId);
        var activeCount = new Dictionary<Guid, int>();
        var activeBooks = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var loan in openLoans)
        {
            var bookId = bookByCopy[loan.BookCopyId];
            var count = activeCount.GetValueOrDefault(loan.BorrowerId);
            var books = activeBooks.TryGetValue(loan.BorrowerId, out var set) ? set : [];

            if (count >= LoanPolicy.MaxActiveLoansPerBorrower || books.Contains(bookId))
            {
                var returnedAt = loan.BorrowedAt.AddDays(_random.Next(4, 21));
                loan.ReturnedAt = returnedAt < now ? returnedAt : now.AddDays(-1);
                continue;
            }

            activeCount[loan.BorrowerId] = count + 1;
            books.Add(bookId);
            activeBooks[loan.BorrowerId] = books;
        }
    }

    private static Dictionary<string, (List<Borrower> Favourite, List<Borrower> Second)> BuildTasteIndex(
        IReadOnlyList<Borrower> borrowers,
        IReadOnlyDictionary<Guid, string> borrowerGenres)
    {
        var index = new Dictionary<string, (List<Borrower>, List<Borrower>)>();

        foreach (var borrower in borrowers)
        {
            var taste = DemoCatalogue.Borrowers.Single(b => b.Email == borrower.Email);

            foreach (var (genre, isFavourite) in new[] { (taste.FavouriteGenre, true), (taste.SecondGenre, false) })
            {
                if (!index.TryGetValue(genre, out var lists))
                    index[genre] = lists = ([], []);

                (isFavourite ? lists.Item1 : lists.Item2).Add(borrower);
            }
        }

        return index;
    }

    private Borrower ChooseBorrower(
        Dictionary<string, (List<Borrower> Favourite, List<Borrower> Second)> tasteIndex,
        string genre,
        IReadOnlyList<Borrower> everyone)
    {
        var roll = _random.NextDouble();

        if (tasteIndex.TryGetValue(genre, out var lists))
        {
            if (roll < 0.62 && lists.Favourite.Count > 0)
                return lists.Favourite[_random.Next(lists.Favourite.Count)];

            if (roll < 0.88 && lists.Second.Count > 0)
                return lists.Second[_random.Next(lists.Second.Count)];
        }

        // Everyone reads outside their taste occasionally, and without this the clusters
        // would be airtight and the recommendations suspiciously tidy.
        return everyone[_random.Next(everyone.Count)];
    }

    private int ReadingDays(DemoBook book)
    {
        var pagesPerDay = 24 + _random.NextDouble() * 22;
        var reading = book.PageCount / pagesPerDay;
        var onTheNightstand = 0.5 + _random.NextDouble() * 4.5;
        var abandonedForAWhile = _random.NextDouble() < 0.06 ? 30 + _random.NextDouble() * 50 : 0;

        return Math.Max(1, (int)Math.Round(reading + onTheNightstand + abandonedForAWhile));
    }

    private int ShelfRestDays(Demand demand) => demand switch
    {
        Demand.Popular => _random.Next(18, 51),
        Demand.Steady => _random.Next(40, 101),
        _ => _random.Next(80, 171)
    };
}
