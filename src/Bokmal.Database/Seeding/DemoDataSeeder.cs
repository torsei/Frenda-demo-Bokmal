using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bokmal.Database.Seeding;

/// <summary>
/// Fills an empty database with the demo library.
///
/// Seeding is written in C# rather than as SQL migration scripts on purpose. Hand-written
/// SQL has to agree with how EF Core stores things, and it silently does not: EF writes
/// GUIDs uppercase while SQLite compares text case-sensitively, so a lowercase literal
/// produces a row that exists but that no lookup by id can find. Timestamps have the same
/// hazard in a different costume. Going through the model instead removes the entire class
/// of defect rather than guarding it with tests -- and lets the shaping of the loan history
/// be readable code with its reasoning attached.
///
/// The schema stays in DbUp where DDL belongs. This handles data.
/// </summary>
public sealed class DemoDataSeeder(
    BokmalDbContext context,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder>? logger = null)
{
    public async Task<bool> SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if (await context.Books.AnyAsync(cancellationToken))
        {
            logger?.LogInformation("Library already has books; skipping demo data");
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        var borrowers = DemoCatalogue.Borrowers
            .Select((demo, index) => new Borrower
            {
                Id = BokmalId.New(),
                Email = demo.Email,
                DisplayName = demo.DisplayName,
                // Members did not all join on the same day.
                JoinedAt = now.AddDays(-(500 + index * 37))
            })
            .ToList();

        var books = new List<Book>();
        var copies = new List<BookCopy>();
        var demoByBookId = new Dictionary<Guid, DemoBook>();

        foreach (var demo in DemoCatalogue.Books)
        {
            var book = new Book
            {
                Id = BokmalId.New(),
                Slug = demo.Slug,
                Title = demo.Title,
                Author = demo.Author,
                Genre = demo.Genre,
                PublishedYear = demo.PublishedYear,
                PageCount = demo.PageCount,
                Description = demo.Description
            };

            books.Add(book);
            demoByBookId[book.Id] = demo;

            for (var number = 1; number <= demo.Copies; number++)
            {
                copies.Add(new BookCopy
                {
                    Id = BokmalId.New(),
                    BookId = book.Id,
                    CopyNumber = number,
                    Status = CopyStatuses.Available
                });
            }
        }

        var borrowerGenres = borrowers.ToDictionary(
            b => b.Id,
            b => DemoCatalogue.Borrowers.Single(d => d.Email == b.Email).FavouriteGenre);

        var loans = new LoanHistoryBuilder(timeProvider)
            .Build(borrowers, demoByBookId, copies, borrowerGenres);

        // Availability is read from book_copy.status, so it has to agree with the loans
        // that are still open. Deriving it here rather than setting it by hand means the
        // two cannot disagree.
        var copiesStillOut = loans
            .Where(l => l.ReturnedAt is null)
            .Select(l => l.BookCopyId)
            .ToHashSet();

        foreach (var copy in copies.Where(c => copiesStillOut.Contains(c.Id)))
            copy.Status = CopyStatuses.OnLoan;

        context.AddRange(borrowers);
        context.AddRange(books);
        context.AddRange(copies);
        context.AddRange(loans);

        // Every identifier was assigned in memory, so the whole library goes in as one
        // transaction. Nothing here depends on a key the database hands back.
        await context.SaveChangesAsync(cancellationToken);

        logger?.LogInformation(
            "Seeded demo library: {Books} books, {Copies} copies, {Borrowers} borrowers, {Loans} loans ({Open} still out)",
            books.Count, copies.Count, borrowers.Count, loans.Count, copiesStillOut.Count);

        return true;
    }
}
