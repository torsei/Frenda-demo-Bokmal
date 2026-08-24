import { cookies } from 'next/headers';

/**
 * Who the browser is currently acting as.
 *
 * A cookie holding an email address, which is exactly as much security as the backend
 * offers: the API identifies borrowers from a header that nothing verifies. Deliberate --
 * the exercise asks for an app that knows who the borrower is, not for a login.
 *
 * The two flags are not decoration, and would stay on a real session:
 *
 *   httpOnly  keeps the value out of reach of JavaScript, so a cross-site scripting bug
 *             cannot read it. Worth having even here: XSS defeats every other defence on
 *             this list, because code running on your own origin passes any origin check
 *             and can read the responses too.
 *
 *   sameSite  'lax' means the browser will not attach this cookie to a cross-site POST, so
 *             a form on someone else's page cannot borrow a book as whoever is signed in.
 *             That is CSRF, and it is a real risk here in a way CORS is not -- no browser
 *             ever talks to the API, but every browser talks to this server.
 */
export const SESSION_COOKIE = 'bokmal_borrower';

export async function currentBorrowerEmail(): Promise<string | null> {
  const store = await cookies();
  return store.get(SESSION_COOKIE)?.value ?? null;
}

export async function setCurrentBorrowerEmail(email: string): Promise<void> {
  const store = await cookies();
  store.set(SESSION_COOKIE, email, {
    httpOnly: true,
    sameSite: 'lax',
    path: '/',
    maxAge: 60 * 60 * 24 * 30,
  });
}

export async function clearCurrentBorrower(): Promise<void> {
  const store = await cookies();
  store.delete(SESSION_COOKIE);
}
