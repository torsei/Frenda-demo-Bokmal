using System;
using System.Collections.Generic;

namespace Bokmal.Database.Entities;

public partial class Borrower
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public DateTime JoinedAt { get; set; }

    public virtual ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
