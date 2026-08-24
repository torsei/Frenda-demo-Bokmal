import Link from 'next/link';
import { notFound } from 'next/navigation';
import { api } from '@/lib/api';
import { AvailabilityLine, BookCard, ReadingTimeLine } from '@/components/BookCard';
import { BookCover } from '@/components/BookCover';
import { BorrowButton } from '@/components/LoanButtons';
import { currentBorrowerEmail } from '@/lib/session';

export default async function BookPage({ params }: PageProps<'/books/[slug]'>) {
  const { slug } = await params;

  const client = await api();
  const { data: book } = await client.getApiBooksBySlug({ path: { slug } });

  if (!book) notFound();

  const signedIn = (await currentBorrowerEmail()) !== null;

  return (
    <article>
      <Link href="/books" className="text-sm text-muted hover:text-foreground">
        ← Back to the shelves
      </Link>

      <div className="mt-4 flex flex-col gap-8 sm:flex-row">
        <BookCover
          title={book.title}
          author={book.author}
          genre={book.genre}
          slug={book.slug}
          className="h-72 w-48 shrink-0 self-start rounded shadow-md"
        />

        <div>
          <p className="text-xs uppercase tracking-wide text-muted">{book.genre}</p>
          <h1 className="mt-1 text-3xl font-semibold tracking-tight">{book.title}</h1>
          <p className="mt-1 text-muted">
            {book.author} · {book.publishedYear} · {book.pageCount} pages
          </p>

          <p className="mt-6 max-w-2xl leading-relaxed">{book.description}</p>
        </div>
      </div>

      <div className="mt-8 flex flex-col gap-4 rounded-lg border border-border bg-surface p-5 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-col gap-1 text-sm">
          <AvailabilityLine availability={book.availability} />
          <ReadingTimeLine readingTime={book.readingTime} />
        </div>

        {signedIn ? (
          <BorrowButton bookId={book.id} disabled={!book.availability.isAvailable} />
        ) : (
          <Link
            href="/sign-in"
            className="rounded-md bg-accent px-4 py-2 text-center text-sm font-medium text-white hover:opacity-90"
          >
            Sign in to borrow
          </Link>
        )}
      </div>

      {book.alsoBorrowed.length > 0 ? (
        <section className="mt-12">
          <h2 className="text-lg font-semibold tracking-tight">
            Readers of this also borrowed
          </h2>
          <p className="mt-1 text-sm text-muted">
            Titles this book&rsquo;s readers reach for more often than the library at large
            does.
          </p>

          <ul className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {book.alsoBorrowed.map(({ book: recommended, sharedBorrowers }) => (
              <BookCard
                key={recommended.id}
                book={recommended}
                footer={
                  <p className="text-xs text-muted">
                    {sharedBorrowers} readers borrowed both
                  </p>
                }
              />
            ))}
          </ul>
        </section>
      ) : null}
    </article>
  );
}
