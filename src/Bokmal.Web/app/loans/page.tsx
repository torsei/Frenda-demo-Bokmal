import Link from 'next/link';
import { redirect } from 'next/navigation';
import { api } from '@/lib/api';
import { ReturnButton } from '@/components/LoanButtons';
import { currentBorrowerEmail } from '@/lib/session';
import { Mascot } from '@/components/Mascot';
import type { LoanDto } from '@/generated/api/types.gen';

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

function LoanRow({ loan, action }: { loan: LoanDto; action?: React.ReactNode }) {
  return (
    <li className="flex flex-wrap items-center gap-x-6 gap-y-3 border-b border-border py-4 last:border-b-0">
      <div className="min-w-56 flex-1">
        <Link href={`/books/${loan.bookSlug}`} className="font-medium hover:text-accent">
          {loan.bookTitle}
        </Link>
        <p className="text-sm text-muted">
          {loan.author} · copy {loan.copyNumber}
        </p>
      </div>

      <div className="text-sm">
        {loan.returnedAt ? (
          <span className="text-muted">
            {formatDate(loan.borrowedAt)} – {formatDate(loan.returnedAt)}
          </span>
        ) : (
          <span className={loan.isOverdue ? 'font-medium text-unavailable' : 'text-muted'}>
            {loan.isOverdue ? 'Overdue since ' : 'Due '}
            {formatDate(loan.dueAt)}
          </span>
        )}
      </div>

      {action}
    </li>
  );
}

export default async function MyLoansPage() {
  if ((await currentBorrowerEmail()) === null) redirect('/sign-in');

  const client = await api();
  const { data } = await client.getApiLoansMe();

  const current = data?.current ?? [];
  const past = data?.past ?? [];

  return (
    <div>
      <h1 className="text-2xl font-semibold tracking-tight">My loans</h1>

      <section className="mt-8">
        <h2 className="text-lg font-medium">Out now</h2>

        {current.length === 0 ? (
          <div className="mt-3 flex items-center gap-4 rounded-lg border border-border bg-surface p-5">
            <Mascot size={96} className="h-24 w-24 shrink-0 object-contain" />
            <p className="text-muted">
              Nothing out at the moment.{' '}
              <Link href="/books" className="text-accent hover:underline">
                Find something to read
              </Link>
              .
            </p>
          </div>
        ) : (
          <ul className="mt-3 rounded-lg border border-border bg-surface px-5">
            {current.map((loan) => (
              <LoanRow key={loan.id} loan={loan} action={<ReturnButton loanId={loan.id} />} />
            ))}
          </ul>
        )}
      </section>

      <section className="mt-12">
        <h2 className="text-lg font-medium">Previously</h2>

        {past.length === 0 ? (
          <p className="mt-3 text-muted">No finished loans yet.</p>
        ) : (
          <ul className="mt-3 rounded-lg border border-border bg-surface px-5">
            {past.map((loan) => (
              <LoanRow key={loan.id} loan={loan} />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
