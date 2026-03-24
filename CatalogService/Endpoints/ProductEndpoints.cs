using System.Text.Json;
using CatalogService.Data;
using CatalogService.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace CatalogService.Endpoints{

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products");

        group.MapGet("/", async (ProductStore store, IDistributedCache cache) =>
        {
            const string cacheKey = "catalog:products:all";

            var cachedJson = await cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedProducts = JsonSerializer.Deserialize<List<Product>>(cachedJson);
                return Results.Ok(cachedProducts);
            }

            var products = store.Products;

            var json = JsonSerializer.Serialize(products);

            await cache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            return Results.Ok(products);
        });

        group.MapGet("/{id:int}", async (int id, ProductStore store, IDistributedCache cache) =>
        {
            var cacheKey = $"catalog:products:{id}";

            var cachedJson = await cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedProduct = JsonSerializer.Deserialize<Product>(cachedJson);
                return Results.Ok(cachedProduct);
            }

            var product = store.Products.FirstOrDefault(p => p.Id == id);

            if (product is null)
                return Results.NotFound();

            var json = JsonSerializer.Serialize(product);

            await cache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });

            return Results.Ok(product);
        });

        group.MapPost("/", async (Product product, ProductStore store, IDistributedCache cache) =>
        {
            var created = store.Add(product);

            await cache.RemoveAsync("catalog:products:all");
            await cache.RemoveAsync($"catalog:products:{product.Id}");

            return Results.Created($"/api/products/{product.Id}", product);
        });
    }
}
}