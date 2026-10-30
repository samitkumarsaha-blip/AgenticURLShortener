using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Infrastructure;

public sealed class ShortUrlDbContext(DbContextOptions<ShortUrlDbContext> options) : DbContext(options)
{
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
    public DbSet<ClickEvent> ClickEvents => Set<ClickEvent>();
    public DbSet<WorkflowRunRecord> WorkflowRuns => Set<WorkflowRunRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShortUrl>();
        entity.HasKey(shortUrl => shortUrl.Code);
        entity.Property(shortUrl => shortUrl.Code).HasMaxLength(16);
        entity.Property(shortUrl => shortUrl.Alias).HasMaxLength(64);
        entity.HasIndex(shortUrl => shortUrl.Alias).IsUnique();
        entity.Property(shortUrl => shortUrl.Destination).HasConversion(uri => uri.ToString(), value => new Uri(value));

        var click = modelBuilder.Entity<ClickEvent>();
        click.HasKey(clickEvent => clickEvent.Id);
        click.Property(clickEvent => clickEvent.ShortCode).HasMaxLength(16).IsRequired();
        click.HasIndex(clickEvent => new { clickEvent.ShortCode, clickEvent.OccurredAt });
        click.HasOne<ShortUrl>().WithMany().HasForeignKey(clickEvent => clickEvent.ShortCode).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkflowRunRecord>().HasKey(record => record.Id);
        modelBuilder.Entity<WorkflowRunRecord>().Property(record => record.Payload).IsRequired();
    }
}