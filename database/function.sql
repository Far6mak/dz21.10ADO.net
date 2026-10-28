CREATE OR REPLACE FUNCTION get_books_by_author(p_author_id integer)
RETURNS TABLE (
    id integer,
    title varchar(200),
    year integer,
    author_id integer
)
LANGUAGE sql
STABLE
AS $$
    SELECT b.id, b.title, b.year, b.author_id
    FROM books AS b
    WHERE b.author_id = p_author_id
    ORDER BY b.id;
$$;
