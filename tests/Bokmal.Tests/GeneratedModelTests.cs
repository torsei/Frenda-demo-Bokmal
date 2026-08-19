using Bokmal.Database;
using Bokmal.Database.Entities;
using Bokmal.Tests.Databases;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Tests;

/// <summary>
/// Guards the two things about the generated model that a regeneration could silently
/// get wrong. These are not tests of EF -- they are tests of the seam between the SQL
/// schema and the code generated from it.
/// </summary>
public class GeneratedModelTests : IDisposable
{
    private readonly TestDatabase _db = TestDatabases.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void A_copy_relates_to_many_loans_not_one()
    {
        // The generator reads the filtered unique index as a global one and infers a
        // one-to-one relation. If this assertion ever fails, the correction in
        // BokmalDbContextOverrides was lost and loan history is quietly broken.
        using var context = _db.CreateContext();

        var foreignKey = context.Model
            .FindEntityType(typeof(Loan))!
            .GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(BookCopy));

        Assert.False(foreignKey.IsUnique);
    }

    [Fact]
    public async Task A_copy_can_carry_a_history_of_returned_loans()
    {
        using var context = _db.CreateContext();

        var book = new Book
        {
            Id = BokmalId.New(),
            Slug = "history-probe",
            Title = "History Probe",
            Author = "A. Author",
            Genre = "Fiction",
            PublishedYear = 2020,
            PageCount = 300,
            Description = "Used to prove a copy accumulates loans."
        };

        var copy = new BookCopy
        {
            Id = BokmalId.New(),
            BookId = book.Id,
            CopyNumber = 1,
            Status = CopyStatuses.Available
        };

        var borrower = new Borrower
        {
            Id = BokmalId.New(),
            Email = "probe@bokmal.test",
            DisplayName = "Probe",
            JoinedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        context.AddRange(book, copy, borrower);

        // Two finished loans and one still out -- the shape the filtered index has to allow.
        for (var month = 1; month <= 2; month++)
        {
            context.Add(new Loan
            {
                Id = BokmalId.New(),
                BookCopyId = copy.Id,
                BorrowerId = borrower.Id,
                BorrowedAt = new DateTime(2025, month, 1, 0, 0, 0, DateTimeKind.Utc),
                DueAt = new DateTime(2025, month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(28),
                ReturnedAt = new DateTime(2025, month, 20, 0, 0, 0, DateTimeKind.Utc)
            });
        }

        context.Add(new Loan
        {
            Id = BokmalId.New(),
            BookCopyId = copy.Id,
            BorrowerId = borrower.Id,
            BorrowedAt = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            DueAt = new DateTime(2025, 8, 29, 0, 0, 0, DateTimeKind.Utc),
            ReturnedAt = null
        });

        await context.SaveChangesAsync();

        using var reader = _db.CreateContext();
        var loans = await reader.Loans.Where(l => l.BookCopyId == copy.Id).ToListAsync();

        Assert.Equal(3, loans.Count);
        Assert.Single(loans, l => l.ReturnedAt == null);

        // Round-trips the two CLR types that only survive because the schema declares
        // 'uuid' and 'datetimeoffset' rather than plain TEXT.
        var active = loans.Single(l => l.ReturnedAt == null);
        Assert.Equal(copy.Id, active.BookCopyId);
        Assert.Equal(new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc), active.BorrowedAt);
    }
}
