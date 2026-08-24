'use client';

import { useState, useTransition } from 'react';
import { borrow, returnLoan } from '@/lib/actions';

/**
 * The two buttons that change anything.
 *
 * Both deliberately avoid optimistic updates. Whether a copy is free is decided by the
 * server at the moment of the click -- another borrower can take the last one a second
 * earlier -- so showing "borrowed" before the server agrees would be showing something that
 * may be false. The wait is short and the answer is true.
 */
function ActionButton({
  label,
  pendingLabel,
  action,
}: {
  label: string;
  pendingLabel: string;
  action: () => Promise<{ error: string } | { ok: true }>;
}) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  return (
    <div>
      <button
        type="button"
        disabled={isPending}
        onClick={() =>
          startTransition(async () => {
            setError(null);
            const result = await action();
            if ('error' in result) setError(result.error);
          })
        }
        className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white transition hover:opacity-90 disabled:opacity-50"
      >
        {isPending ? pendingLabel : label}
      </button>

      {error ? <p className="mt-2 text-sm text-unavailable">{error}</p> : null}
    </div>
  );
}

export function BorrowButton({ bookId, disabled }: { bookId: string; disabled?: boolean }) {
  if (disabled) {
    return (
      <button
        type="button"
        disabled
        className="rounded-md border border-border px-4 py-2 text-sm text-muted"
      >
        No copies on the shelf
      </button>
    );
  }

  return <ActionButton label="Borrow" pendingLabel="Borrowing…" action={() => borrow(bookId)} />;
}

export function ReturnButton({ loanId }: { loanId: string }) {
  return (
    <ActionButton label="Return" pendingLabel="Returning…" action={() => returnLoan(loanId)} />
  );
}
