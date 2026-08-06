using Microsoft.EntityFrameworkCore;
using S4HERP.Api.Models;

namespace S4HERP.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Sku).IsUnique();
            entity.Property(p => p.UnitPrice).HasPrecision(18, 2);
            entity.Property(p => p.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        });
    }
}
