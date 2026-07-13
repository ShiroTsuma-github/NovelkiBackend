using System.Reflection;
using Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Application.UnitTests;

public class SecurityAttributeTests
{
    [Fact]
    public void NonAccountControllerActions_ShouldRequireAuthorization()
    {
        var actionMethods = typeof(BookController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(type => type != typeof(AccountController))
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsActionMethod)
                .Select(method => new
                {
                    Controller = type,
                    Method = method,
                    Authorize = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                        .Concat(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                        .ToList()
                }))
            .ToList();

        Assert.NotEmpty(actionMethods);
        Assert.DoesNotContain(actionMethods, action => action.Authorize.Count == 0);
    }

    [Fact]
    public void AdminController_ShouldRequireAdminRoleAtControllerLevel()
    {
        var authorize = typeof(AdminController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single();

        Assert.Equal("Admin", authorize.Roles);
    }

    private static bool IsActionMethod(MethodInfo method)
    {
        if (method.IsSpecialName)
        {
            return false;
        }

        if (method.GetCustomAttributes<NonActionAttribute>(inherit: true).Any())
        {
            return false;
        }

        return method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any();
    }
}
