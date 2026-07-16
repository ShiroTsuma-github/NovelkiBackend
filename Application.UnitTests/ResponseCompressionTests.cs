using System.Reflection;
using Api;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Application.UnitTests;

public class ResponseCompressionTests
{
    [Fact]
    public void AddWebServices_ShouldConfigureResponseCompressionForApiPayloads()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        Type dependencyInjection = typeof(Program).Assembly.GetType("Api.DependencyInjection")
                                   ?? throw new InvalidOperationException(
                                       "Api dependency injection type was not found.");
        MethodInfo addWebServices = dependencyInjection.GetMethod(
                                        "AddWebServices",
                                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? throw new InvalidOperationException(
                                        "AddWebServices extension method was not found.");

        addWebServices.Invoke(null, [builder]);

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        ResponseCompressionOptions options = provider.GetRequiredService<IOptions<ResponseCompressionOptions>>().Value;
        BrotliCompressionProviderOptions brotliOptions =
            provider.GetRequiredService<IOptions<BrotliCompressionProviderOptions>>().Value;
        GzipCompressionProviderOptions gzipOptions =
            provider.GetRequiredService<IOptions<GzipCompressionProviderOptions>>().Value;
        IEnumerable<Type> compressionProviderTypes = options.Providers
            .Cast<object>()
            .Select(compressionProviderFactory => compressionProviderFactory
                .GetType()
                .GetProperty("ProviderType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(compressionProviderFactory))
            .OfType<Type>();

        Assert.True(options.EnableForHttps);
        Assert.Contains(typeof(BrotliCompressionProvider), compressionProviderTypes);
        Assert.Contains(typeof(GzipCompressionProvider), compressionProviderTypes);
        Assert.Contains("application/json", options.MimeTypes);
        Assert.Contains("application/problem+json", options.MimeTypes);
        Assert.Equal(System.IO.Compression.CompressionLevel.Fastest, brotliOptions.Level);
        Assert.Equal(System.IO.Compression.CompressionLevel.Fastest, gzipOptions.Level);
    }
}
