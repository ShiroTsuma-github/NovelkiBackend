using Application.Common.DTOs.Genre;
using Application.Common.Models;
using Application.Features.GenreFeatures.Commands;
using Application.Features.GenreFeatures.Queries.GetGenre;
using Application.Features.GenreFeatures.Validators;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Repositories;

namespace Application.UnitTests;

public class GenreFeatureTests
{
    [Fact]
    public async Task CreateGenre_ShouldPersistAndReturnDto()
    {
        var repository = new FakeGenreRepository();
        var handler = new CreateGenreCommandHandler(repository);

        var result =
            await handler.Handle(new CreateGenreCommand("Fantasy", "Magic stories"), CancellationToken.None);

        Assert.Equal("Fantasy", result.Name);
        Assert.Equal("Magic stories", result.Description);
        Assert.Equal(1, await repository.GetCountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CreateGenre_ShouldRejectDuplicateNameIgnoringCase()
    {
        var repository = new FakeGenreRepository();
        await repository.AddAsync(new Genre { Name = "Fantasy", NormalizedName = "FANTASY" }, CancellationToken.None);
        var handler = new CreateGenreCommandHandler(repository);

        await Assert.ThrowsAsync<EntityAlreadyExistsException<Genre, Guid>>(() =>
            handler.Handle(new CreateGenreCommand("fantasy", null), CancellationToken.None));
    }

    [Fact]
    public async Task GetAllGenres_ShouldReturnPaginatedResult()
    {
        var repository = new FakeGenreRepository();
        await repository.AddAsync(new Genre { Name = "Fantasy", NormalizedName = "FANTASY" }, CancellationToken.None);
        await repository.AddAsync(new Genre { Name = "Sci-Fi", NormalizedName = "SCI-FI" }, CancellationToken.None);
        await repository.AddAsync(new Genre { Name = "Drama", NormalizedName = "DRAMA" }, CancellationToken.None);
        var handler = new GetAllGenresQueryHandler(repository);

        var result = await handler.Handle(new GetAllGenresQuery(1, 1), CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.Skip);
        Assert.Equal(1, result.Take);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GenreHandlers_ShouldCoverQueriesUpdateDeleteAndDetails()
    {
        var repository = new FakeGenreRepository();
        var genre = new Genre { Name = "Fantasy", NormalizedName = "FANTASY", Description = "Magic" };
        await repository.AddAsync(genre, CancellationToken.None);

        var byId =
            await new GetGenreQueryHandler(repository).Handle(new GetGenreQuery(genre.Id), CancellationToken.None);
        var byName =
            await new GetGenreByNameQueryHandler(repository).Handle(new GetGenreByNameQuery("Fantasy"),
                CancellationToken.None);
        var details =
            await new GetGenreDetailsQueryHandler(repository).Handle(new GetGenreDetailsQuery(genre.Id),
                CancellationToken.None);
        var detailsByName =
            await new GetGenreDetailsByNameQueryHandler(repository).Handle(new GetGenreDetailsByNameQuery("Fantasy"),
                CancellationToken.None);

        Assert.Equal(genre.Id, byId.Id);
        Assert.Equal(genre.Id, byName.Id);
        Assert.Equal(genre.Id, details.Id);
        Assert.Equal(genre.Id, detailsByName.Id);

        var updated = await new UpdateGenreCommandHandler(repository)
            .Handle(new UpdateGenreCommand { Id = genre.Id, Name = "Drama", Description = "Serious" },
                CancellationToken.None);
        Assert.Equal("Drama", updated.Name);

        await new DeleteGenreCommandHandler(repository).Handle(new DeleteGenreCommand(genre.Id),
            CancellationToken.None);
        Assert.Equal(0, await repository.GetCountAsync(CancellationToken.None));
    }

    [Fact]
    public void GenreValidators_ShouldRejectBlankNames()
    {
        Assert.False(new CreateGenreCommandValidator().Validate(new CreateGenreCommand("", null)).IsValid);
        Assert.False(new UpdateGenreCommandValidator()
            .Validate(new UpdateGenreCommand { Id = Guid.NewGuid(), Name = "" }).IsValid);
    }

    private sealed class FakeGenreRepository : IGenreRepository
    {
        private readonly List<Genre> _genres = new();

        public Task AddAsync(Genre entity, CancellationToken cancellationToken)
        {
            _genres.Add(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _genres.RemoveAll(g => g.Id == id);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Genre>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Genre>>(_genres.Skip(Skip).Take(Take).ToList());
        }

        public Task<Genre?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_genres.FirstOrDefault(g => g.Id == id));
        }

        public Task<IEnumerable<Genre>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
        {
            var idList = ids.ToList();
            return Task.FromResult<IEnumerable<Genre>>(_genres.Where(g => idList.Contains(g.Id)).ToList());
        }

        public Task<Genre?> GetByNameAsync(string name, CancellationToken cancellationToken)
        {
            return Task.FromResult(_genres.FirstOrDefault(g =>
                string.Equals(g.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
        }

        public Task<int> GetCountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_genres.Count);
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
