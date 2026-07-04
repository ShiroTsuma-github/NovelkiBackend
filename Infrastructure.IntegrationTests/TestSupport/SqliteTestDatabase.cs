using Application.Common.Interfaces;
using Infrastructure.Contexts;
using Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.TestSupport;

public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestUser _user;

    public SqliteTestDatabase(Guid? userId = null)
    {
        UserId = userId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        _user = new TestUser(UserId);
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=ON;";
        command.ExecuteNonQuery();

        using var context = CreateContext();
        context.Database.EnsureCreated();
        context.Users.Add(new User
        {
            Id = UserId,
            UserName = "reader",
            NormalizedUserName = "READER",
            Email = "reader@example.com",
            NormalizedEmail = "READER@EXAMPLE.COM"
        });
        context.SaveChanges();
    }

    public Guid UserId { get; }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options, _user);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestUser : IUser
    {
        public TestUser(Guid id)
        {
            Id = id;
        }

        public Guid? Id { get; }
        public Guid RequiredId => Id!.Value;
        public string? Email => "reader@example.com";
        public string? Username => "reader";
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsAuthenticated => true;
        public bool Valid => true;
    }
}
