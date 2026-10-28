using Dapper;
using Npgsql;

namespace LibraryDapper;

public sealed class LibraryRepository
{
    private readonly string _cs;

    public LibraryRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Строка подключения не должна быть пустой.", nameof(connectionString));

        _cs = connectionString;
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private NpgsqlConnection Conn() => new(_cs);

    public async Task<int> AddAuthorAsync(Author author)
    {
        const string sql = """
            INSERT INTO authors (name, country)
            VALUES (@Name, @Country)
            RETURNING id;
            """;
        await using var c = Conn();
        return await c.ExecuteScalarAsync<int>(sql, author);
    }

    public async Task<int> AddBookAsync(Book book)
    {
        const string sql = """
            INSERT INTO books (title, year, author_id)
            VALUES (@Title, @Year, @AuthorId)
            RETURNING id;
            """;
        await using var c = Conn();
        return await c.ExecuteScalarAsync<int>(sql, book);
    }

    public async Task<IEnumerable<Book>> GetBooksAsync()
    {
        const string sql = "SELECT id, title, year, author_id FROM books ORDER BY id";
        await using var c = Conn();
        return await c.QueryAsync<Book>(sql);
    }

    public async Task<IEnumerable<Book>> GetBooksAfterYearAsync(int year)
    {
        const string sql = "SELECT id, title, year, author_id FROM books WHERE year >= @year ORDER BY year, title";
        await using var c = Conn();
        return await c.QueryAsync<Book>(sql, new { year });
    }

    public async Task<IEnumerable<Author>> GetAuthorsWithBooksAsync()
    {
        const string sql = """
            SELECT a.id, a.name, a.country,
                   b.id, b.title, b.year, b.author_id
            FROM authors a
            LEFT JOIN books b ON b.author_id = a.id
            ORDER BY a.id, b.id;
            """;

        var authors = new Dictionary<int, Author>();
        await using var c = Conn();
        await c.QueryAsync<Author, Book, Author>(
            sql,
            (author, book) =>
            {
                if (!authors.TryGetValue(author.Id, out var existing))
                {
                    existing = author;
                    authors.Add(author.Id, existing);
                }

                if (book is not null && book.Id != 0)
                    existing.Books.Add(book);

                return existing;
            },
            splitOn: "id");

        return authors.Values;
    }

    public async Task<bool> UpdateBookYearAsync(int bookId, int newYear)
    {
        const string sql = "UPDATE books SET year = @newYear WHERE id = @bookId";
        await using var c = Conn();
        return await c.ExecuteAsync(sql, new { bookId, newYear }) > 0;
    }

    public async Task<bool> DeleteAuthorAsync(int authorId)
    {
        const string sql = "DELETE FROM authors WHERE id = @authorId";
        await using var c = Conn();
        return await c.ExecuteAsync(sql, new { authorId }) > 0;
    }

    public async Task<int> AddGenreAsync(string name)
    {
        const string sql = """
            INSERT INTO genres (name) VALUES (@name)
            ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name
            RETURNING id;
            """;
        await using var c = Conn();
        return await c.ExecuteScalarAsync<int>(sql, new { name });
    }

    public async Task AddGenreToBookAsync(int bookId, int genreId)
    {
        const string sql = """
            INSERT INTO book_genres (book_id, genre_id)
            VALUES (@bookId, @genreId)
            ON CONFLICT DO NOTHING;
            """;
        await using var c = Conn();
        await c.ExecuteAsync(sql, new { bookId, genreId });
    }

    // QueryMultiple: один вызов БД, два SELECT — книги и связи с жанрами.
    public async Task<IReadOnlyList<Book>> GetBooksWithGenresAsync()
    {
        const string sql = """
            SELECT id, title, year, author_id FROM books ORDER BY id;
            SELECT bg.book_id, g.id AS genre_id, g.name AS genre_name
            FROM book_genres bg
            JOIN genres g ON g.id = bg.genre_id
            ORDER BY bg.book_id, g.name;
            """;

        await using var c = Conn();
        using var results = await c.QueryMultipleAsync(sql);
        var books = (await results.ReadAsync<Book>()).ToList();
        var genreRows = (await results.ReadAsync<BookGenreRow>()).ToList();
        var booksById = books.ToDictionary(book => book.Id);

        foreach (var row in genreRows)
        {
            if (booksById.TryGetValue(row.BookId, out var book))
                book.Genres.Add(row.GenreName);
        }

        return books;
    }

    // Поиск с ILIKE и параметром — term никогда не становится частью SQL-кода.
    public async Task<IEnumerable<Book>> SearchBooksAsync(string term)
    {
        const string sql = """
            SELECT id, title, year, author_id
            FROM books
            WHERE title ILIKE @pattern
            ORDER BY title, id;
            """;
        await using var c = Conn();
        return await c.QueryAsync<Book>(sql, new { pattern = $"%{term}%" });
    }

    public async Task<IEnumerable<Book>> GetBooksPagedAsync(int page, int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Номер страницы начинается с 1.");
        if (pageSize < 1 || pageSize > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Размер страницы должен быть от 1 до 100.");

        const string sql = """
            SELECT id, title, year, author_id
            FROM books
            ORDER BY id
            LIMIT @size OFFSET @offset;
            """;
        int offset = checked((page - 1) * pageSize);
        await using var c = Conn();
        return await c.QueryAsync<Book>(sql, new { size = pageSize, offset });
    }

    // Перенос книги другому автору атомарен: либо обновление завершится, либо откатится.
    public async Task<bool> TransferBookAsync(int bookId, int newAuthorId)
    {
        await using var c = Conn();
        await c.OpenAsync();
        await using var transaction = await c.BeginTransactionAsync();

        try
        {
            const string authorSql = "SELECT EXISTS (SELECT 1 FROM authors WHERE id = @newAuthorId)";
            bool authorExists = await c.ExecuteScalarAsync<bool>(
                authorSql, new { newAuthorId }, transaction);

            if (!authorExists)
            {
                await transaction.RollbackAsync();
                return false;
            }

            const string updateSql = "UPDATE books SET author_id = @newAuthorId WHERE id = @bookId";
            int rows = await c.ExecuteAsync(updateSql, new { bookId, newAuthorId }, transaction);

            if (rows == 0)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // Демонстрация уязвимости: используйте только на учебной базе и только с SELECT.
    // Ввод вида: ' OR '1'='1' -- превращает условие в истинное для всех строк.
    public async Task<IEnumerable<Book>> VulnerableSearchBooksAsync(string term)
    {
        string unsafeSql = $"SELECT id, title, year, author_id FROM books WHERE title ILIKE '%{term}%'";
        await using var c = Conn();
        return await c.QueryAsync<Book>(unsafeSql);
    }

    // Та же задача безопасно — параметр передаётся отдельно от текста SQL.
    public async Task<IEnumerable<Book>> SafeSearchBooksAsync(string term)
    {
        const string sql = """
            SELECT id, title, year, author_id
            FROM books
            WHERE title ILIKE @pattern
            ORDER BY title;
            """;
        await using var c = Conn();
        return await c.QueryAsync<Book>(sql, new { pattern = $"%{term}%" });
    }

    // Функция get_books_by_author создаётся из database/function.sql.
    public async Task<IEnumerable<Book>> GetBooksByAuthorFunctionAsync(int authorId)
    {
        const string sql = "SELECT * FROM get_books_by_author(@authorId)";
        await using var c = Conn();
        return await c.QueryAsync<Book>(sql, new { authorId });
    }
}
