namespace Infrastructure.Contexts;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Type = Domain.Entities.Type;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    private readonly IUser _user;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUser user)
        : base(options)
    {
        _user = user;
    }

    public DbSet<Book> Books { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Status> Statuses { get; set; }
    public DbSet<Type> Types { get; set; }


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

        modelBuilder.Entity<Book>()
            .HasOne(b => b.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AuthorId);

        modelBuilder.Entity<Book>()
            .HasOne(b => b.Type)
            .WithMany(t => t.Books)
            .HasForeignKey(b => b.TypeId);

        modelBuilder.Entity<Book>()
            .HasOne(b => b.Status)
            .WithMany(s => s.Books)
            .HasForeignKey(b => b.StatusId);

        modelBuilder.Entity<Book>()
            .HasMany(b => b.Genres)
            .WithMany(g => g.Books);

        modelBuilder.Entity<Book>()
            .HasOne<User>()
            .WithMany(u => u.Books)
            .HasForeignKey(b => b.OwnerId)
            .IsRequired(true);

        modelBuilder.Entity<BookTagAssociation>(entity =>
        {
            entity.HasKey(e => new { e.BookId, e.TagId, e.OwnerId });

            entity.HasOne(e => e.Book)
                .WithMany(b => b.BookTags)
                .HasForeignKey(e => e.BookId);

            entity.HasOne(e => e.Tag)
                .WithMany(t => t.BookAssociations)
                .HasForeignKey(e => e.TagId);

            entity.HasOne<User>()
                .WithMany( u => u.TagAssociations)
                .HasForeignKey(e => e.OwnerId);
        });

        modelBuilder.Entity<Tag>()
            .HasOne<User>()
            .WithMany(u => u.OwnedTags)
            .HasForeignKey(t => t.OwnerId);
    }
}
