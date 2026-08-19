using System;
using System.Collections.Generic;

namespace Bokmal.Database.Entities;

public partial class Book
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Author { get; set; } = null!;

    public string Genre { get; set; } = null!;

    public int PublishedYear { get; set; }

    public int PageCount { get; set; }

    public string Description { get; set; } = null!;

    public virtual ICollection<BookCopy> BookCopies { get; set; } = new List<BookCopy>();
}
