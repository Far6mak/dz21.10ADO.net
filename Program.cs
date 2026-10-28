using LibraryDapper;

string connectionString = Environment.GetEnvironmentVariable("LIBRARY_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=library;Username=lab;Password=lab123";

var repo = new LibraryRepository(connectionString);

try
{
    Console.WriteLine("=== Авторы и их книги ===");
    foreach (var author in await repo.GetAuthorsWithBooksAsync())
    {
        Console.WriteLine($"{author.Name} ({author.Country ?? "страна не указана"}):");
        foreach (var book in author.Books)
            Console.WriteLine($"  • {book.Title} [{book.Year?.ToString() ?? "год неизвестен"}]");
    }

    Console.WriteLine();
    Console.WriteLine("=== Книги с жанрами (QueryMultiple) ===");
    foreach (var book in await repo.GetBooksWithGenresAsync())
    {
        string genres = book.Genres.Count == 0 ? "жанры не указаны" : string.Join(", ", book.Genres);
        Console.WriteLine($"{book.Id}. {book.Title} — {genres}");
    }

    Console.WriteLine();
    Console.Write("Поиск по названию: ");
    string term = Console.ReadLine() ?? "";
    Console.WriteLine("--- Результаты SearchBooksAsync (ILIKE) ---");
    foreach (var book in await repo.SearchBooksAsync(term))
        PrintBook(book);

    Console.WriteLine();
    Console.Write("Номер страницы (например, 1): ");
    if (int.TryParse(Console.ReadLine(), out int page) && page > 0)
    {
        Console.Write("Размер страницы (например, 5): ");
        if (int.TryParse(Console.ReadLine(), out int size) && size > 0)
        {
            Console.WriteLine("--- Пагинация ---");
            foreach (var book in await repo.GetBooksPagedAsync(page, size))
                PrintBook(book);
        }
        else
        {
            Console.WriteLine("Размер страницы должен быть положительным числом.");
        }
    }
    else
    {
        Console.WriteLine("Номер страницы должен быть положительным числом.");
    }

    Console.WriteLine();
    Console.Write("ID автора для вызова функции PostgreSQL: ");
    if (int.TryParse(Console.ReadLine(), out int authorId))
    {
        Console.WriteLine("--- Результат get_books_by_author ---");
        foreach (var book in await repo.GetBooksByAuthorFunctionAsync(authorId))
            PrintBook(book);
    }

    Console.WriteLine();
    Console.WriteLine("=== SQL-инъекция: безопасная демонстрация на SELECT ===");
    Console.WriteLine("Введите учебную строку (например, ' OR '1'='1' --), чтобы увидеть разницу.");
    Console.Write("Строка поиска: ");
    string injectionTerm = Console.ReadLine() ?? "";
    Console.WriteLine("Небезопасный запрос конкатенирует введённый текст в SQL:");
    Console.WriteLine($"SELECT id, title, year, author_id FROM books WHERE title ILIKE '%{injectionTerm}%'");
    Console.WriteLine("Запуск уязвимого запроса выполняется только по явному подтверждению.");
    Console.Write("Выполнить уязвимый SELECT на учебной базе? (yes/no): ");
    if (string.Equals(Console.ReadLine(), "yes", StringComparison.OrdinalIgnoreCase))
    {
        var vulnerable = (await repo.VulnerableSearchBooksAsync(injectionTerm)).ToList();
        Console.WriteLine($"Уязвимый запрос вернул строк: {vulnerable.Count}");
    }
    var safe = (await repo.SafeSearchBooksAsync(injectionTerm)).ToList();
    Console.WriteLine($"Параметризованный запрос вернул строк: {safe.Count}");

    Console.WriteLine();
    Console.Write("ID книги для переноса: ");
    if (int.TryParse(Console.ReadLine(), out int bookId))
    {
        Console.Write("ID нового автора: ");
        if (int.TryParse(Console.ReadLine(), out int newAuthorId))
        {
            bool transferred = await repo.TransferBookAsync(bookId, newAuthorId);
            Console.WriteLine(transferred
                ? "Книга успешно перенесена к новому автору."
                : "Перенос не выполнен: проверьте ID книги и нового автора.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("Ошибка работы с PostgreSQL.");
    Console.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static void PrintBook(Book book)
{
    Console.WriteLine($"{book.Id}. {book.Title} ({book.Year?.ToString() ?? "год неизвестен"}), author_id={book.AuthorId}");
}
