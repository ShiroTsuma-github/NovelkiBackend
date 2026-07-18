namespace Infrastructure.IntegrationTests;

using Domain.Associations;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.IntegrationTests.TestSupport;
using Infrastructure.Persistence;

public sealed class AdminMetadataDeletionTests
{
    [Fact]
    public async Task DictionaryDeletion_ShouldReturnConflictWhenItemIsUsedByBook()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var genre = TestData.Genre("Fantasy");
        var book = TestData.Book(database.UserId, "Managed metadata book");
        book.BookGenres.Add(new BookGenre { Book = book, Genre = genre });
        context.AddRange(genre, book);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<EntityInUseException<Status>>(() =>
            new StatusRepository(context).DeleteAsync(TestData.ReadingStatusId, CancellationToken.None));
        await Assert.ThrowsAsync<EntityInUseException<ContentType>>(() =>
            new TypeRepository(context).DeleteAsync(TestData.NovelTypeId, CancellationToken.None));
        await Assert.ThrowsAsync<EntityInUseException<Genre>>(() =>
            new GenreRepository(context).DeleteAsync(genre.Id, CancellationToken.None));
    }
}
