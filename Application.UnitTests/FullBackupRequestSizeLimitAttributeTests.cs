namespace Application.UnitTests;

using System.Security.Claims;
using Api.Filters;
using Common;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class FullBackupRequestSizeLimitAttributeTests
{
    [Theory]
    [InlineData(false, 300)]
    [InlineData(true, 600)]
    public void OnAuthorization_ShouldApplyRoleSpecificRequestLimit(bool isAdmin, long expectedMegabytes)
    {
        var options = Options.Create(new BookImportSecurityOptions
        {
            MaxFullBackupRequestBytes = 300L * 1024 * 1024,
            AdminMaxFullBackupRequestBytes = 600L * 1024 * 1024
        });
        using var services = new ServiceCollection()
            .AddSingleton<IOptions<BookImportSecurityOptions>>(options)
            .BuildServiceProvider();
        var attribute = new FullBackupRequestSizeLimitAttribute();
        var filter = Assert.IsAssignableFrom<IAuthorizationFilter>(attribute.CreateInstance(services));
        var httpContext = new DefaultHttpContext();
        var requestSizeFeature = new MutableRequestSizeFeature();
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(requestSizeFeature);
        if (isAdmin)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, AuthorizationRoles.Admin)],
                "test"));
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var authorizationContext = new AuthorizationFilterContext(
            actionContext,
            [(IFilterMetadata)filter]);

        filter.OnAuthorization(authorizationContext);

        Assert.Equal(expectedMegabytes * 1024 * 1024, requestSizeFeature.MaxRequestBodySize);
    }

    private sealed class MutableRequestSizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }
}
