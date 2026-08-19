import Link from 'next/link';
import { api } from '@/lib/api';
import { BookCard } from '@/components/BookCard';
import { Mascot } from '@/components/Mascot';

export default async function CataloguePage({ searchParams }: PageProps<'/'>) {
  const params = await searchParams;
  const search = typeof params.search === 'string' ? params.search : undefined;
  const genre = typeof params.genre === 'string' ? params.genre : undefined;

  const client = await api();
  const [books, genres] = await Promise.all([
    client.getApiBooks({ query: { search, genre } }),
    client.getApiBooksGenres(),
  ]);

  const results = books.data ?? [];
  const allGenres = genres.data ?? [];

  return (
    <div>
      <h1 className="text-2xl font-semibold tracking-tight">The shelves</h1>
      <p className="mt-1 text-muted">
        {results.length} {results.length === 1 ? 'book' : 'books'}
        {search ? ` matching “${search}”` : ''}
        {genre ? ` in ${genre}` : ''}
      </p>

      <form className="mt-6 flex flex-wrap gap-2">
        <input
          type="search"
          name="search"
          defaultValue={search ?? ''}
          placeholder="Title or author"
          className="min-w-56 flex-1 rounded-md border border-border bg-surface px-3 py-2 text-sm"
        />
        <select
          name="genre"
          defaultValue={genre ?? ''}
          className="rounded-md border border-border bg-surface px-3 py-2 text-sm"
        >
          <option value="">Every genre</option>
          {allGenres.map((g) => (
            <option key={g} value={g}>
              {g}
            </option>
          ))}
        </select>
        <button
          type="submit"
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white hover:opacity-90"
        >
          Search
        </button>
        {search || genre ? (
          <Link
            href="/"
            className="rounded-md border border-border px-4 py-2 text-sm text-muted hover:text-foreground"
          >
            Clear
          </Link>
        ) : null}
      </form>

      {results.length === 0 ? (
        <div className="mt-10 flex items-center gap-4">
          <Mascot size={96} className="h-24 w-24 shrink-0 object-contain" />
          <p className="text-muted">Nothing here matches. Try a different search.</p>
        </div>
      ) : (
        <ul className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {results.map((book) => (
            <BookCard key={book.id} book={book} />
          ))}
        </ul>
      )}
    </div>
  );
}
