import { api } from '@/lib/api';
import { currentBorrowerEmail } from '@/lib/session';
import { BookCard } from './BookCard';

/**
 * A personal shelf at the top of the catalogue.
 *
 * Renders nothing at all for a visitor who is not signed in, and nothing for a member who
 * has not finished a book yet — there is no honest basis for a suggestion before that, and
 * an empty "For you" heading is worse than no heading.
 *
 * Each group names the book that prompted it. That is the difference between a suggestion
 * the reader can weigh and one they have to take on faith.
 */
export async function ForYou() {
  if ((await currentBorrowerEmail()) === null) return null;

  const client = await api();
  const { data } = await client.getApiBooksForMe({ query: { groups: 2, perGroup: 3 } });

  const groups = data ?? [];
  if (groups.length === 0) return null;

  return (
    <section className="mb-12">
      <h2 className="text-2xl font-semibold tracking-tight">For you</h2>

      {groups.map((group) => (
        <div key={group.basedOn.id} className="mt-6">
          <p className="text-sm text-muted">
            Because you read <span className="text-foreground">{group.basedOn.title}</span>
          </p>

          <ul className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {group.books.map(({ book, sharedBorrowers }) => (
              <BookCard
                key={book.id}
                book={book}
                footer={
                  <p className="text-xs text-muted">
                    {sharedBorrowers} readers borrowed both
                  </p>
                }
              />
            ))}
          </ul>
        </div>
      ))}

      <hr className="mt-12 border-border" />
    </section>
  );
}
