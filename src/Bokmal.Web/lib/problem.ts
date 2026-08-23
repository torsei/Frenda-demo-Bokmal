/**
 * Pulls the human-readable sentence out of an API error.
 *
 * A failed borrow is not an exception. Every way it can fail -- the last copy went, you
 * already have this one, you are at your limit -- is something the borrower needs to read,
 * and the API already phrases it well in the problem details. Inventing a generic message
 * on top of a good one would be a downgrade, so the fallback is only for the cases where
 * there is genuinely nothing to pass on.
 */
export function problemMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object' && 'detail' in error) {
    const detail = (error as { detail?: unknown }).detail;
    if (typeof detail === 'string' && detail.trim().length > 0) return detail;
  }

  return fallback;
}
