# Bokmal

A small lending library, seen from the borrower's side. Browse the shelves, see what is
actually available right now, borrow a copy, give it back, and find the next thing to read.

*Bokmal* is Swedish for bookworm.

---

## Running it

### You need

| | Version | For |
|---|---|---|
| **.NET SDK** | **10.0** | everything. `global.json` pins it, so an older SDK fails immediately with a clear message instead of something cryptic further in |
| **Node** | **20.9+** | the frontend only. Next.js 16's own requirement |
| Docker | any | optional. Only to run the test suite against PostgreSQL |

The backend needs no Node and the frontend needs no .NET. They are separate applications
that speak HTTP, and the solution is arranged so you can work on either without the other's
toolchain installed.

### Then, in two terminals

```bash
# 1 — the API.
#     Creates bokmal.db, migrates it, and fills it with a demo library on first run.
dotnet run --project src/Bokmal.Api
```

```bash
# 2 — the frontend. The npm install is required; nothing else is.
cd src/Bokmal.Web
npm install
npm run dev
```

Open **http://localhost:3000**.

The API listens on `http://localhost:5080` and publishes its OpenAPI document at
`/openapi/v1.json`. Point the frontend elsewhere with `BOKMAL_API_URL` if you need to; see
`.env.example`.

Nothing else to configure. The database is a file, created on first run. Delete
`src/Bokmal.Api/bokmal.db` for a clean library.

> **Opening the solution.** `Bokmal.Web` appears in Solution Explorer so the frontend can be
> read alongside the backend, but it is **excluded from the solution build** — `next build`
> is npm's job, not MSBuild's. Building or running the frontend is done with npm, from
> VS Code or a terminal. `Bokmal.slnx` requires Visual Studio 17.13 or later.

