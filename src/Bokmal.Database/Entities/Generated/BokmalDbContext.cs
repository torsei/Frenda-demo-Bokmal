using System;
using System.Collections.Generic;
using Bokmal.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bokmal.Database;

public partial class BokmalDbContext : DbContext
{
    public BokmalDbContext(DbContextOptions<BokmalDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Book> Books { get; set; }

    public virtual DbSet<BookCopy> BookCopies { get; set; }

    public virtual DbSet<Borrower> Borrowers { get; set; }

    public virtual DbSet<Loan> Loans { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("book");

            entity.HasIndex(e => e.Slug, "ux_book_slug").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("uuid")
                .HasColumnName("id");
            entity.Property(e => e.Author).HasColumnName("author");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Genre).HasColumnName("genre");
            entity.Property(e => e.PageCount).HasColumnName("page_count");
            entity.Property(e => e.PublishedYear).HasColumnName("published_year");
            entity.Property(e => e.Slug).HasColumnName("slug");
            entity.Property(e => e.Title).HasColumnName("title");
        });

        modelBuilder.Entity<BookCopy>(entity =>
        {
            entity.ToTable("book_copy");

            entity.HasIndex(e => e.BookId, "ix_book_copy_book_id");

            entity.HasIndex(e => new { e.BookId, e.CopyNumber }, "ux_book_copy_book_id_copy_number").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("uuid")
                .HasColumnName("id");
            entity.Property(e => e.BookId)
                .HasColumnType("uuid")
                .HasColumnName("book_id");
            entity.Property(e => e.CopyNumber).HasColumnName("copy_number");
            entity.Property(e => e.Status).HasColumnName("status");

            entity.HasOne(d => d.Book).WithMany(p => p.BookCopies)
                .HasForeignKey(d => d.BookId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Borrower>(entity =>
        {
            entity.ToTable("borrower");

            entity.HasIndex(e => e.Email, "ux_borrower_email").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("uuid")
                .HasColumnName("id");
            entity.Property(e => e.DisplayName).HasColumnName("display_name");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.JoinedAt)
                .HasColumnType("datetime")
                .HasColumnName("joined_at");
        });

        modelBuilder.Entity<Loan>(entity =>
        {
            entity.ToTable("loan");

            entity.HasIndex(e => e.BookCopyId, "ix_loan_book_copy_id");

            entity.HasIndex(e => e.BorrowerId, "ix_loan_borrower_id");

            entity.HasIndex(e => e.BookCopyId, "ux_loan_active_book_copy_id").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("uuid")
                .HasColumnName("id");
            entity.Property(e => e.BookCopyId)
                .HasColumnType("uuid")
                .HasColumnName("book_copy_id");
            entity.Property(e => e.BorrowedAt)
                .HasColumnType("datetime")
                .HasColumnName("borrowed_at");
            entity.Property(e => e.BorrowerId)
                .HasColumnType("uuid")
                .HasColumnName("borrower_id");
            entity.Property(e => e.DueAt)
                .HasColumnType("datetime")
                .HasColumnName("due_at");
            entity.Property(e => e.ReturnedAt)
                .HasColumnType("datetime")
                .HasColumnName("returned_at");

            entity.HasOne(d => d.BookCopy).WithOne(p => p.Loan)
                .HasForeignKey<Loan>(d => d.BookCopyId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Borrower).WithMany(p => p.Loans)
                .HasForeignKey(d => d.BorrowerId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
