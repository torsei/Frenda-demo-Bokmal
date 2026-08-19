import Link from 'next/link';
import type { AvailabilityDto, BookSummaryDto, ReadingTimeDto } from '@/generated/api/types.gen';
import { BookCover } from './BookCover';

export function AvailabilityLine({ availability }: { availability: AvailabilityDto }) {
  const { availableCopies, totalCopies, onLoanCopies, isAvailable } = availability;

  return (
    <span className={isAvailable ? 'text-available' : 'text-unavailable'}>
      {isAvailable
        ? `${availableCopies} of ${totalCopies} on the shelf`
        : `All ${totalCopies} ${totalCopies === 1 ? 'copy' : 'copies'} out`}
      {isAvailable && onLoanCopies > 0 ? ` · ${onLoanCopies} out` : ''}
    </span>
  );
}

/**
 * The reading-time estimate, phrased according to how much it is worth.
 *
 * The API sends the number of loans behind the figure precisely so this can be honest about
 * it: a median over thirty borrowers is worth stating plainly, while a page-count guess for
 * a book nobody has finished is not, and showing both the same way would be a small lie.
 */
export function ReadingTimeLine({ readingTime }: { readingTime: ReadingTimeDto }) {
  const { typicalDays, basedOnLoans, fromHistory } = readingTime;

  if (!fromHistory) {
    return (
      <span className="text-muted">
        Roughly {typicalDays} {typicalDays === 1 ? 'day' : 'days'}, going by length
      </span>
    );
  }

  return (
    <span className="text-muted">
      Usually out {typicalDays} {typicalDays === 1 ? 'day' : 'days'}
      <span className="opacity-70"> · {basedOnLoans} readers</span>
    </span>
  );
}

export function BookCard({ book, footer }: { book: BookSummaryDto; footer?: React.ReactNode }) {
  return (
    <li className="flex flex-col overflow-hidden rounded-lg border border-border bg-surface">
      <Link href={`/books/${book.slug}`} className="group flex gap-4 p-4">
        <BookCover
          title={book.title}
          author={book.author}
          genre={book.genre}
          slug={book.slug}
          className="h-28 w-[75px] shrink-0 rounded-sm shadow-sm"
        />

        <div className="min-w-0">
          <h3 className="font-medium leading-snug group-hover:text-accent">{book.title}</h3>
          <p className="mt-0.5 text-sm text-muted">
            {book.author} · {book.publishedYear}
          </p>
          <p className="mt-2 text-xs uppercase tracking-wide text-muted opacity-80">
            {book.genre}
          </p>
        </div>
      </Link>

      <div className="mt-auto flex flex-col gap-1 border-t border-border px-4 py-3 text-sm">
        <AvailabilityLine availability={book.availability} />
        <ReadingTimeLine readingTime={book.readingTime} />
        {footer ? <div className="mt-1">{footer}</div> : null}
      </div>
    </li>
  );
}
