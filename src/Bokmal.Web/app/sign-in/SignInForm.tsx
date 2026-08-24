'use client';

import { useState, useTransition } from 'react';
import { signIn } from '@/lib/actions';

export function SignInForm() {
  const [isPending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);

  return (
    <form
      className="mt-6 flex flex-col gap-3"
      action={(formData) =>
        startTransition(async () => {
          setError(null);
          const result = await signIn(String(formData.get('email') ?? ''));
          // A successful sign-in redirects, so anything returned here is a failure.
          if (result && 'error' in result) setError(result.error);
        })
      }
    >
      <input
        type="email"
        name="email"
        required
        autoComplete="email"
        placeholder="name@example.se"
        className="rounded-md border border-border bg-surface px-3 py-2 text-sm"
      />

      <button
        type="submit"
        disabled={isPending}
        className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50"
      >
        {isPending ? 'Signing in…' : 'Sign in'}
      </button>

      {error ? <p className="text-sm text-unavailable">{error}</p> : null}
    </form>
  );
}
