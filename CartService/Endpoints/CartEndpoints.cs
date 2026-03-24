using System.Text.Json;
using CartService.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace CartService.Endpoints{

public static class CartEndpoints
{
    public static void MapCartEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cart");

        group.MapGet("/{userId}", async (string userId, IDistributedCache cache) =>
        {
            var cacheKey = $"cart:{userId}";
            var cached = await cache.GetStringAsync(cacheKey);

            Cart cart;

            if (string.IsNullOrEmpty(cached))
            {
                cart = new Cart(userId, new List<CartItem>());

                var emptyJson = JsonSerializer.Serialize(cart);
                await cache.SetStringAsync(
                    cacheKey,
                    emptyJson,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                    });
            }
            else
            {
                cart = JsonSerializer.Deserialize<Cart>(cached)
                    ?? new Cart(userId, new List<CartItem>());
            }

            return Results.Ok(cart);
        });

        group.MapPost("/{userId}/items", async (string userId, CartItem item, IDistributedCache cache) =>
        {
            var cacheKey = $"cart:{userId}";
            var cached = await cache.GetStringAsync(cacheKey);

            var cart = string.IsNullOrEmpty(cached)
                ? new Cart(userId, new List<CartItem>())
                : JsonSerializer.Deserialize<Cart>(cached) ?? new Cart(userId, new List<CartItem>());

            var existingItem = cart.Items.FirstOrDefault(x => x.ProductId == item.ProductId);

            if (existingItem is not null)
            {
                cart.Items.Remove(existingItem);
                cart.Items.Add(existingItem with
                {
                    Quantity = existingItem.Quantity + item.Quantity
                });
            }
            else
            {
                cart.Items.Add(item);
            }

            var json = JsonSerializer.Serialize(cart);

            await cache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

            return Results.Ok(cart);
        });

        group.MapDelete("/{userId}/items/{productId:int}", async (string userId, int productId, IDistributedCache cache) =>
        {
            var cacheKey = $"cart:{userId}";
            var cached = await cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(cached))
                return Results.NotFound();

            var cart = JsonSerializer.Deserialize<Cart>(cached);

            if (cart is null)
                return Results.Problem("Failed to deserialize cart.");

            var item = cart.Items.FirstOrDefault(x => x.ProductId == productId);

            if (item is null)
                return Results.NotFound();

            cart.Items.Remove(item);

            var json = JsonSerializer.Serialize(cart);

            await cache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });

            return Results.Ok(cart);
        });
    }
}
}