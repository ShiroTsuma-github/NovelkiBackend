namespace Infrastructure;

using Infrastructure.Authentication;
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
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        builder.Services.AddScoped<IBookRepository, BookRepository>();
        builder.Services.AddScoped<IAuthorRepository, AuthorRepository>();
        builder.Services.AddScoped<IGenreRepository, GenreRepository>();
        builder.Services.AddScoped<IStatusRepository, StatusRepository>();
        builder.Services.AddScoped<ITypeRepository, TypeRepository>();
        builder.Services.AddScoped<ITagRepository, TagRepository>();

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
