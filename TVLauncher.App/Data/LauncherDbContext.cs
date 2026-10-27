using Microsoft.EntityFrameworkCore;
using TVLauncher.App.Models;

namespace TVLauncher.App.Data;

public sealed class LauncherDbContext(DbContextOptions<LauncherDbContext> options) : DbContext(options)
{
    public DbSet<LauncherApp> Apps => Set<LauncherApp>();
    public DbSet<FeaturedTitle> Featured => Set<FeaturedTitle>();
    public DbSet<NewsCacheEntry> NewsCache => Set<NewsCacheEntry>();
    public DbSet<AutoFeaturedTitle> AutoFeatured => Set<AutoFeaturedTitle>();
    public DbSet<FeaturedViewEvent> FeaturedViews => Set<FeaturedViewEvent>();
    public DbSet<CatalogSyncCheckpoint> CatalogSyncCheckpoints => Set<CatalogSyncCheckpoint>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<LauncherApp>().ToTable("aplicativos");
        builder.Entity<LauncherApp>().HasIndex(x => x.SortOrder);
        builder.Entity<FeaturedTitle>().ToTable("destaques");
        builder.Entity<FeaturedTitle>().HasIndex(x => x.SortOrder);
        builder.Entity<AutoFeaturedTitle>().ToTable("destaques_automaticos");
        builder.Entity<AutoFeaturedTitle>().HasIndex(x => x.ExternalKey).IsUnique();
        builder.Entity<CatalogSyncCheckpoint>().ToTable("catalogo_consultas");
        builder.Entity<FeaturedViewEvent>().ToTable("destaques_exibicoes");
        builder.Entity<FeaturedViewEvent>().HasIndex(x => x.ShownAtUnix);
        builder.Entity<NewsCacheEntry>().ToTable("noticias_cache");
        builder.Entity<NewsCacheEntry>().HasIndex(x => x.Url).IsUnique();
    }
}