**Signing in.** There are no passwords — see [Identity](#identity-is-not-authentication)
below. The sign-in page lists members you can sign in as, with a copy button next to each
address. A few with different reading habits:

| | |
|---|---|
| `astrid.lindqvist@example.se` | crime; several loans out and a long history |
| `bjorn.ek@example.se` | science fiction and non-fiction |
| `cecilia.nordin@example.se` | literary fiction and classics |

---

## Layout

```
src/
  Bokmal.Database/     schema, entities, seeding, database engines
  Bokmal.Api/          controllers, services, the loan flow
  Bokmal.Web/          Next.js frontend
tests/
  Bokmal.Tests/        60 tests, runnable against either database engine
```

`Bokmal.Web` is in the solution as a JavaScript project so it can be browsed from Solution
Explorer, with the solution build turned off for it. The reasoning is in `Bokmal.slnx`.

---

## How it is put together

### Books and copies

A `Book` is a title. A `BookCopy` is a physical object on a shelf. A `Loan` is of a **copy**,
never of a book.

Everything else follows from that. Availability is a count of copies, not a flag on the
title. Two people can read the same book at once if the library owns two copies. And a copy
accumulates a history of loans, which is what the top list, the recommendations and the
reading-time estimate are all computed from.

### The borrow flow

This is the part worth reading: `src/Bokmal.Api/Services/LoanService.cs`.

Handing out a copy looks like a two-step operation — check whether one is free, then take it
— and written that way it is a race. Under the isolation level everything defaults to, two
requests both read the same free copy before either writes, and both proceed. Wrapping it in
a transaction does not fix that: a transaction makes the write atomic, not the decision
exclusive.

So the decision is not trusted. The claim is a **conditional update** that repeats the
condition in its `WHERE` clause and reports how many rows it actually changed:

```csharp
var claimed = await context.BookCopies
    .Where(c => c.Id == copyId && c.Status == CopyStatuses.Available)
    .ExecuteUpdateAsync(u => u.SetProperty(c => c.Status, CopyStatuses.OnLoan), ct);

if (claimed == 0)
    continue;   // somebody else got this one; try the next copy
```

Zero rows means another request got there first, and we simply move to the next copy. Only
when the shelf is genuinely empty does the borrower get a refusal. This works identically on
every engine, unlike row locks, which are spelled differently everywhere and do not exist at
all in SQLite.

Behind it sits a **partial unique index** that permits only one unreturned loan per copy:

```sql
CREATE UNIQUE INDEX ux_loan_active_book_copy_id ON loan (book_copy_id)
    WHERE returned_at IS NULL;
```

Returned loans fall out of the index entirely, so history is unlimited. The borrow flow does
not depend on this index and never catches its violation — it is a backstop against a bug
somewhere else, and if it fires that is exactly what it should look like.

The division is deliberate: **application code may fail politely, the database may not fail
at all.**

#### This was measured, not assumed

Replacing the conditional update with a plain unconditional one — the naive "I already
checked it was free" version — and running the suite:

```
SQLite     Passed!  - Failed: 0, Passed: 8      ← hides the bug completely
Postgres   Failed!  - Failed: 1, Passed: 7      ← catches it
```

and the failure is precisely the backstop doing its job:

```
23505: duplicate key value violates unique constraint "ux_loan_active_book_copy_id"
```

SQLite allows one writer at a time, so the second request waits for the first to commit and
then reads a correct view of the shelf. The race cannot be produced there at all. That is
why the suite runs against PostgreSQL too, and why a green concurrency test on SQLite is
weaker evidence than it looks.

### Reading time

`ReadingTimeEstimator` reports the **median** of how long other borrowers kept the book, not
the mean. A handful of readers keep a book for months, and an average lets four of them drag
the estimate for everyone else up by a week.

Two things it is honest about. It measures loan length, not reading time — a book finished
in a weekend and returned three weeks later counts as three weeks. And the API returns
`basedOnLoans` alongside the figure so the interface can distinguish a median over thirty
readers from a page-count guess, rather than presenting both with equal confidence.

The median is computed in C#, not SQL. `PERCENTILE_CONT` exists in PostgreSQL and SQL Server
but not in SQLite, and all three spell date arithmetic differently. It reads two columns for
the books on screen. If the library grew until that mattered, the answer would be a
maintained statistic per book, not cleverer SQL.

### Recommendations

"Readers of this also borrowed…" is not a count of shared readers. Counting shared readers
recommends the library's bestsellers to everybody, because a book half the members have read
overlaps heavily with *everything*. The first version of this feature cheerfully suggested
*The Girl with the Dragon Tattoo* to readers of *Dune*.

So the overlap is weighed against how widely read the candidate is anyway. The question is
not "how many readers do these two share" but "are this book's readers **unusually** likely
to have read that one, compared with a member picked at random". A title everyone reads
scores no better than chance and drops out.

A floor on the shared count keeps a coincidence between three people from topping the list.

Titles the reader has already borrowed are dropped from the result — "find your next book"
has to mean *next*, and suggesting something already on their own bedside table is the one
way a recommendation can be both correct and useless. That filters the output only; their
loans still count towards the signal, because removing them from the statistics would make
the recommendations worse for everybody, themselves included.

---

## The database

### Schema in SQL, entities generated from it

The schema is owned by DbUp scripts in `src/Bokmal.Database/Scripts/`. EF Core does not
create it and has no migrations. A reviewer can read the whole schema as SQL without running
anything.

The entities in `Entities/Generated/` are produced from the database by
`dotnet ef dbcontext scaffold` and are **never edited by hand** — corrections live in
`Entities/BokmalDbContextOverrides.cs`, which sits in the generator's own partial hook and
survives regeneration.

There is one correction, and it matters. The generator cannot express a partial index, so it
reads `ux_loan_active_book_copy_id` as a plain unique constraint on the foreign key and
concludes a copy has at most one loan *ever* — which would silently erase loan history and
with it three of the five features. `GeneratedModelTests` fails if that correction is ever
lost.

Note also that the SQLite schema declares `uuid` and `datetime` rather than `TEXT`. SQLite
does not care, but the generator reads those names to recover `Guid` and `DateTime`; declared
as `TEXT` every key and timestamp comes back a `string`.

To regenerate after a schema change:

```bash
dotnet run --project src/Bokmal.Api -- --migrate-only

dotnet ef dbcontext scaffold "Data Source=../Bokmal.Api/bokmal.db" \
    Microsoft.EntityFrameworkCore.Sqlite \
    --project src/Bokmal.Database \
    --output-dir Entities/Generated --context-dir Entities/Generated \
    --context BokmalDbContext \
    --namespace Bokmal.Database.Entities --context-namespace Bokmal.Database \
    --table book --table book_copy --table borrower --table loan \
    --no-onconfiguring --force --no-build
```

`Data Source` is relative to `--project`, not to where you are standing. `--no-build` uses
the last built assembly, which is the way out if a half-finished scaffold leaves
`Entities/Generated` in a state that will not compile.

### Two engines

Everything engine-specific sits behind `IDatabaseEngine`: which DbUp builder, which EF
provider, which `Scripts/` folder, and how to open a write transaction. Nothing above that
interface mentions SQLite or PostgreSQL.

SQLite is the default because it makes the solution runnable with nothing installed.
PostgreSQL exists so the portability claim is checkable rather than merely stated — and, as
above, because it is the only place the borrow flow's concurrency can actually be exercised.

Adding an engine is one `IDatabaseEngine` implementation, one `Scripts/<Provider>/` folder
and one line in `DatabaseEngines`. Nothing in the domain, the services, the controllers or
the frontend changes.

The application code is provider-agnostic: **no raw SQL outside the migration scripts**, all
timestamps UTC, string comparisons normalised explicitly rather than relying on the database's
collation.

Timestamps are `DateTime` in UTC rather than `DateTimeOffset`, because EF Core cannot
translate `ORDER BY` over a `DateTimeOffset` on SQLite at all — "my loans, newest first"
simply throws. The offset carried no information anyway. A value converter pins `Kind` to
UTC on the way out, which also keeps the trailing `Z` in the JSON; without it the browser
would read every due date as local time.

### Demo data

Seeding is C#, not SQL scripts (`Seeding/DemoDataSeeder.cs`). Hand-written SQL has to agree
with how EF stores things and silently does not: EF writes GUIDs uppercase while SQLite
compares text case-sensitively, so a lowercase literal produces a row that exists but that no
lookup by id can find. Going through the model removes the whole class of defect.

The loan history is *invented* rather than authored, and `LoanHistoryBuilder` explains every
knob in terms of the feature it exists for — borrowers have reading tastes so co-borrowing
clusters, popular titles turn over faster so the top list has a real ranking, and a small
share of readers keep a book for months, which is why the estimate uses a median.

The invented history obeys the same rules the API enforces, so the app never starts in a
state a borrower could not have reached.

Seeding is off unless configuration asks for it, **and refused outright in Production
regardless** (`DemoDataPolicy`). Bokmal will never be deployed anywhere; the guard is there
because a seeder that writes hundreds of fake rows should not rely on nobody making a
mistake.

---

## Identity is not authentication

The API identifies the borrower from an `X-Borrower-Email` header. Nothing verifies it.
Anyone can borrow books as anyone else.

That is deliberate — the exercise asks for an app that knows who the current borrower is,
not for a login — and it is said out loud here rather than left to be discovered.

What it does buy is shape. Everything depends on `ICurrentBorrower`, nothing reads the header,
and the guard sits where `[Authorize]` would sit. Replacing this with a JWT or a cookie
session is one new class and one registration; no controller and no service changes.

`GET /api/borrowers` returns every member so the sign-in page has something to offer. In a
real system that is a directory of everyone who uses the library, and it goes the moment
authentication becomes real.

---

## Frontend

Next.js App Router. Server Components read, Server Actions write, `revalidatePath` refreshes
whatever showed a copy count.

The typed client in `generated/api` is generated from the API's OpenAPI document, so renaming
a field on a C# DTO becomes a TypeScript compile error rather than an `undefined` in a
component. Regenerate with `npm run generate-api` while the API is running.

`lib/api.ts` is marked `server-only`, and that is the architecture in one line: every call
happens on the Next server, never from the browser. The API therefore needs no CORS
configuration at all, and the borrower's identity travels from an httpOnly cookie into a
request header without passing through anything the user can reach.

The borrow and return buttons deliberately do not update optimistically. Whether a copy is
free is decided by the server at the moment of the click, and showing "borrowed" before the
server agrees would be showing something that may be false.

Book covers are generated as inline SVG from the title, author and genre. Real cover art is
copyrighted, and a cover API would add a network dependency that breaks the app offline.

---

## Tests

```bash
dotnet test                                    # SQLite — about a second
BOKMAL_TEST_PROVIDER=Postgres dotnet test      # PostgreSQL via Testcontainers — needs Docker

cd src/Bokmal.Web && npm test                  # the frontend
```

60 backend tests, green on both engines, and 18 on the frontend.

Every test corresponds to one rule, so a failure says which rule broke. What is deliberately
*not* tested: EF mappings, controller model binding, React components. Those test the
framework, not this domain.

Tests build their database by running the **migration scripts**, never `EnsureCreated`. If
they did the latter, EF would build the schema from its own model and every test would pass
against a database that does not exist in production. Going through the scripts means each
test also proves the generated model still matches the actual SQL.

The loan-flow tests build a three-book library rather than using the demo data: an assertion
about borrowing the last copy is only readable if you can see that the book has two copies
and one is already out.

| Suite | | |
|---|---|---|
| `ApiEndpointTests` | 16 | the HTTP layer: which outcome becomes which status code |
| `DiscoveryTests` | 9 | top list, recommendations, catalogue |
| `BorrowFlowTests` | 8 | the loan flow and its concurrency |
| `DemoDataPolicyTests` | 7 | production is never seeded |
| `ReturnFlowTests` | 6 | returning, and every way it can be wrong |
| `ReadingTimeEstimatorTests` | 6 | median, outliers, fallback |
| `DemoDataTests` | 6 | the demo library's invariants |
| `GeneratedModelTests` | 2 | the seam between schema and generated code |

`ApiEndpointTests` boots the real application in-process and talks to it over HTTP, so the
status codes, the borrower filter and the DTO shapes are covered rather than trusted. On the
frontend the tests cover the presentation logic that encodes a decision — the availability
wording, and the difference between stating a median and hedging a guess — and nothing else.
Coverage sits around 47% overall, which is what happens when the remainder is DTOs and
generated code; the figure worth having is that the loan flow's branches are covered.

---

## Conventions worth knowing

`Guid.NewGuid()`, `DateTime.UtcNow` and their neighbours are **compile errors** in this
solution (`BannedSymbols.txt`, raised to error in `.editorconfig`). Identifiers come from
`BokmalId.New()`, which produces a time-ordered UUID v7 rather than a random v4 that
fragments every index it lands in; `Guid.NewGuid()` cannot be reconfigured to do that because
it is contractually a v4 generator. Clocks come from an injected `TimeProvider`, so
time-dependent behaviour can be tested without waiting.

The error message points at the replacement, so hitting one tells you what to do instead.

---

## With more time

- **Search worth the name.** `LOWER(...) LIKE` is fine for two dozen books and wrong for
  twenty thousand. Real search wants an index built for it.
- **Reservations.** The most obvious missing feature: every copy is out, and there is nothing
  to do but check back.
- **Recommendations that cope with a cold start.** Co-borrowing needs history. A new title
  has none and is invisible until somebody finds it by browsing.
- **A statistics table for reading times**, if the loan table ever grew past what an in-memory
  median is comfortable with.
- **Real authentication**, which is one class — see above.
