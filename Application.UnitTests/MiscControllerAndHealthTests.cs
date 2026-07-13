using Api.Controllers;
using Api.Health;
using Application.Common.DTOs.Author;
using Application.Common.DTOs.Tag;
using Application.Features.AuthorFeatures.Queries;
using Application.Features.TagFeatures.Queries;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace Application.UnitTests;

public class MiscControllerAndHealthTests
{
    [Fact]
    public async Task AuthorController_ShouldForwardSearchToMediator()
    {
        var mediator = new Mock<IMediator>();
        var payload = new[] { new AuthorDto { Id = Guid.NewGuid(), PrimaryName = "Author" } };
        mediator.Setup(mock => mock.Send(It.IsAny<SearchAuthorsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(payload);
        var controller = new AuthorController(mediator.Object);
        var query = new SearchAuthorsQuery("auth", 5);

        var result = Assert.IsType<OkObjectResult>(await controller.Search(query));

        Assert.Same(payload, result.Value);
        mediator.Verify(mock => mock.Send(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TagController_ShouldForwardSearchToMediator()
    {
        var mediator = new Mock<IMediator>();
        var payload = new[] { new TagDto { Id = Guid.NewGuid(), Name = "favorite" } };
        mediator.Setup(mock => mock.Send(It.IsAny<SearchTagsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(payload);
        var controller = new TagController(mediator.Object);
        var query = new SearchTagsQuery("fav", 5);

        var result = Assert.IsType<OkObjectResult>(await controller.Search(query));

        Assert.Same(payload, result.Value);
        mediator.Verify(mock => mock.Send(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DatabaseReadyHealthCheck_ShouldReturnHealthy_WhenDatabaseCanConnect()
    {
        await using var context = await CreateContextAsync(openConnection: true);
        var healthCheck = new DatabaseReadyHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task DatabaseReadyHealthCheck_ShouldReturnUnhealthy_WhenDatabaseCannotConnect()
    {
        await using var context = CreateMissingDatabaseContext();
        var healthCheck = new DatabaseReadyHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static async Task<ApplicationDbContext> CreateContextAsync(bool openConnection)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        if (openConnection)
        {
            await connection.OpenAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ApplicationDbContext(options, new FakeUser());
        if (openConnection)
        {
            await context.Database.EnsureCreatedAsync();
        }

        return context;
    }

    private static ApplicationDbContext CreateMissingDatabaseContext()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "missing.db");
        var connection = new SqliteConnection($"Data Source={missingPath};Mode=ReadOnly");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        return new ApplicationDbContext(options, new FakeUser());
    }

    private sealed class FakeUser : Application.Common.Interfaces.IUser
    {
        public Guid? Id => Guid.NewGuid();
        public Guid RequiredId => Id!.Value;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
