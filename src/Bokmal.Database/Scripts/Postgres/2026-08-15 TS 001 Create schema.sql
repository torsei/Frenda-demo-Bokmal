-- Bokmal core schema, PostgreSQL.
--
-- The same schema as Scripts/Sqlite, which is the point: raw SQL is the one part of the
-- solution that cannot be written once and moved, so it is duplicated per engine rather
-- than hidden behind an abstraction that would only paper over the differences.
--
-- The differences are all in type names. PostgreSQL has a real uuid type and a real
-- timestamp type, where SQLite has neither and takes the names purely as a hint for the
-- entity generator. Everything else -- the partial unique index especially -- is identical.
--
-- Timestamps are timestamptz because every value written here is UTC and the application
-- pins DateTime.Kind to Utc on the way in and out.

CREATE TABLE borrower (
    id           uuid        NOT NULL PRIMARY KEY,
    email        text        NOT NULL,
    display_name text        NOT NULL,
    joined_at    timestamptz NOT NULL
);

-- Email is the stand-in for a real identity, so it has to be unique.
CREATE UNIQUE INDEX ux_borrower_email ON borrower (email);

CREATE TABLE book (
    id             uuid    NOT NULL PRIMARY KEY,
    slug           text    NOT NULL,
    title          text    NOT NULL,
    author         text    NOT NULL,
    genre          text    NOT NULL,
    published_year integer NOT NULL,
    page_count     integer NOT NULL,
    description    text    NOT NULL
);

-- The catalogue is addressed by slug in both the API and the UI, so it must be unique.
CREATE UNIQUE INDEX ux_book_slug ON book (slug);

-- A physical copy on a shelf. Availability is tracked here because this is the row the
-- borrow flow compare-and-swaps against.
CREATE TABLE book_copy (
    id          uuid    NOT NULL PRIMARY KEY,
    book_id     uuid    NOT NULL REFERENCES book (id),
    copy_number integer NOT NULL,
    status      text    NOT NULL,
    CONSTRAINT ck_book_copy_status CHECK (status IN ('Available', 'OnLoan'))
);

CREATE INDEX ix_book_copy_book_id ON book_copy (book_id);
CREATE UNIQUE INDEX ux_book_copy_book_id_copy_number ON book_copy (book_id, copy_number);

CREATE TABLE loan (
    id           uuid        NOT NULL PRIMARY KEY,
    book_copy_id uuid        NOT NULL REFERENCES book_copy (id),
    borrower_id  uuid        NOT NULL REFERENCES borrower (id),
    borrowed_at  timestamptz NOT NULL,
    due_at       timestamptz NOT NULL,
    returned_at  timestamptz NULL
);

-- The one invariant the application is never allowed to break: a copy can have at most one
-- loan that has not been returned. Returned loans fall out of the index entirely, so history
-- is unlimited.
--
-- Unlike on SQLite, this one earns its keep at runtime. PostgreSQL lets two borrow requests
-- run at the same time, so a bug in the borrow flow could genuinely try to write a second
-- open loan for a copy. The flow does not depend on this index and never catches its
-- violation -- if it fires, something above is broken and should say so.
CREATE UNIQUE INDEX ux_loan_active_book_copy_id ON loan (book_copy_id)
    WHERE returned_at IS NULL;

CREATE INDEX ix_loan_borrower_id ON loan (borrower_id);
CREATE INDEX ix_loan_book_copy_id ON loan (book_copy_id);
