namespace Infrastructure.IntegrationTests;

using Application.Common.Interfaces;
using Domain.Associations;
using Microsoft.EntityFrameworkCore;
using Services;
using TestSupport;

public sealed class MetadataSimilarityReconcilerTests
{
    [Fact]
    public async Task ReconcileAsync_ShouldMergeEquivalentRecordsAndPreserveBookLinks()
    {
        using var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        var r18 = TestData.Tag(database.UserId, "R18");
        r18.IsGlobal = true;
        r18.OwnerId = null;
        var rDash18 = TestData.Tag(database.UserId, "R-18");
        var action = TestData.Tag(database.UserId, "Action");
        var faction = TestData.Tag(database.UserId, "Faction");
        var slice = TestData.Genre("Slice Of Life");
        var sliceTypo = TestData.Genre("Slice Of Lifee");
        var first = TestData.Book(database.UserId, "First merge book");
        var second = TestData.Book(database.UserId, "Second merge book");
        first.BookTags.Add(new BookTag { Book = first, Tag = r18 });
        second.BookTags.Add(new BookTag { Book = second, Tag = rDash18 });
        first.BookGenres.Add(new BookGenre { Book = first, Genre = slice });
        second.BookGenres.Add(new BookGenre { Book = second, Genre = sliceTypo });
        context.AddRange(r18, rDash18, action, faction, slice, sliceTypo, first, second);
        await context.SaveChangesAsync();

        var service = new MetadataSimilarityReconciler(context, new NoopCache());
        await service.ReconcileAsync(CancellationToken.None);
        await service.ReconcileAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var mergedTags = await context.Tags.Where(tag => tag.Name == "R18" || tag.Name == "R-18").ToListAsync();
        var mergedTag = Assert.Single(mergedTags);
        Assert.Equal(2, await context.Set<BookTag>().CountAsync(link => link.TagId == mergedTag.Id));
        Assert.Equal(2, await context.Tags.CountAsync(tag => tag.Name == "Action" || tag.Name == "Faction"));
        var mergedGenres = await context.Genres
            .Where(genre => genre.Name == "Slice Of Life" || genre.Name == "Slice Of Lifee").ToListAsync();
        var mergedGenre = Assert.Single(mergedGenres);
        Assert.Equal(2, await context.Set<BookGenre>().CountAsync(link => link.GenreId == mergedGenre.Id));
    }

    private sealed class NoopCache : IBookListCacheInvalidator
    {
        public Task InvalidateBooksAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
