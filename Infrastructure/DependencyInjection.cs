namespace Infrastructure;

using Amazon.Runtime;
using Amazon.S3;
using Application.Common.Interfaces;
using Infrastructure.Authentication;
using Infrastructure.BookCovers;
using Infrastructure.Caching;
using Infrastructure.Identity;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        
        var keyString = jwtSettings["Key"];
        if (keyString == null)
        {
            throw new ArgumentNullException(nameof(keyString));
        }
        var key = Encoding.UTF8.GetBytes(keyString);

        var connectionString = builder.Configuration.GetConnectionString("DB");
        var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
        });
        builder.Services.AddDistributedMemoryCache();
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
        }
        builder.Services.AddScoped<IBookRepository, BookRepository>();
        builder.Services.AddScoped<BookSearchCriteriaApplier>();
        builder.Services.AddScoped<BookSortBuilder>();
        builder.Services.AddScoped<BookListProjectionQuery>();
        builder.Services.AddScoped<IBookListQueryService, BookReadQueryService>();
        builder.Services.AddScoped<IBookExportQueryService, BookExportQueryService>();
        builder.Services.AddScoped<IBookSummaryQueryService, BookSummaryQueryService>();
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
        builder.Services.AddScoped<IAdminLibraryService, AdminLibraryService>();

        builder.Services.Configure<BookCoverOptions>(builder.Configuration.GetSection("BookCovers"));
        var s3Options = builder.Configuration.GetSection("BookCovers:S3").Get<BookCoverS3Options>();
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
                    AuthenticationRegion = string.IsNullOrWhiteSpace(s3Options.Region) ? "garage" : s3Options.Region
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
        builder.Services.AddSingleton<IBookCoverQueue>(provider => provider.GetRequiredService<InMemoryBookCoverQueue>());
        builder.Services.AddScoped<BookCoverResolver>();
        builder.Services.AddScoped<BookCoverProcessor>();
        builder.Services.AddHostedService<BookCoverBackgroundService>();
        builder.Services.AddHttpClient<IBookCoverProvider, BookLinkMetadataCoverProvider>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovelkiBackend/1.0");
        });
        builder.Services.AddHttpClient<IBookCoverProvider, AniListBookCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://graphql.anilist.co");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovelkiBackend/1.0");
        });
        builder.Services.AddHttpClient<IBookCoverProvider, JikanBookCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.jikan.moe");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovelkiBackend/1.0");
        });
        builder.Services.AddHttpClient<IBookCoverProvider, GoogleBooksCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovelkiBackend/1.0");
        });
        builder.Services.AddHttpClient<IBookCoverProvider, OpenLibraryCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://openlibrary.org");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovelkiBackend/1.0");
        });
        builder.Services.AddHttpClient<IBookCoverProvider, WikidataCoverProvider>(client =>
        {
            client.BaseAddress = new Uri("https://query.wikidata.org");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovelkiBackend/1.0");
        });
        builder.Services.AddHttpClient("BookCoverImages", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NovelkiBackend/1.0");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUser, CurrentUser>();

        builder.Services.Configure<JwtSettings>(
            builder.Configuration.GetSection("Jwt"));
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
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.Zero
            };
        });

        builder.Services.AddAuthorization();
    }
}
