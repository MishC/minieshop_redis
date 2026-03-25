using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.Email).IsRequired();
            entity.Property(o => o.UserId).IsRequired();
            entity.Property(o => o.Address).IsRequired();
            entity.Property(o => o.City).IsRequired();
            entity.Property(o => o.PostalCode).IsRequired();
            entity.Property(o => o.Country).IsRequired();

            entity.Property(o => o.TotalAmount).HasColumnType("numeric(18,2)");

            entity.HasMany(o => o.Items)
                  .WithOne(i => i.Order)
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.ProductName).IsRequired();
            entity.Property(i => i.UnitPrice).HasColumnType("numeric(18,2)");
        });
    }
}