# Библиотека: Dapper и PostgreSQL

Консольное приложение на C#/.NET 8 для работы с PostgreSQL через Dapper и Npgsql.

## Клонирование

```bash
git clone https://github.com/Far6mak/dz21.10ADO.net.git
cd dz21.10ADO.net
```

## Настройка базы

Создайте базу данных PostgreSQL с именем `library` (или используйте свою) и выполните скрипты в ней по порядку:

1. `database/schema.sql` — таблицы `authors`, `books`, `genres`, таблица связи `book_genres` и демонстрационные данные.
2. `database/function.sql` — функция `get_books_by_author`.

Можно выполнить через pgAdmin Query Tool или через терминал:

```bash
psql -U lab -d library -f database/schema.sql
psql -U lab -d library -f database/function.sql
```

Замените `lab` на своего пользователя PostgreSQL, если он отличается.

## Подключение и запуск

Приложение читает строку подключения из переменной окружения `LIBRARY_CONNECTION_STRING`. Для локальной базы в PowerShell:

```powershell
$env:LIBRARY_CONNECTION_STRING = "Host=localhost;Port=5432;Database=library;Username=lab;Password=ВАШ_ПАРОЛЬ"
dotnet run --project LibraryDapper.csproj
```

В Linux/macOS:

```bash
export LIBRARY_CONNECTION_STRING="Host=localhost;Port=5432;Database=library;Username=lab;Password=ВАШ_ПАРОЛЬ"
dotnet run --project LibraryDapper.csproj
```

Укажите действительные имя пользователя, пароль, порт и базу. Не добавляйте свой пароль в GitHub. Если переменная не задана, программа использует учебное подключение `lab/lab123`; измените её перед запуском на свои учётные данные.

## Реализованные задания

- CRUD-примеры: добавление автора/книги, чтение книг, выборка по году, изменение года и удаление автора.
- Связь книг и жанров «многие-ко-многим» через `genres` и `book_genres`.
- `GetBooksWithGenresAsync()`: `QueryMultiple` читает книги и связи с жанрами из двух SELECT в одном обращении к БД.
- `SearchBooksAsync(string term)`: поиск названия через PostgreSQL `ILIKE` с параметром.
- `GetBooksPagedAsync(int page, int pageSize)`: `LIMIT @size OFFSET @offset`.
- `TransferBookAsync(int bookId, int newAuthorId)`: транзакция через `BeginTransaction()`, проверка автора и откат при ошибке.
- SQL-инъекция: `VulnerableSearchBooksAsync` показывает небезопасную конкатенацию только для учебного SELECT; `SafeSearchBooksAsync` использует параметр. Запуск уязвимого варианта требует явного подтверждения в консоли.
- Вызов PostgreSQL-функции через Dapper: `SELECT * FROM get_books_by_author(@authorId)`.

## Важно

SQL-инъекция демонстрируется только на SELECT-запросе к учебной базе. Не запускайте уязвимый метод на базе с важными данными. Все обычные методы репозитория используют параметризованные запросы.
