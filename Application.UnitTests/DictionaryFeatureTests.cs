using Application.Features.StatusFeatures.Commands;
using Application.Features.StatusFeatures.Queries.GetStatus;
using Application.Features.StatusFeatures.Validators;
using Application.Features.TypeFeatures.Commands;
using Application.Features.TypeFeatures.Queries.GetType;
using Application.Features.TypeFeatures.Validators;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Repositories;

namespace Application.UnitTests;

public class DictionaryFeatureTests
{
    [Fact]
    public async Task StatusHandlers_ShouldCoverCrudQueriesAndDetails()
    {
        var repository = new FakeStatusRepository();
        var created = await new CreateStatusCommandHandler(repository)
            .Handle(new CreateStatusCommand("Paused", "Waiting"), CancellationToken.None);

        Assert.Equal("Paused", created.Name);
        await Assert.ThrowsAsync<EntityAlreadyExistsException<Status, Guid>>(() =>
            new CreateStatusCommandHandler(repository).Handle(new CreateStatusCommand("paused", null), CancellationToken.None));

        var all = await new GetAllStatusesQueryHandler(repository).Handle(new GetAllStatusesQuery(0, 10), CancellationToken.None);
        var byId = await new GetStatusQueryHandler(repository).Handle(new GetStatusQuery(created.Id), CancellationToken.None);
        var byName = await new GetStatusByNameQueryHandler(repository).Handle(new GetStatusByNameQuery("Paused"), CancellationToken.None);
        var details = await new GetStatusDetailsQueryHandler(repository).Handle(new GetStatusDetailsQuery(created.Id), CancellationToken.None);
        var detailsByName = await new GetStatusDetailsByNameQueryHandler(repository).Handle(new GetStatusDetailsByNameQuery("Paused"), CancellationToken.None);

        Assert.Single(all.Data);
        Assert.Equal(created.Id, byId.Id);
        Assert.Equal(created.Id, byName.Id);
        Assert.Equal(created.Id, details.Id);
        Assert.Equal(created.Id, detailsByName.Id);

        var updated = await new UpdateStatusCommandHandler(repository)
            .Handle(new UpdateStatusCommand { Id = created.Id, Name = "On Hold", Description = "Later" }, CancellationToken.None);
        Assert.Equal("On Hold", updated.Name);

        await new DeleteStatusCommandHandler(repository).Handle(new DeleteStatusCommand(created.Id), CancellationToken.None);
        Assert.Equal(0, await repository.GetCountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TypeHandlers_ShouldCoverCrudQueriesAndDetails()
    {
        var repository = new FakeTypeRepository();
        var created = await new CreateTypeCommandHandler(repository)
            .Handle(new CreateTypeCommand("Audio", "Narrated"), CancellationToken.None);

        Assert.Equal("Audio", created.Name);
        await Assert.ThrowsAsync<EntityAlreadyExistsException<ContentType, Guid>>(() =>
            new CreateTypeCommandHandler(repository).Handle(new CreateTypeCommand("audio", null), CancellationToken.None));

        var all = await new GetAllTypesQueryHandler(repository).Handle(new GetAllTypesQuery(0, 10), CancellationToken.None);
        var byId = await new GetTypeQueryHandler(repository).Handle(new GetTypeQuery(created.Id), CancellationToken.None);
        var byName = await new GetTypeByNameQueryHandler(repository).Handle(new GetTypeByNameQuery("Audio"), CancellationToken.None);
        var details = await new GetTypeDetailsQueryHandler(repository).Handle(new GetTypeDetailsQuery(created.Id), CancellationToken.None);
        var detailsByName = await new GetTypeDetailsByNameQueryHandler(repository).Handle(new GetTypeDetailsByNameQuery("Audio"), CancellationToken.None);

        Assert.Single(all.Data);
        Assert.Equal(created.Id, byId.Id);
        Assert.Equal(created.Id, byName.Id);
        Assert.Equal(created.Id, details.Id);
        Assert.Equal(created.Id, detailsByName.Id);

        var updated = await new UpdateTypeCommandHandler(repository)
            .Handle(new UpdateTypeCommand { Id = created.Id, Name = "Audiobook", Description = "Spoken" }, CancellationToken.None);
        Assert.Equal("Audiobook", updated.Name);

        await new DeleteTypeCommandHandler(repository).Handle(new DeleteTypeCommand(created.Id), CancellationToken.None);
        Assert.Equal(0, await repository.GetCountAsync(CancellationToken.None));
    }

    [Fact]
    public void DictionaryValidators_ShouldRejectBlankNames()
    {
        Assert.False(new CreateStatusCommandValidator().Validate(new CreateStatusCommand("", null)).IsValid);
        Assert.False(new UpdateStatusCommandValidator().Validate(new UpdateStatusCommand { Id = Guid.NewGuid(), Name = "" }).IsValid);
        Assert.False(new CreateTypeCommandValidator().Validate(new CreateTypeCommand("", null)).IsValid);
        Assert.False(new UpdateTypeCommandValidator().Validate(new UpdateTypeCommand { Id = Guid.NewGuid(), Name = "" }).IsValid);
    }

    private sealed class FakeStatusRepository : IStatusRepository
    {
        private readonly List<Status> _statuses = [];

        public Task<Status?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_statuses.FirstOrDefault(status => status.Id == id));
        public Task<Status?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult(_statuses.FirstOrDefault(status => string.Equals(status.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task<IEnumerable<Status>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<Status>>(_statuses.Skip(Skip).Take(Take).ToList());
        public Task<int> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult(_statuses.Count);
        public Task AddAsync(Status status, CancellationToken cancellationToken)
        {
            _statuses.Add(status);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _statuses.RemoveAll(status => status.Id == id);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTypeRepository : ITypeRepository
    {
        private readonly List<ContentType> _types = [];

        public Task<ContentType?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_types.FirstOrDefault(type => type.Id == id));
        public Task<ContentType?> GetByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult(_types.FirstOrDefault(type => string.Equals(type.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task<IEnumerable<ContentType>> GetAllAsync(int Skip, int Take, CancellationToken cancellationToken) => Task.FromResult<IEnumerable<ContentType>>(_types.Skip(Skip).Take(Take).ToList());
        public Task<int> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult(_types.Count);
        public Task AddAsync(ContentType type, CancellationToken cancellationToken)
        {
            _types.Add(type);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            _types.RemoveAll(type => type.Id == id);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
