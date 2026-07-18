namespace Infrastructure;

using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Authentication;
using BookCovers;
using Caching;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Services;

public static class DependencyInjection
{
    private const string JwtSection = "Jwt";
    private const string JwtKey = "Key";
    private const string JwtIssuer = "Issuer";
    private const string JwtAudience = "Audience";
    private const string DatabaseConnectionString = "DB";
    private const string RedisConnectionString = "Redis";
    private const string BookCoversSection = "BookCovers";
    private const string BookCoverS3Section = "BookCovers:S3";
    private const string DefaultS3Region = "garage";

    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var jwtSettings = builder.Configuration.GetSection(JwtSection);

        var keyString = jwtSettings[JwtKey];
        if (keyString == null)
        {
            throw new ArgumentNullException(nameof(keyString));
        }

        var key = Encoding.UTF8.GetBytes(keyString);

        var connectionString = builder.Configuration.GetConnectionString(DatabaseConnectionString);
        var redisConnectionString = builder.Configuration.GetConnectionString(RedisConnectionString);
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString,
                npgsqlOptions => { npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery); });
        });
        builder.Services.AddDistributedMemoryCache();
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConnectionString; });
        }

        builder.Services.AddScoped<IBookRepository, BookRepository>();
        builder.Services.AddScoped<BookSearchCriteriaApplier>();
        builder.Services.AddScoped<BookSortBuilder>();
        builder.Services.AddScoped<BookListProjectionQuery>();
        builder.Services.AddScoped<IBookListQueryService, BookReadQueryService>();
        builder.Services.AddScoped<IBookExportQueryService, BookExportQueryService>();
        builder.Services.AddScoped<IBookSummaryQueryService, BookSummaryQueryService>();
        builder.Services.AddScoped<IBookAnalyticsQueryService, BookAnalyticsQueryService>();
        builder.Services.AddScoped<IAuthorRepository, AuthorRepository>();
        builder.Services.AddScoped<IGenreRepository, GenreRepository>();
        builder.Services.AddScoped<IStatusRepository, StatusRepository>();
        builder.Services.AddScoped<ITypeRepository, TypeRepository>();
        builder.Services.AddScoped<ITagRepository, TagRepository>();
        builder.Services.AddScoped<IBookCoverRepository, BookCoverRepository>();
        builder.Services.AddScoped<BookListCache>();
        builder.Services.AddScoped<IBookListCache>(provider => provider.GetRequiredService<BookListCache>());
        builder.Services.AddScoped<IBookListCacheInvalidator>(provider => provider.GetRequiredService<BookListCache>());
        builder.Services.AddScoped<IBookCsvImportService, BookCsvImportService>();
        builder.Services.AddOptions<BookImportSecurityOptions>()
            .Bind(builder.Configuration.GetSection(BookImportSecurityOptions.SectionName))
            .Validate(options => options.MaxArchiveEntries > 0 &&
                                 options.MaxCsvRows > 0 &&
                                 options.MaxManifestBooks > 0 &&
                                 options.MaxCsvBytes > 0 &&
                                 options.MaxManifestBytes > 0 &&
                                 options.MaxCoverBytes > 0 &&
                                 options.MaxUncompressedArchiveBytes > 0 &&
                                 options.MaxCompressionRatio >= 1 &&
                                 options.SuspiciousCompressionRatio > options.MaxCompressionRatio &&
                                 options.SuspiciousCompressionMinimumBytes > 0 &&
                                 options.SuspiciousCompressionMinimumBytes <= options.MaxUncompressedArchiveBytes &&
                                 options.SuspiciousAccountBlockDuration > TimeSpan.Zero &&
                                 options.MaxConcurrentFullImportOperations > 0 &&
                                 options.MaxActiveSessionsGlobal > 0 &&
                                 options.MaxActiveSessionsPerUser > 0 &&
                                 options.MaxActiveFullSessionsGlobal > 0 &&
                                 options.MaxActiveFullSessionsPerUser > 0 &&
                                 options.MaxActiveFullSessionsGlobal <= options.MaxActiveSessionsGlobal &&
                                 options.MaxActiveFullSessionsPerUser <= options.MaxActiveSessionsPerUser &&
                                 options.MaxStagedBytesGlobal >= options.MaxUncompressedArchiveBytes &&
                                 options.SessionIdleTimeout > TimeSpan.Zero &&
                                 options.SessionAbsoluteLifetime >= options.SessionIdleTimeout &&
                                 options.CleanupInterval > TimeSpan.Zero &&
                                 options.DraftProcessingTimeout > TimeSpan.Zero &&
                                 options.FinalizeProcessingTimeout > TimeSpan.Zero,
                "Book import security limits are invalid.")
            .ValidateOnStart();
        builder.Services.AddSingleton<BookImportSessionStore>();
        builder.Services.AddSingleton<BookImportConcurrencyGate>();
        builder.Services.AddSingleton<AccountAbuseGuard>();
        builder.Services.AddHostedService<BookImportSessionCleanupService>();
        builder.Services.AddScoped<IAdminLibraryService, AdminLibraryService>();

        builder.Services.AddOptions<BookCoverOptions>()
            .Bind(builder.Configuration.GetSection(BookCoversSection))
            .Validate(options => options.MaxBytes > 0 && options.MaxWidth > 0 && options.MaxHeight > 0 &&
                                 options.MaxPixels > 0 &&
                                 options.MaxPixels <= (long)options.MaxWidth * options.MaxHeight,
                "Book cover security limits are invalid.")
            .ValidateOnStart();
        var s3Options = builder.Configuration.GetSection(BookCoverS3Section).Get<BookCoverS3Options>();
        if (!string.IsNullOrWhiteSpace(s3Options?.Endpoint) &&
            !string.IsNullOrWhiteSpace(s3Options.AccessKey) &&
            !string.IsNullOrWhiteSpace(s3Options.SecretKey) &&
            !string.IsNullOrWhiteSpace(s3Options.Bucket))
        {
            builder.Services.AddSingleton<IAmazonS3>(_ =>
            {
                var credentials = new BasicAWSCredentials(s3Options.AccessKey, s3Options.SecretKey);
                var config = new AmazonS3Config
                {
                    ServiceURL = s3Options.Endpoint,
                    ForcePathStyle = true,
                    AuthenticationRegion = string.IsNullOrWhiteSpace(s3Options.Region)
                        ? DefaultS3Region
                        : s3Options.Region
                };
                return new AmazonS3Client(credentials, config);
            });
            builder.Services.AddScoped<IBookCoverStorage, S3BookCoverStorage>();
        }
        else
        {
            builder.Services.AddScoped<IBookCoverStorage, LocalBookCoverStorage>();
        }

        builder.Services.AddScoped<IBookCoverRemoteImageService, BookCoverRemoteImageService>();
        builder.Services.AddSingleton<InMemoryBookCoverQueue>();
        builder.Services.AddSingleton<IBookCoverQueue>(provider =>
            provider.GetRequiredService<InMemoryBookCoverQueue>());
        builder.Services.AddScoped<BookCoverResolver>();
        builder.Services.AddScoped<BookCoverProcessor>();
        builder.Services.AddHostedService<BookCoverBackgroundService>();
        builder.Services.AddHttpClient<IBookCoverProvider, BookLinkMetadataCoverProvider>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BookCoverOptions.DefaultUserAgent);
        });
        builder.Services.AddHttpClient<IBookCoverProvider, AniListBookCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://graphql.anilist.co");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BookCoverOptions.DefaultUserAgent);
        });
        builder.Services.AddHttpClient<IBookCoverProvider, JikanBookCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.jikan.moe");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BookCoverOptions.DefaultUserAgent);
        });
        builder.Services.AddHttpClient<IBookCoverProvider, GoogleBooksCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BookCoverOptions.DefaultUserAgent);
        });
        builder.Services.AddHttpClient<IBookCoverProvider, OpenLibraryCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://openlibrary.org");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BookCoverOptions.DefaultUserAgent);
        });
        builder.Services.AddHttpClient<IBookCoverProvider, WikidataCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://query.wikidata.org");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BookCoverOptions.DefaultUserAgent);
        });
        builder.Services.AddHttpClient(BookCoverHttpClients.Images,
                client => { client.DefaultRequestHeaders.UserAgent.ParseAdd(BookCoverOptions.DefaultUserAgent); })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUser, CurrentUser>();

        builder.Services.Configure<JwtSettings>(
            builder.Configuration.GetSection(JwtSection));
        builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddHostedService<AdminRoleSeeder>();
        builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings[JwtIssuer],
                    ValidAudience = jwtSettings[JwtAudience],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddAuthorization();
    }
}
