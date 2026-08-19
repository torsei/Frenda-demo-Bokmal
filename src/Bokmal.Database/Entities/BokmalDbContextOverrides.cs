using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bokmal.Database;

/// <summary>
/// Corrections applied on top of the generated model.
///
/// Generated/ is produced by <c>dotnet ef dbcontext scaffold</c> straight from the SQL
/// schema and must never be edited by hand -- it is overwritten every time the schema
/// changes. Anything the generator cannot express belongs here instead, so that
/// regenerating stays a one-command operation.
/// </summary>
public partial class BokmalDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // The generator reads ux_loan_active_book_copy_id -- a UNIQUE index filtered on
        // "WHERE returned_at IS NULL" -- as an unfiltered unique constraint on the
        // foreign key, and therefore concludes that a copy has at most one loan ever.
        // It has at most one *active* loan, and an unbounded history behind it, which is
        // what the whole top list and reading-time estimate are built on.
        //
        // Reconfigure the relation as many-to-one and drop the navigation the generator
        // invented. Pinned by GeneratedModelTests so a future regeneration cannot quietly
        // reintroduce it.
        // Order matters: EF refuses to change the multiplicity while the navigation the
        // generator invented is still part of the relationship, so drop it first.
        modelBuilder.Entity<BookCopy>().Ignore(c => c.Loan);

        modelBuilder.Entity<Loan>()
            .HasOne(l => l.BookCopy)
            .WithMany(c => c.Loans)
            .HasForeignKey(l => l.BookCopyId);

        ForceTimestampsToUtc(modelBuilder);

        // Every key is a GUID the application has already assigned -- v7 at runtime,
        // literals in the seed scripts. Nothing is generated on insert.
        modelBuilder.Entity<Book>().Property(b => b.Id).ValueGeneratedNever();
        modelBuilder.Entity<BookCopy>().Property(c => c.Id).ValueGeneratedNever();
        modelBuilder.Entity<Borrower>().Property(b => b.Id).ValueGeneratedNever();
        modelBuilder.Entity<Loan>().Property(l => l.Id).ValueGeneratedNever();
    }

    /// <summary>
    /// Every timestamp in Bokmal is UTC, and this makes the type system agree.
    ///
    /// Timestamps are stored as DateTime rather than DateTimeOffset because EF Core cannot
    /// translate ORDER BY over a DateTimeOffset on SQLite -- "my loans, newest first" throws
    /// outright. The offset carried no information anyway, since everything written here is
    /// already UTC.
    ///
    /// The catch is that SQLite reads a DateTime back with Kind=Unspecified. That is not a
    /// cosmetic detail: System.Text.Json serialises an Unspecified time without the trailing
    /// Z, so the browser would parse every due date as local time and quietly shift it by
    /// the user's offset. Pinning the kind on the way out fixes the JSON as much as the C#.
    /// </summary>
    private static void ForceTimestampsToUtc(ModelBuilder modelBuilder)
    {
        var toUtc = new ValueConverter<DateTime, DateTime>(
            value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        var toUtcNullable = new ValueConverter<DateTime?, DateTime?>(
            value => value.HasValue
                ? (value.Value.Kind == DateTimeKind.Utc ? value : value.Value.ToUniversalTime())
                : null,
            value => value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : null);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(toUtc);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(toUtcNullable);
            }
        }
    }
}
