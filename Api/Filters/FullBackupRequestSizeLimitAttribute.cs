namespace Api.Filters;

using Infrastructure.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class FullBackupRequestSizeLimitAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    public int Order => 900;
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<BookImportSecurityOptions>>();
        return new FullBackupRequestSizeLimitFilter(options.Value, Order);
    }

    private sealed class FullBackupRequestSizeLimitFilter : IAuthorizationFilter, IOrderedFilter
    {
        private readonly BookImportSecurityOptions _options;

        public FullBackupRequestSizeLimitFilter(BookImportSecurityOptions options, int order)
        {
            _options = options;
            Order = order;
        }

        public int Order { get; }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature == null)
            {
                return;
            }

            if (feature.IsReadOnly)
            {
                throw new InvalidOperationException(
                    "The full backup request size limit cannot be changed after reading the request body.");
            }

            feature.MaxRequestBodySize = context.HttpContext.User.IsInRole(AuthorizationRoles.Admin)
                ? _options.AdminMaxFullBackupRequestBytes
                : _options.MaxFullBackupRequestBytes;
        }
    }
}
