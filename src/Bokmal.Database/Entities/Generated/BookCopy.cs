using System;
using System.Collections.Generic;

namespace Bokmal.Database.Entities;

public partial class BookCopy
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    public int CopyNumber { get; set; }

    public string Status { get; set; } = null!;

    public virtual Book Book { get; set; } = null!;

    public virtual Loan? Loan { get; set; }
}
