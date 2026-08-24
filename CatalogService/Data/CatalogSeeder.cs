using System.Text.Json;
using CatalogService.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Data;

public static class CatalogSeeder
{
    public static async Task SeedAsync(CatalogDbContext db, IWebHostEnvironment environment)
    {
        if (await db.Products.AnyAsync())
        {
            return;
        }

        var seedPath = Path.Combine(environment.ContentRootPath, "Data", "catalog.seed.json");
        await using var stream = File.OpenRead(seedPath);
        var products = await JsonSerializer.DeserializeAsync<List<Product>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (products is null || products.Count == 0)
        {
            return;
        }

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }
}
