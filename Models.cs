namespace LibraryDapper;

public sealed class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Country { get; set; }
    public List<Book> Books { get; set; } = new();
}

public sealed class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int? Year { get; set; }
    public int AuthorId { get; set; }
    public List<string> Genres { get; set; } = new();
}

public sealed class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

internal sealed class BookGenreRow
{
    public int BookId { get; set; }
    public int GenreId { get; set; }
    public string GenreName { get; set; } = "";
}
