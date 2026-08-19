namespace Bokmal.Database.Entities;

public partial class BookCopy
{
    /// <summary>
    /// Every loan this copy has ever been part of, at most one of which is unreturned.
    ///
    /// Hand-written because the generator cannot produce it. It reads the unique index on
    /// loan.book_copy_id without its "WHERE returned_at IS NULL" filter, concludes a copy
    /// has one loan for all time, and emits a single <c>Loan?</c> reference. That property
    /// is dropped in BokmalDbContextOverrides and replaced by this one.
    /// </summary>
    public ICollection<Loan> Loans { get; set; } = [];
}
