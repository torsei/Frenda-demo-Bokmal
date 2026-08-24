using Bokmal.Database;
using Bokmal.Database.Entities;
using Bokmal.Tests.Databases;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Tests;

/// <summary>
/// The top list and the recommendations.
///
/// Both are built from loan history, so these tests write history directly rather than
/// borrowing and returning through the service. That keeps each test's premise visible: the
/// point of a recommendation test is which books which people read, and routing that through
/// the loan flow would bury it under twenty lines of setup.
/// </summary>
public class DiscoveryTests
{
    /// <summary>Writes a finished loan straight into history.</summary>
    private static async Task RecordLoanAsync(
        BokmalDbContext context,
        string bookSlug,
        string borrowerEmail,
        int days = 7)
    {
        var copyId = await context.BookCopies
            .Where(c => c.Book.Slug == bookSlug)
            .OrderBy(c => c.CopyNumber)
            .Select(c => c.Id)
            .FirstAsync();

        var borrowerId = await context.Borrowers
            .Where(b => b.Email == borrowerEmail)
            .Select(b => b.Id)
            .SingleAsync();

        var borrowedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        context.Loans.Add(new Loan
        {
            Id = BokmalId.New(),
            BookCopyId = copyId,
            BorrowerId = borrowerId,
            BorrowedAt = borrowedAt,
            DueAt = borrowedAt.AddDays(LoanPolicy.LoanPeriodDays),
            ReturnedAt = borrowedAt.AddDays(days)
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task The_top_list_ranks_by_how_often_a_book_was_borrowed()
    {
        using var library = await Library.WithAsync(b => b
            .Book("popular", copies: 1).Book("middling", copies: 1).Book("ignored", copies: 1)
            .Borrower("a@example.se").Borrower("b@example.se").Borrower("c@example.se"));

        await using var context = library.CreateContext();

        foreach (var reader in new[] { "a", "b", "c" })
            await RecordLoanAsync(context, "popular", $"{reader}@example.se");

        await RecordLoanAsync(context, "middling", "a@example.se");

        var top = await library.CreateDiscoveryService(context).TopBorrowedAsync(10, default);

        Assert.Equal("popular", top[0].Book.Slug);
        Assert.Equal(3, top[0].BorrowCount);
        Assert.Equal("middling", top[1].Book.Slug);

        // A book nobody borrowed still belongs in the catalogue, just at the bottom.
        Assert.Equal("ignored", top[2].Book.Slug);
        Assert.Equal(0, top[2].BorrowCount);
    }

    [Fact]
    public async Task A_book_never_recommends_itself()
    {
        using var library = await Library.WithAsync(b => b
            .Book("dune", copies: 1).Book("neuromancer", copies: 1)
            .Borrower("a@example.se").Borrower("b@example.se").Borrower("c@example.se"));

        await using var context = library.CreateContext();

        foreach (var reader in new[] { "a", "b", "c" })
        {
            await RecordLoanAsync(context, "dune", $"{reader}@example.se");
            await RecordLoanAsync(context, "neuromancer", $"{reader}@example.se");
        }

        var dune = await context.Books.SingleAsync(b => b.Slug == "dune");
        var recommendations = await library.CreateDiscoveryService(context)
            .RecommendationsForAsync(dune.Id, null, limit: 5, default);

        Assert.DoesNotContain(recommendations, r => r.Book.Slug == "dune");
    }

    [Fact]
    public async Task A_single_shared_reader_is_not_enough_to_be_recommended()
    {
        using var library = await Library.WithAsync(b => b
            .Book("dune", copies: 1).Book("coincidence", copies: 1)
            .Borrower("a@example.se").Borrower("b@example.se").Borrower("c@example.se"));

        await using var context = library.CreateContext();

        foreach (var reader in new[] { "a", "b", "c" })
            await RecordLoanAsync(context, "dune", $"{reader}@example.se");

        await RecordLoanAsync(context, "coincidence", "a@example.se");

        var dune = await context.Books.SingleAsync(b => b.Slug == "dune");
        var recommendations = await library.CreateDiscoveryService(context)
            .RecommendationsForAsync(dune.Id, null, limit: 5, default);

        Assert.Empty(recommendations);
    }

    /// <summary>
    /// The test the recommendation query was rewritten for.
    ///
    /// Everyone in this library has read the bestseller. Dune's readers have also all read
    /// its companion. Counting shared readers, the bestseller ties or wins -- and the first
    /// version of this feature duly recommended the library's most popular book to everybody,
    /// which is true and useless.
    ///
    /// Weighing the overlap against how widely read the candidate is anyway asks a better
    /// question: are Dune's readers *unusually* likely to have read this? For the bestseller
    /// the answer is no, everybody is, so it drops out.
    /// </summary>
    [Fact]
    public async Task A_book_everybody_reads_is_not_recommended_to_everybody()
    {
        var readers = new[] { "a", "b", "c", "d", "e", "f" };

        using var library = await Library.WithAsync(builder =>
        {
            builder.Book("dune", copies: 1).Book("companion", copies: 1).Book("bestseller", copies: 1);
            foreach (var reader in readers) builder.Borrower($"{reader}@example.se");
        });

        await using var context = library.CreateContext();

        // Everybody reads the bestseller.
        foreach (var reader in readers)
            await RecordLoanAsync(context, "bestseller", $"{reader}@example.se");

        // Half of them read Dune, and exactly those also read its companion.
        foreach (var reader in readers.Take(3))
        {
            await RecordLoanAsync(context, "dune", $"{reader}@example.se");
            await RecordLoanAsync(context, "companion", $"{reader}@example.se");
        }

        var dune = await context.Books.SingleAsync(b => b.Slug == "dune");
        var recommendations = await library.CreateDiscoveryService(context)
            .RecommendationsForAsync(dune.Id, null, limit: 5, default);

        Assert.Equal("companion", Assert.Single(recommendations).Book.Slug);
    }

    [Fact]
    public async Task A_library_where_everybody_has_read_everything_recommends_nothing()
    {
        // Surprising the first time it happens, and correct. When every member has read both
        // books, the overlap is total but so is the overlap with everything else -- readers
        // of one are no more likely than anyone to have read the other, because there is no
        // "anyone" left to compare against. Recommending on that would be recommending noise.
        //
        // Written down because it looks like a bug in a small test fixture, and someone will
        // eventually be tempted to "fix" it by dropping the threshold.
        using var library = await Library.WithAsync(b => b
            .Book("dune", copies: 1).Book("companion", copies: 1)
            .Borrower("a@example.se").Borrower("b@example.se").Borrower("c@example.se"));

        await using var context = library.CreateContext();

        foreach (var reader in new[] { "a", "b", "c" })
        {
            await RecordLoanAsync(context, "dune", $"{reader}@example.se");
            await RecordLoanAsync(context, "companion", $"{reader}@example.se");
        }

        var dune = await context.Books.SingleAsync(b => b.Slug == "dune");

        Assert.Empty(await library.CreateDiscoveryService(context)
            .RecommendationsForAsync(dune.Id, null, limit: 5, default));
    }

    /// <summary>
    /// Builds a library where Dune's readers reliably also read two companion titles, so
    /// both are strong recommendations for Dune. Six members read something else entirely,
    /// which is what gives the comparison a population to work against.
    /// </summary>
    private static async Task<Library> WithTwoStrongCompanionsAsync()
    {
        var duneReaders = new[] { "a", "b", "c" };
        var others = new[] { "d", "e", "f", "g", "h", "i" };

        var library = await Library.WithAsync(builder =>
        {
            builder.Book("dune", copies: 1)
                .Book("companion-one", copies: 1)
                .Book("companion-two", copies: 1)
                .Book("elsewhere", copies: 1);

            foreach (var reader in duneReaders.Concat(others))
                builder.Borrower($"{reader}@example.se");
        });

        await using var context = library.CreateContext();

        foreach (var reader in duneReaders)
        {
            await RecordLoanAsync(context, "dune", $"{reader}@example.se");
            await RecordLoanAsync(context, "companion-one", $"{reader}@example.se");
            await RecordLoanAsync(context, "companion-two", $"{reader}@example.se");
        }

        foreach (var reader in others)
            await RecordLoanAsync(context, "elsewhere", $"{reader}@example.se");

        return library;
    }

    [Fact]
    public async Task A_book_the_reader_has_already_borrowed_is_not_recommended_to_them()
    {
        // "Find your next book" has to mean next. Suggesting something already read, or worse
        // something sitting on the reader's own bedside table right now, is the one way a
        // recommendation can be both correct and actively unhelpful.
        using var library = await WithTwoStrongCompanionsAsync();
        await using var context = library.CreateContext();

        var dune = await context.Books.SingleAsync(b => b.Slug == "dune");
        var discovery = library.CreateDiscoveryService(context);

        var anonymous = await discovery.RecommendationsForAsync(dune.Id, null, limit: 5, default);
        Assert.Equal(
            ["companion-one", "companion-two"],
            anonymous.Select(r => r.Book.Slug).Order());

        // Reader "a" has read both companions, so neither is news to them.
        var readerA = await library.BorrowerIdAsync("a@example.se");
        Assert.Empty(await discovery.RecommendationsForAsync(dune.Id, readerA, limit: 5, default));

        // Reader "d" has read neither, and still gets both.
        var readerD = await library.BorrowerIdAsync("d@example.se");
        Assert.Equal(2, (await discovery.RecommendationsForAsync(dune.Id, readerD, limit: 5, default)).Count);
    }

    [Fact]
    public async Task A_readers_own_loans_still_count_towards_what_gets_recommended()
    {
        // The filter applies to the output, not to the signal. Reader "a" is one of the three
        // whose borrowing established that Dune and companion-two belong together; dropping
        // their loans from the statistics would weaken the recommendation for everyone else.
        using var library = await WithTwoStrongCompanionsAsync();
        await using var context = library.CreateContext();

        var dune = await context.Books.SingleAsync(b => b.Slug == "dune");
        var readerD = await library.BorrowerIdAsync("d@example.se");

        var forReaderD = await library.CreateDiscoveryService(context)
            .RecommendationsForAsync(dune.Id, readerD, limit: 5, default);

        // Three readers link Dune to its companion: a, b and c. Reader "a" is filtered out of
        // their own recommendations, but is still one of the three counted here.
        Assert.Equal(3, forReaderD.Single(r => r.Book.Slug == "companion-two").SharedBorrowers);
    }

    [Fact]
    public async Task A_personal_shelf_is_grouped_by_the_books_that_prompted_it()
    {
        using var library = await WithTwoStrongCompanionsAsync();
        await using var context = library.CreateContext();

        // Reader "d" has read neither companion, so both are still news to them. Give them a
        // finished loan of Dune to base suggestions on.
        await RecordLoanAsync(context, "dune", "d@example.se");

        var readerD = await library.BorrowerIdAsync("d@example.se");
        var shelf = await library.CreateDiscoveryService(context)
            .ForBorrowerAsync(readerD, groups: 2, perGroup: 3, default);

        var group = Assert.Single(shelf);
        Assert.Equal("dune", group.BasedOn.Slug);
        Assert.Equal(
            ["companion-one", "companion-two"],
            group.Recommendations.Select(r => r.Book.Slug).Order());
    }

    [Fact]
    public async Task A_personal_shelf_never_lists_the_same_book_under_two_headings()
    {
        // Two books with overlapping readerships would otherwise both suggest the same third
        // title, which reads as a bug however sound the arithmetic behind it is.
        var readers = new[] { "a", "b", "c", "d", "e", "f" };

        using var library = await Library.WithAsync(builder =>
        {
            builder.Book("first", copies: 1).Book("second", copies: 1)
                .Book("shared-favourite", copies: 1).Book("elsewhere", copies: 1);
            foreach (var reader in readers) builder.Borrower($"{reader}@example.se");
        });

        await using var context = library.CreateContext();

        foreach (var reader in readers.Take(3))
        {
            await RecordLoanAsync(context, "first", $"{reader}@example.se");
            await RecordLoanAsync(context, "second", $"{reader}@example.se");
            await RecordLoanAsync(context, "shared-favourite", $"{reader}@example.se");
        }

        foreach (var reader in readers.Skip(3))
            await RecordLoanAsync(context, "elsewhere", $"{reader}@example.se");

        // Reader "d" has finished both seeds and neither companion.
        await RecordLoanAsync(context, "first", "d@example.se", days: 5);
        await RecordLoanAsync(context, "second", "d@example.se", days: 9);

        var readerD = await library.BorrowerIdAsync("d@example.se");
        var shelf = await library.CreateDiscoveryService(context)
            .ForBorrowerAsync(readerD, groups: 2, perGroup: 3, default);

        var suggested = shelf.SelectMany(g => g.Recommendations.Select(r => r.Book.Slug)).ToList();

        Assert.Equal(suggested.Count, suggested.Distinct().Count());
        Assert.Contains("shared-favourite", suggested);
    }

    [Fact]
    public async Task The_most_recently_finished_book_leads_the_personal_shelf()
    {
        using var library = await WithTwoStrongCompanionsAsync();
        await using var context = library.CreateContext();

        // Both are seeds; "companion-one" was handed back later, so it comes first.
        await RecordLoanAsync(context, "dune", "d@example.se", days: 3);
        await RecordLoanAsync(context, "companion-one", "d@example.se", days: 30);

        var readerD = await library.BorrowerIdAsync("d@example.se");
        var shelf = await library.CreateDiscoveryService(context)
            .ForBorrowerAsync(readerD, groups: 2, perGroup: 3, default);

        Assert.Equal("companion-one", shelf[0].BasedOn.Slug);
    }

    [Fact]
    public async Task A_reader_who_has_finished_nothing_gets_no_personal_shelf()
    {
        // Nothing to base a suggestion on, so the section does not appear at all. An empty
        // "For you" heading is worse than no heading.
        using var library = await WithTwoStrongCompanionsAsync();
        await using var context = library.CreateContext();

        var newMember = await library.BorrowerIdAsync("i@example.se");

        Assert.Empty(await library.CreateDiscoveryService(context)
            .ForBorrowerAsync(newMember, groups: 2, perGroup: 3, default));
    }

    [Fact]
    public async Task Recommendations_carry_the_availability_the_reader_needs_to_act_on_them()
    {
        // Six members, not three. With a membership where everybody has read everything the
        // overlap between any two books is total, no book is more associated with another
        // than chance would give, and the query correctly returns nothing -- which is right
        // but makes for a useless fixture.
        using var library = await Library.WithAsync(builder =>
        {
            builder.Book("dune", copies: 1).Book("companion", copies: 2).Book("elsewhere", copies: 1);
            foreach (var reader in new[] { "a", "b", "c", "d", "e", "f" })
                builder.Borrower($"{reader}@example.se");
        });

        await using var context = library.CreateContext();

        foreach (var reader in new[] { "a", "b", "c" })
        {
            await RecordLoanAsync(context, "dune", $"{reader}@example.se");
            await RecordLoanAsync(context, "companion", $"{reader}@example.se");
        }

        foreach (var reader in new[] { "d", "e", "f" })
            await RecordLoanAsync(context, "elsewhere", $"{reader}@example.se");

        var dune = await context.Books.SingleAsync(b => b.Slug == "dune");
        var recommendation = Assert.Single(await library.CreateDiscoveryService(context)
            .RecommendationsForAsync(dune.Id, null, limit: 5, default));

        Assert.Equal(2, recommendation.Availability.TotalCopies);
        Assert.Equal(2, recommendation.Availability.AvailableCopies);
        Assert.Equal(3, recommendation.SharedBorrowers);
    }

    [Fact]
    public async Task The_catalogue_reports_availability_and_reading_time_together()
    {
        using var library = await Library.WithAsync(b => b
            .Book("dune", copies: 3, pageCount: 400).Borrower("a@example.se"));

        await using var context = library.CreateContext();

        foreach (var days in new[] { 6, 8, 10, 12 })
            await RecordLoanAsync(context, "dune", "a@example.se", days);

        var astrid = await library.BorrowerIdAsync("a@example.se");
        await library.CreateLoanService(context).BorrowAsync(astrid, await library.BookIdAsync("dune"), default);

        await using var reader = library.CreateContext();
        var entry = await library.CreateCatalogueService(reader).FindAsync("dune", default);

        Assert.NotNull(entry);
        Assert.Equal(3, entry.Availability.TotalCopies);
        Assert.Equal(2, entry.Availability.AvailableCopies);
        Assert.Equal(1, entry.Availability.OnLoanCopies);
        Assert.True(entry.ReadingTime.FromHistory);
        Assert.Equal(9, entry.ReadingTime.TypicalDays);
    }
}
