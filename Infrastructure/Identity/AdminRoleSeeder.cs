namespace Infrastructure.Identity;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class AdminRoleSeeder : IHostedService
{
    public const string AdminRole = "Admin";

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminRoleSeeder> _logger;

    public AdminRoleSeeder(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AdminRoleSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(AdminRole));
            if (!result.Succeeded)
            {
                _logger.LogError("Could not create Admin role: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
        }

        var adminEmails = _configuration.GetSection("Admin:Emails").Get<string[]>() ?? Array.Empty<string>();
        foreach (var email in adminEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()))
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Configured admin email {Email} does not match any user.", email);
                continue;
            }

            if (!await userManager.IsInRoleAsync(user, AdminRole))
            {
                var result = await userManager.AddToRoleAsync(user, AdminRole);
                if (!result.Succeeded)
                {
                    _logger.LogError("Could not assign Admin role to {Email}: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
