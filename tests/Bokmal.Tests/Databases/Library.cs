using Bokmal.Api.Services;
using Bokmal.Database;
using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Bokmal.Tests.Databases;

/// <summary>
/// A tiny library, built to order.
///
/// The loan-flow tests use this rather than the demo data on purpose: an assertion about
/// borrowing the last copy is only readable if the reader can see that the book has exactly
/// two copies and one of them is already out. Several hundred invented loans would make
/// every test a puzzle.
/// </summary>
public sealed class Library : IDisposable
{
    private readonly TestDatabase _database;

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 5, 4, 9, 0, 0, TimeSpan.Zero));

    private Library(TestDatabase database) => _database = database;

    public static async Task<Library> WithAsync(Action<LibraryBuilder> configure)
    {
        var library = new Library(TestDatabases.Create());

        var builder = new LibraryBuilder();
        configure(builder);

        await using var context = library.CreateContext();
        await builder.WriteToAsync(context);

        return library;
    }

    public BokmalDbContext CreateContext() => _database.CreateContext();

    /// <summary>
    /// A service over its own context, because that is what a real request gets. Sharing one
    /// context between two concurrent borrows would share a change tracker and a connection,
    /// and the race being tested would not be the race that happens in production.
    /// </summary>
    public LoanService CreateLoanService(BokmalDbContext context)
        => new(context, _database.Engine, Clock, NullLogger<LoanService>.Instance);

    public ReadingTimeService CreateReadingTimeService(BokmalDbContext context) => new(context);

    public CatalogueService CreateCatalogueService(BokmalDbContext context)
        => new(context, CreateReadingTimeService(context));

    public DiscoveryService CreateDiscoveryService(BokmalDbContext context) => new(context);

    public async Task<Guid> BorrowerIdAsync(string email)
    {
        await using var context = CreateContext();
        return await context.Borrowers.Where(b => b.Email == email).Select(b => b.Id).SingleAsync();
    }

    public async Task<string> CopyStatusAsync(string slug, int copyNumber)
    {
        await using var context = CreateContext();
        return await context.BookCopies
            .Where(c => c.Book.Slug == slug && c.CopyNumber == copyNumber)
            .Select(c => c.Status)
            .SingleAsync();
    }

    public async Task<int> AvailableCopiesAsync(string slug)
    {
        await using var context = CreateContext();
        return await context.BookCopies
            .CountAsync(c => c.Book.Slug == slug && c.Status == CopyStatuses.Available);
    }

    public void Dispose() => _database.Dispose();
}

public sealed class LibraryBuilder
{
    private readonly List<Book> _books = [];
    private readonly List<BookCopy> _copies = [];
    private readonly List<Borrower> _borrowers = [];

    public LibraryBuilder Book(string slug, int copies, int pageCount = 300, string genre = "Fiction")
    {
        var book = new Book
        {
            Id = BokmalId.New(),
            Slug = slug,
            Title = slug,
            Author = "A. Author",
            Genre = genre,
            PublishedYear = 2000,
            PageCount = pageCount,
            Description = $"A book called {slug}."
        };

        _books.Add(book);

        for (var number = 1; number <= copies; number++)
        {
            _copies.Add(new BookCopy
            {
                Id = BokmalId.New(),
                BookId = book.Id,
                CopyNumber = number,
                Status = CopyStatuses.Available
            });
        }

        return this;
    }

    public LibraryBuilder Borrower(string email)
    {
        _borrowers.Add(new Borrower
        {
            Id = BokmalId.New(),
            Email = email,
            DisplayName = email,
            JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        return this;
    }

    internal async Task WriteToAsync(BokmalDbContext context)
    {
        context.AddRange(_books);
        context.AddRange(_copies);
        context.AddRange(_borrowers);
        await context.SaveChangesAsync();
    }
}
