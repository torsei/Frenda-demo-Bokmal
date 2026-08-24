'use client';

import { useState, useTransition } from 'react';
import { signIn } from '@/lib/actions';
import { CopyEmailButton } from './CopyEmailButton';
import type { BorrowerDto } from '@/generated/api/types.gen';

/** Enough to choose from without turning the page into a directory. */
const INITIALLY_SHOWN = 8;

/**
 * The members you can sign in as.
 *
 * Two ways in, because they serve different purposes. Clicking a row signs you in as that
 * member, which is the one-click path for looking around. The copy button gets you the
 * address to paste somewhere else -- a second browser, say, which is how you watch two
 * borrowers race for the same last copy.
 *
 * Only possible at all because there are no passwords. See the note in the API's
 * SessionController: this stands in for a login, it is not one.
 *
 * Most of the list is folded away to begin with. Forty-five addresses is a directory, not a
 * choice, but the rest are one click away for anyone who wants a particular reader.
 */
export function MemberList({ members }: { members: BorrowerDto[] }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [signingInAs, setSigningInAs] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);

  const shown = showAll ? members : members.slice(0, INITIALLY_SHOWN);
  const hidden = members.length - shown.length;

  return (
    <div>
      <h2 className="text-sm font-medium text-muted">Members you can sign in as</h2>

      <ul className="mt-2">
        {shown.map((member) => (
          <li key={member.id} className="flex items-center gap-1">
            <button
              type="button"
              disabled={isPending}
              onClick={() => {
                setSigningInAs(member.email);
                startTransition(async () => {
                  setError(null);
                  const result = await signIn(member.email);
                  // A successful sign-in redirects, so reaching here means it failed.
                  if (result && 'error' in result) setError(result.error);
                  setSigningInAs(null);
                });
              }}
              className="flex flex-1 items-center justify-between gap-4 rounded px-2 py-1.5 text-left text-sm transition hover:bg-accent-soft disabled:opacity-50"
            >
              <span>{member.displayName}</span>
              <span className="text-muted">
                {signingInAs === member.email ? 'Signing in…' : member.email}
              </span>
            </button>

            <CopyEmailButton email={member.email} />
          </li>
        ))}
      </ul>

      {hidden > 0 ? (
        <button
          type="button"
          onClick={() => setShowAll(true)}
          className="mt-1 px-2 text-xs text-muted underline underline-offset-2 hover:text-foreground"
        >
          &hellip;and {hidden} more
        </button>
      ) : null}

      {error ? <p className="mt-2 px-2 text-sm text-unavailable">{error}</p> : null}
    </div>
  );
}
