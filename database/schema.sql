-- Run this script in the target library database.
CREATE TABLE IF NOT EXISTS authors (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    country VARCHAR(50)
);

CREATE TABLE IF NOT EXISTS books (
    id SERIAL PRIMARY KEY,
    title VARCHAR(200) NOT NULL,
    year INT,
    author_id INT NOT NULL REFERENCES authors(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS genres (
    id SERIAL PRIMARY KEY,
    name VARCHAR(80) NOT NULL UNIQUE
);

-- Many-to-many: a book may have multiple genres and a genre may belong to many books.
CREATE TABLE IF NOT EXISTS book_genres (
    book_id INT NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    genre_id INT NOT NULL REFERENCES genres(id) ON DELETE CASCADE,
    PRIMARY KEY (book_id, genre_id)
);

-- Small repeatable sample dataset. Existing matching records are left intact.
INSERT INTO authors (name, country)
SELECT 'J.R.R. Tolkien', 'UK'
WHERE NOT EXISTS (SELECT 1 FROM authors WHERE name = 'J.R.R. Tolkien');

INSERT INTO authors (name, country)
SELECT 'George Orwell', 'UK'
WHERE NOT EXISTS (SELECT 1 FROM authors WHERE name = 'George Orwell');

INSERT INTO books (title, year, author_id)
SELECT 'The Hobbit', 1937, a.id
FROM authors a
WHERE a.name = 'J.R.R. Tolkien'
  AND NOT EXISTS (SELECT 1 FROM books b WHERE b.title = 'The Hobbit');

INSERT INTO books (title, year, author_id)
SELECT 'The Fellowship of the Ring', 1954, a.id
FROM authors a
WHERE a.name = 'J.R.R. Tolkien'
  AND NOT EXISTS (SELECT 1 FROM books b WHERE b.title = 'The Fellowship of the Ring');

INSERT INTO books (title, year, author_id)
SELECT '1984', 1949, a.id
FROM authors a
WHERE a.name = 'George Orwell'
  AND NOT EXISTS (SELECT 1 FROM books b WHERE b.title = '1984');

INSERT INTO genres (name) VALUES ('Fantasy'), ('Classic'), ('Dystopia')
ON CONFLICT (name) DO NOTHING;

INSERT INTO book_genres (book_id, genre_id)
SELECT b.id, g.id
FROM books b
JOIN genres g ON
    (b.title IN ('The Hobbit', 'The Fellowship of the Ring') AND g.name = 'Fantasy')
    OR (b.title = 'The Fellowship of the Ring' AND g.name = 'Classic')
    OR (b.title = '1984' AND g.name IN ('Classic', 'Dystopia'))
ON CONFLICT DO NOTHING;
