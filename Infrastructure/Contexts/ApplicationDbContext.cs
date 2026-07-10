namespace Infrastructure.Contexts;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    private readonly IUser _user;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUser user)
        : base(options)
    {
        _user = user;
    }

    public DbSet<Book> Books { get; set; }
    public DbSet<BookCover> BookCovers { get; set; }
    public DbSet<BookTitle> BookTitles { get; set; }
    public DbSet<BookLink> BookLinks { get; set; }
    public DbSet<BookProgressHistory> BookProgressHistory { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<AuthorName> AuthorNames { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Status> Statuses { get; set; }
    public DbSet<ContentType> ContentTypes { get; set; }
    public DbSet<Tag> Tags { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        foreach (var entry in ChangeTracker.Entries<BaseAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Created = DateTimeOffset.UtcNow;
                    entry.Entity.CreatedBy = _user.Id;
                    goto case EntityState.Modified;

                case EntityState.Modified:
                    entry.Entity.LastModified = DateTimeOffset.UtcNow;
                    entry.Entity.LastModifiedBy = _user.Id;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureBook(modelBuilder);
        ConfigureAuthor(modelBuilder);
        ConfigureGenre(modelBuilder);
        ConfigureStatus(modelBuilder);
        ConfigureContentType(modelBuilder);
        ConfigureTag(modelBuilder);
        ConfigureIdentity(modelBuilder);
        SeedSystemDictionaries(modelBuilder);
    }

    private static void ConfigureBook(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasIndex(b => new { b.OwnerId, b.NormalizedPrimaryTitle, b.ContentTypeId }).IsUnique();
            entity.HasIndex(b => new { b.OwnerId, b.CurrentChapterNumber });
            entity.HasIndex(b => new { b.OwnerId, b.Rating });
            entity.HasIndex(b => new { b.OwnerId, b.StatusId });
            entity.HasIndex(b => new { b.OwnerId, b.ContentTypeId });

            entity.Property(b => b.PrimaryTitle).HasMaxLength(500);
            entity.Property(b => b.NormalizedPrimaryTitle).HasMaxLength(500);
            entity.Property(b => b.CurrentChapterLabel).HasMaxLength(100);

            entity.HasOne(b => b.Author)
                .WithMany(a => a.Books)
                .HasForeignKey(b => b.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.ContentType)
                .WithMany(t => t.Books)
                .HasForeignKey(b => b.ContentTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Status)
                .WithMany(s => s.Books)
                .HasForeignKey(b => b.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<User>()
                .WithMany(u => u.Books)
                .HasForeignKey(b => b.OwnerId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookCover>(entity =>
        {
            entity.HasIndex(c => c.BookId).IsUnique();
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(c => c.Source).HasConversion<string>().HasMaxLength(32);
            entity.Property(c => c.StoragePath).HasMaxLength(500);
            entity.Property(c => c.OriginalImageUrl).HasMaxLength(2000);
            entity.Property(c => c.MimeType).HasMaxLength(100);
            entity.Property(c => c.FailureReason).HasMaxLength(1000);
            entity.HasOne(c => c.Book)
                .WithOne(b => b.Cover)
                .HasForeignKey<BookCover>(c => c.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookTitle>(entity =>
        {
            entity.HasIndex(t => t.NormalizedTitle);
            entity.HasIndex(t => new { t.BookId, t.NormalizedTitle }).IsUnique();
            entity.Property(t => t.Title).HasMaxLength(500);
            entity.Property(t => t.NormalizedTitle).HasMaxLength(500);
            entity.Property(t => t.Language).HasMaxLength(10);
            entity.Property(t => t.Source).HasMaxLength(50);
            entity.HasOne(t => t.Book)
                .WithMany(b => b.Titles)
                .HasForeignKey(t => t.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookLink>(entity =>
        {
            entity.Property(l => l.Url).HasMaxLength(2000);
            entity.Property(l => l.Label).HasMaxLength(200);
            entity.Property(l => l.SourceType).HasMaxLength(50);
            entity.HasOne(l => l.Book)
                .WithMany(b => b.Links)
                .HasForeignKey(l => l.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookProgressHistory>(entity =>
        {
            entity.HasIndex(h => new { h.BookId, h.ChangedAt });
            entity.Property(h => h.ChapterLabel).HasMaxLength(100);
            entity.HasOne(h => h.Book)
                .WithMany(b => b.ProgressHistory)
                .HasForeignKey(h => h.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BookGenre>(entity =>
        {
            entity.HasKey(e => new { e.BookId, e.GenreId });
            entity.HasOne(e => e.Book)
                .WithMany(b => b.BookGenres)
                .HasForeignKey(e => e.BookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Genre)
                .WithMany(g => g.BookGenres)
                .HasForeignKey(e => e.GenreId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BookTag>(entity =>
        {
            entity.HasKey(e => new { e.BookId, e.TagId });
            entity.HasOne(e => e.Book)
                .WithMany(b => b.BookTags)
                .HasForeignKey(e => e.BookId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tag)
                .WithMany(t => t.BookTags)
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureAuthor(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasIndex(a => a.NormalizedPrimaryName).IsUnique();
            entity.Property(a => a.PrimaryName).HasMaxLength(300);
            entity.Property(a => a.NormalizedPrimaryName).HasMaxLength(300);
        });

        modelBuilder.Entity<AuthorName>(entity =>
        {
            entity.HasIndex(a => a.NormalizedName).IsUnique();
            entity.Property(a => a.Name).HasMaxLength(300);
            entity.Property(a => a.NormalizedName).HasMaxLength(300);
            entity.Property(a => a.Language).HasMaxLength(10);
            entity.Property(a => a.Source).HasMaxLength(50);
            entity.HasOne(a => a.Author)
                .WithMany(a => a.Names)
                .HasForeignKey(a => a.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGenre(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.HasIndex(g => g.NormalizedName).IsUnique();
            entity.Property(g => g.Name).HasMaxLength(100);
            entity.Property(g => g.NormalizedName).HasMaxLength(100);
        });
    }

    private static void ConfigureStatus(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Status>(entity =>
        {
            entity.HasIndex(s => s.Slug).IsUnique();
            entity.Property(s => s.Name).HasMaxLength(100);
            entity.Property(s => s.Slug).HasMaxLength(100);
        });
    }

    private static void ConfigureContentType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentType>(entity =>
        {
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.Property(t => t.Name).HasMaxLength(100);
            entity.Property(t => t.Slug).HasMaxLength(100);
        });
    }

    private static void ConfigureTag(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(t => new { t.OwnerId, t.NormalizedName }).IsUnique();
            entity.Property(t => t.Name).HasMaxLength(100);
            entity.Property(t => t.NormalizedName).HasMaxLength(100);
            entity.Property(t => t.Color).HasMaxLength(32);
            entity.HasOne<User>()
                .WithMany(u => u.OwnedTags)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.Property(t => t.TokenHash).HasMaxLength(200);
            entity.Property(t => t.ReplacedByTokenHash).HasMaxLength(200);
            entity.Property(t => t.ReasonRevoked).HasMaxLength(200);
            entity.HasOne<User>()
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void SeedSystemDictionaries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentType>().HasData(
            new ContentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Novel", Slug = "novel" },
            new ContentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Manga", Slug = "manga" },
            new ContentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Manhwa", Slug = "manhwa" },
            new ContentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Manhua", Slug = "manhua" },
            new ContentType { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Name = "Other", Slug = "other" }
        );

        modelBuilder.Entity<Status>().HasData(
            new Status { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Name = "Reading", Slug = "reading", SortOrder = 10 },
            new Status { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Name = "Completed", Slug = "completed", SortOrder = 20 },
            new Status { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Name = "Plan To Read", Slug = "plan-to-read", SortOrder = 30 },
            new Status { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Name = "On Hold", Slug = "on-hold", SortOrder = 40 },
            new Status { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Name = "Dropped", Slug = "dropped", SortOrder = 50 },
            new Status { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), Name = "Unknown", Slug = "unknown", SortOrder = 60 }
        );
    }
}
