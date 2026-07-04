using Application.Common.Interfaces;
using Application.Features.AuthorFeatures.Queries;
using Application.Features.TagFeatures.Queries;
using Domain.Entities;
using Domain.Repositories;

namespace Application.UnitTests;

public class AutocompleteFeatureTests
{
    private static readonly Guid OwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task SearchAuthors_ShouldReturnAuthorMatchedByAlias()
    {
        var repository = new FakeAuthorRepository();
        var author = new Author { PrimaryName = "Er Gen", NormalizedPrimaryName = "ER GEN" };
        author.Names.Add(new AuthorName { Name = "耳根", NormalizedName = "耳根", IsPrimary = false });
        await repository.AddAsync(author, CancellationToken.None);
        var handler = new SearchAuthorsQueryHandler(repository);

        var result = await handler.Handle(new SearchAuthorsQuery("耳", 10), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Er Gen", result.First().PrimaryName);
    }

    [Fact]
    public async Task SearchTags_ShouldReturnOnlyCurrentUsersTags()
    {
        var repository = new FakeTagRepository();
        await repository.AddAsync(new Tag { OwnerId = OwnerId, Name = "favorite", NormalizedName = "FAVORITE" }, CancellationToken.None);
        await repository.AddAsync(new Tag { OwnerId = Guid.NewGuid(), Name = "favorite", NormalizedName = "FAVORITE" }, CancellationToken.None);
        var handler = new SearchTagsQueryHandler(repository, new FakeUser());

        var result = await handler.Handle(new SearchTagsQuery("fav", 10), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("favorite", result.First().Name);
    }

    private sealed class FakeUser : IUser
    {
        public Guid? Id => OwnerId;
        public Guid RequiredId => OwnerId;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }

    private sealed class FakeAuthorRepository : IAuthorRepository
    {
        private readonly List<Author> _authors = new();

        public Task AddAsync(Author author, CancellationToken cancellationToken)
        {
            _authors.Add(author);
            return Task.CompletedTask;
        }

        public Task<Author?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_authors.FirstOrDefault(a => a.Id == id));
        public Task<Author?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult(_authors.FirstOrDefault(a => a.NormalizedPrimaryName == name.Trim().ToUpperInvariant()));

        public Task<IEnumerable<Author>> SearchAsync(string? search, int take, CancellationToken cancellationToken)
        {
            var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
            return Task.FromResult<IEnumerable<Author>>(_authors
                .Where(a => a.NormalizedPrimaryName.Contains(normalizedSearch) ||
                            a.Names.Any(n => n.NormalizedName.Contains(normalizedSearch)))
                .Take(take)
                .ToList());
        }

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTagRepository : ITagRepository
    {
        private readonly List<Tag> _tags = new();

        public Task AddAsync(Tag tag, CancellationToken cancellationToken)
        {
            _tags.Add(tag);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Tag>> GetByNamesAsync(Guid ownerId, IEnumerable<string> names, CancellationToken cancellationToken)
        {
            var normalizedNames = names.Select(n => n.Trim().ToUpperInvariant()).ToList();
            return Task.FromResult<IEnumerable<Tag>>(_tags.Where(t => t.OwnerId == ownerId && normalizedNames.Contains(t.NormalizedName)).ToList());
        }

        public Task<Tag?> GetByNameAsync(Guid ownerId, string name, CancellationToken cancellationToken) => Task.FromResult(_tags.FirstOrDefault(t => t.OwnerId == ownerId && t.NormalizedName == name.Trim().ToUpperInvariant()));

        public Task<IEnumerable<Tag>> SearchAsync(Guid ownerId, string? search, int take, CancellationToken cancellationToken)
        {
            var normalizedSearch = search?.Trim().ToUpperInvariant() ?? string.Empty;
            return Task.FromResult<IEnumerable<Tag>>(_tags
                .Where(t => t.OwnerId == ownerId && t.NormalizedName.Contains(normalizedSearch))
                .Take(take)
                .ToList());
        }

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
