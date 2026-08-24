import { redirect } from 'next/navigation';

/**
 * The catalogue lives at /books, not at the root.
 *
 * Every page in the app is about something the URL can name -- a book, your loans, the top
 * list -- and the shelves deserve the same. It also means signing in lands you somewhere
 * that says where you are, rather than back at a bare slash.
 */
export default function Home() {
  redirect('/books');
}
