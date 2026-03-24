using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders");

        group.MapPost("/", async (CreateOrderRequest request, OrderStore store, IDistributedCache cache) =>
        {
            var order = new Order(
                Guid.NewGuid(),
                request.UserId,
                request.Items,
                DateTime.UtcNow
            );

            store.Orders.Add(order);

            await cache.RemoveAsync("orders:all");
            await cache.RemoveAsync($"orders:user:{request.UserId}");
            await cache.RemoveAsync($"orders:{order.Id}");

            return Results.Created($"/api/orders/{order.Id}", order);
        });

        group.MapGet("/{id:guid}", async (Guid id, OrderStore store, IDistributedCache cache) =>
        {
            var cacheKey = $"orders:{id}";
            var cached = await cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cached))
            {
                var cachedOrder = JsonSerializer.Deserialize<Order>(cached);
                return Results.Ok(cachedOrder);
            }

            var order = store.Orders.FirstOrDefault(x => x.Id == id);

            if (order is null)
                return Results.NotFound();

            var json = JsonSerializer.Serialize(order);

            await cache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });

            return Results.Ok(order);
        });

        group.MapGet("/user/{userId}", async (string userId, OrderStore store, IDistributedCache cache) =>
        {
            var cacheKey = $"orders:user:{userId}";
            var cached = await cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cached))
            {
                var cachedOrders = JsonSerializer.Deserialize<List<Order>>(cached);
                return Results.Ok(cachedOrders);
            }

            var orders = store.Orders
                .Where(x => x.UserId == userId)
                .ToList();

            var json = JsonSerializer.Serialize(orders);

            await cache.SetStringAsync(
                cacheKey,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });

            return Results.Ok(orders);
        });
   
    }
}

