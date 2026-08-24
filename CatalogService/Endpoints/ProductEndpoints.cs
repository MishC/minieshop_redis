using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CatalogService.Data;
using CatalogService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CatalogService.Endpoints{

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products");

        group.MapGet("/health", () => Results.Ok("/api/products works"));

        group.MapGet("/", async (CatalogDbContext db) =>
        {
            var products = await db.Products
                .OrderBy(p => p.Id)
                .ToListAsync();
            return Results.Ok(products);
        });

        group.MapGet("/recently-viewed", async (CatalogDbContext db, ClaimsPrincipal user, IDistributedCache cache) =>
        {
            var productIds = await GetRecentlyViewedProductIdsAsync(cache, GetCurrentSessionId(user));

            if (productIds.Count == 0)
            {
                return Results.Ok(Array.Empty<Product>());
            }

            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var orderedProducts = productIds
                .Select(id => products.FirstOrDefault(p => p.Id == id))
                .Where(product => product is not null)
                .ToList();

            return Results.Ok(orderedProducts);
        }).RequireAuthorization();

        group.MapGet("/RecentViews", async (CatalogDbContext db, ClaimsPrincipal user, IDistributedCache cache) =>
        {
            var productIds = await GetRecentlyViewedProductIdsAsync(cache, GetCurrentSessionId(user));

            if (productIds.Count == 0)
            {
                return Results.Ok(Array.Empty<Product>());
            }

            var products = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var orderedProducts = productIds
                .Select(id => products.FirstOrDefault(p => p.Id == id))
                .Where(product => product is not null)
                .ToList();

            return Results.Ok(orderedProducts);
        }).RequireAuthorization();

        group.MapGet("/{id:int}", async (int id, CatalogDbContext db, ClaimsPrincipal user, IDistributedCache cache) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null)
            {
                return Results.NotFound();
            }

            if (user.Identity?.IsAuthenticated == true)
            {
                await SaveRecentlyViewedProductAsync(cache, GetCurrentSessionId(user), product.Id);
            }

            return Results.Ok(product);
        });

        group.MapPost("/", async (ProductCreateDto dto, CatalogDbContext db) =>
        {
            var product = new Product
            {
                Name = dto.Name,
                Price = dto.Price
            };

            db.Products.Add(product);
            await db.SaveChangesAsync();

            return Results.Created($"/api/products/{product.Id}", product);
        });
    }

    private static async Task<List<int>> GetRecentlyViewedProductIdsAsync(IDistributedCache cache, string userId)
    {
        var cachedJson = await cache.GetStringAsync(GetRecentlyViewedKey(userId));
        return string.IsNullOrEmpty(cachedJson)
            ? new List<int>()
            : JsonSerializer.Deserialize<List<int>>(cachedJson) ?? new List<int>();
    }

    private static async Task SaveRecentlyViewedProductAsync(IDistributedCache cache, string userId, int productId)
    {
        var productIds = await GetRecentlyViewedProductIdsAsync(cache, userId);

        productIds.Remove(productId);
        productIds.Insert(0, productId);
        productIds = productIds.Take(10).ToList();

        await cache.SetStringAsync(
            GetRecentlyViewedKey(userId),
            JsonSerializer.Serialize(productIds),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            });
    }

    private static string GetRecentlyViewedKey(string sessionId)
    {
        return $"catalog:recently-viewed:{sessionId}";
    }

    private static string GetCurrentSessionId(ClaimsPrincipal user)
    {
        return user.FindFirstValue(JwtRegisteredClaimNames.Sid)
            ?? user.FindFirstValue(ClaimTypes.Sid)
            ?? throw new InvalidOperationException("Authenticated session id is missing.");
    }
}
}
