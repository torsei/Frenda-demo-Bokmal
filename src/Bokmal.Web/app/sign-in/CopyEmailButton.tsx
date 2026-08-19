'use client';

import { useEffect, useState } from 'react';

function CopyIcon() {
  return (
    <svg viewBox="0 0 16 16" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="1.4">
      <rect x="5.5" y="5.5" width="8" height="8" rx="1.5" />
      <path d="M10.5 3.5v-1a1 1 0 0 0-1-1h-7a1 1 0 0 0-1 1v7a1 1 0 0 0 1 1h1" />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 16 16" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M3 8.5l3.5 3.5L13 5" />
    </svg>
  );
}

/**
 * Copies a member's address to the clipboard.
 *
 * The addresses are the only way in and nobody is going to type
 * "gabriella.roos@example.se" by hand to try the app as a second borrower.
 */
export function CopyEmailButton({ email }: { email: string }) {
  const [copied, setCopied] = useState(false);

  // Reverts the button after a moment, and cancels the timer if the component goes away
  // first so it cannot set state on something unmounted.
  useEffect(() => {
    if (!copied) return;

    const timer = setTimeout(() => setCopied(false), 1500);
    return () => clearTimeout(timer);
  }, [copied]);

  return (
    <button
      type="button"
      onClick={async () => {
        try {
          await navigator.clipboard.writeText(email);
          setCopied(true);
        } catch {
          // Clipboard access can be refused. Selecting the text still works, so there is
          // nothing useful to say and an error message would only be in the way.
        }
      }}
      aria-label={copied ? `Copied ${email}` : `Copy ${email}`}
      className="rounded p-1 text-muted transition hover:bg-accent-soft hover:text-foreground"
    >
      {copied ? <CheckIcon /> : <CopyIcon />}
    </button>
  );
}
