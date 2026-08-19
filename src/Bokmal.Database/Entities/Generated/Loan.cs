using System;
using System.Collections.Generic;

namespace Bokmal.Database.Entities;

public partial class Loan
{
    public Guid Id { get; set; }

    public Guid BookCopyId { get; set; }

    public Guid BorrowerId { get; set; }

    public DateTime BorrowedAt { get; set; }

    public DateTime DueAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public virtual BookCopy BookCopy { get; set; } = null!;

    public virtual Borrower Borrower { get; set; } = null!;
}
