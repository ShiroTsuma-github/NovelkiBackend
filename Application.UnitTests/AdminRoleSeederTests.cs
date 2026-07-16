using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests;

public class AdminRoleSeederTests
{
    [Fact]
    public async Task StartAsync_ShouldCreateAdminRoleAndAssignConfiguredUsers()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "admin@example.com", UserName = "admin" };
        Mock<RoleManager<IdentityRole<Guid>>> roleManager = CreateRoleManager();
        Mock<UserManager<User>> userManager = CreateUserManager();
        roleManager.Setup(manager => manager.RoleExistsAsync(AdminRoleSeeder.AdminRole)).ReturnsAsync(false);
        roleManager.Setup(manager =>
                manager.CreateAsync(It.Is<IdentityRole<Guid>>(role => role.Name == AdminRoleSeeder.AdminRole)))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.FindByEmailAsync("admin@example.com")).ReturnsAsync(user);
        userManager.Setup(manager => manager.IsInRoleAsync(user, AdminRoleSeeder.AdminRole)).ReturnsAsync(false);
        userManager.Setup(manager => manager.AddToRoleAsync(user, AdminRoleSeeder.AdminRole))
            .ReturnsAsync(IdentityResult.Success);
        AdminRoleSeeder seeder = CreateSeeder(roleManager.Object, userManager.Object,
            [" admin@example.com ", "", "missing@example.com"]);

        await seeder.StartAsync(CancellationToken.None);

        roleManager.Verify(manager => manager.CreateAsync(It.IsAny<IdentityRole<Guid>>()), Times.Once);
        userManager.Verify(manager => manager.FindByEmailAsync("admin@example.com"), Times.Once);
        userManager.Verify(manager => manager.FindByEmailAsync("missing@example.com"), Times.Once);
        userManager.Verify(manager => manager.AddToRoleAsync(user, AdminRoleSeeder.AdminRole), Times.Once);
    }

    [Fact]
    public async Task StartAsync_ShouldSkipRoleAssignment_WhenRoleCreationFails()
    {
        Mock<RoleManager<IdentityRole<Guid>>> roleManager = CreateRoleManager();
        Mock<UserManager<User>> userManager = CreateUserManager();
        roleManager.Setup(manager => manager.RoleExistsAsync(AdminRoleSeeder.AdminRole)).ReturnsAsync(false);
        roleManager.Setup(manager => manager.CreateAsync(It.IsAny<IdentityRole<Guid>>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "boom" }));
        AdminRoleSeeder seeder = CreateSeeder(roleManager.Object, userManager.Object, ["admin@example.com"]);

        await seeder.StartAsync(CancellationToken.None);

        userManager.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        userManager.Verify(manager => manager.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StartAsync_ShouldNotAssignUserAlreadyInRole()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "admin@example.com", UserName = "admin" };
        Mock<RoleManager<IdentityRole<Guid>>> roleManager = CreateRoleManager();
        Mock<UserManager<User>> userManager = CreateUserManager();
        roleManager.Setup(manager => manager.RoleExistsAsync(AdminRoleSeeder.AdminRole)).ReturnsAsync(true);
        userManager.Setup(manager => manager.FindByEmailAsync("admin@example.com")).ReturnsAsync(user);
        userManager.Setup(manager => manager.IsInRoleAsync(user, AdminRoleSeeder.AdminRole)).ReturnsAsync(true);
        AdminRoleSeeder seeder = CreateSeeder(roleManager.Object, userManager.Object, ["admin@example.com"]);

        await seeder.StartAsync(CancellationToken.None);

        roleManager.Verify(manager => manager.CreateAsync(It.IsAny<IdentityRole<Guid>>()), Times.Never);
        userManager.Verify(manager => manager.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    private static AdminRoleSeeder CreateSeeder(RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<User> userManager, string[] adminEmails)
    {
        var services = new ServiceCollection();
        services.AddSingleton(roleManager);
        services.AddSingleton(userManager);
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(adminEmails.Select((email, index) =>
                new KeyValuePair<string, string?>($"Admin:Emails:{index}", email)))
            .Build();

        return new AdminRoleSeeder(services.BuildServiceProvider(), configuration,
            NullLogger<AdminRoleSeeder>.Instance);
    }

    private static Mock<RoleManager<IdentityRole<Guid>>> CreateRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole<Guid>>>();
        return new Mock<RoleManager<IdentityRole<Guid>>>(
            store.Object,
            Array.Empty<IRoleValidator<IdentityRole<Guid>>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole<Guid>>>.Instance);
    }

    private static Mock<UserManager<User>> CreateUserManager()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            store.Object,
            null!,
            new PasswordHasher<User>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<User>>.Instance);
    }
}
