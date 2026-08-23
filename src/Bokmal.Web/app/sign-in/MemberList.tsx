'use client';

import { useState, useTransition } from 'react';
import { signIn } from '@/lib/actions';
import { CopyEmailButton } from './CopyEmailButton';
import type { BorrowerDto } from '@/generated/api/types.gen';

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
 */
export function MemberList({ members }: { members: BorrowerDto[] }) {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [signingInAs, setSigningInAs] = useState<string | null>(null);

  return (
    <div>
      <h2 className="text-sm font-medium text-muted">Members you can sign in as</h2>

      <ul className="mt-2">
        {members.map((member) => (
          <li key={member.id} className="flex items-center gap-1">
            <button
              type="button"
              disabled={isPending}
              onClick={() => {
                setSigningInAs(member.email);
                startTransition(async () => {
                  setError(null);
                  const formData = new FormData();
                  formData.set('email', member.email);
                  const result = await signIn(formData);
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

      {error ? <p className="mt-2 px-2 text-sm text-unavailable">{error}</p> : null}
    </div>
  );
}
