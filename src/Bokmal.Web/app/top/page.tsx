import { api } from '@/lib/api';
import { BookCard } from '@/components/BookCard';

export default async function TopPage() {
  const client = await api();
  const { data } = await client.getApiBooksTop({ query: { limit: 10 } });

  const top = data ?? [];

  return (
    <div>
      <h1 className="text-2xl font-semibold tracking-tight">Most borrowed</h1>
      <p className="mt-1 text-muted">Counted over every loan the library has on record.</p>

      <ol className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {top.map(({ book, borrowCount }, index) => (
          <BookCard
            key={book.id}
            book={book}
            footer={
              <p className="text-xs text-muted">
                #{index + 1} · borrowed {borrowCount} times
              </p>
            }
          />
        ))}
      </ol>
    </div>
  );
}
