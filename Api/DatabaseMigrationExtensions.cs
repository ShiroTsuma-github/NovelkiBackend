namespace Api;

using Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:AutoMigrate"))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
