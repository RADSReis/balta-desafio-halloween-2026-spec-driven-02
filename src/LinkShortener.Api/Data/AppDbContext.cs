using LinkShortener.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LinkShortener.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // O SQLite não guarda o DateTimeKind; as datas são sempre UTC.
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public DbSet<Link> Links => Set<Link>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<Link>();

        link.HasKey(l => l.Id);
        link.Property(l => l.Code).HasMaxLength(20).IsRequired();
        link.Property(l => l.TargetUrl).HasMaxLength(2048).IsRequired();
        link.Property(l => l.CreatedAtUtc).HasConversion(UtcConverter);
        link.Property(l => l.ExpiresAtUtc).HasConversion(UtcConverter);

        // D-04: unicidade garantida pelo banco, não só pelo serviço.
        link.HasIndex(l => l.Code).IsUnique();
        link.HasIndex(l => l.TargetUrl);
    }
}
