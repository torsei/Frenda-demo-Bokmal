import type { Metadata } from 'next';
import { Geist } from 'next/font/google';
import Link from 'next/link';
import './globals.css';
import { api } from '@/lib/api';
import { signOut } from '@/lib/actions';
import { Mascot } from '@/components/Mascot';

const geistSans = Geist({ variable: '--font-geist-sans', subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'Bokmal',
  description: 'Borrow books from the library.',
};

/**
 * Reads the session on the server for every page, which is cheap because it is one request
 * to an API that is already being called, and means no flash of a signed-out header.
 */
async function currentBorrower() {
  const client = await api();
  const { data } = await client.getApiSession();
  return data ?? null;
}

export default async function RootLayout({ children }: LayoutProps<'/'>) {
  const borrower = await currentBorrower();

  return (
    <html lang="en" className={`${geistSans.variable} h-full antialiased`}>
      <body className="flex min-h-full flex-col font-sans">
        <header className="border-b border-border bg-surface">
          <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-x-6 gap-y-3 px-6 py-4">
            <Link href="/books" className="flex items-center gap-2 text-lg font-semibold tracking-tight">
              <Mascot size={34} className="h-9 w-9 object-contain" priority />
              Bokmal
            </Link>

            <nav className="flex items-center gap-5 text-sm text-muted">
              <Link href="/books" className="hover:text-foreground">
                Catalogue
              </Link>
              <Link href="/top" className="hover:text-foreground">
                Most borrowed
              </Link>
              <Link href="/loans" className="hover:text-foreground">
                My loans
              </Link>
            </nav>

            <div className="ml-auto flex items-center gap-3 text-sm">
              {borrower ? (
                <>
                  <span className="text-muted">{borrower.displayName}</span>
                  <form action={signOut}>
                    <button type="submit" className="text-muted underline hover:text-foreground">
                      Sign out
                    </button>
                  </form>
                </>
              ) : (
                <Link href="/sign-in" className="font-medium text-accent hover:underline">
                  Sign in
                </Link>
              )}
            </div>
          </div>
        </header>

        <main className="mx-auto w-full max-w-5xl flex-1 px-6 py-10">{children}</main>

        <footer className="border-t border-border px-6 py-6 text-center text-xs text-muted">
          Bokmal — a small lending library.
        </footer>
      </body>
    </html>
  );
}
