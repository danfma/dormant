using Microsoft.EntityFrameworkCore;

namespace Dormant.Benchmarks.Model;

/// <summary>
/// EF Core context bound to the same physical table Dormant's generator creates (<c>bench_product</c>,
/// lowercase columns). EF never owns the schema — no <c>EnsureCreated</c>/migrations; it reads and writes
/// rows on the table Dormant's <c>EnsureCreatedAsync</c> already created. Reads use <c>AsNoTracking</c> in
/// the benchmarks; writes use the normal tracked <c>SaveChanges</c> path.
/// </summary>
public sealed class BenchDbContext(DbContextOptions<BenchDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("bench_product");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.Name).HasColumnName("name");
            entity.Property(p => p.Category).HasColumnName("category");
            entity.Property(p => p.Price).HasColumnName("price");
            entity.Property(p => p.Quantity).HasColumnName("quantity");
        });
    }
}
