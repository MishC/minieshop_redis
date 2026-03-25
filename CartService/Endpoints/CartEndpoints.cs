using System.Text.Json;
using System.Net.Http.Json;
using CartService.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace CartService.Endpoints
{

    public static class CartEndpoints
    {
        public static void MapCartEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/cart");

            group.MapGet("/health", async () => { Console.WriteLine("/api/cart works"); });

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

            group.MapPost("/{userId}/items", async (
               string userId,
               CartItem item,
               IDistributedCache cache,
               IHttpClientFactory httpClientFactory) =>
           {
               if (item.Quantity <= 0)
               {
                   return Results.BadRequest("Quantity must be greater than zero.");
               }

               var client = httpClientFactory.CreateClient("CatalogApi");
               var response = await client.GetAsync($"/api/products/{item.ProductId}");

               if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
               {
                   return Results.BadRequest("Product not found.");
               }

               if (!response.IsSuccessStatusCode)
               {
                   return Results.Problem("CatalogService request failed.");
               }

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

            group.MapDelete("/{userId}/items/{productId:int}", async (
     string userId,
     int productId,
     IDistributedCache cache) =>
 {
     var cacheKey = $"cart:{userId}";
     var cached = await cache.GetStringAsync(cacheKey);

     if (string.IsNullOrEmpty(cached))
         return Results.NotFound();

     var cart = JsonSerializer.Deserialize<Cart>(cached);

     if (cart is null)
         return Results.Problem("Failed to deserialize cart.");

     //var item = cart.Items.FirstOrDefault(x => x.ProductId == productId);
    var index = cart.Items.FindIndex(x => x.ProductId == productId);

    var item = cart.Items[index];


     if (item is null)
         return Results.NotFound();


     if (index == -1)
         return Results.NotFound();


     if (item.Quantity > 1)
     {
         cart.Items[index] = item with
         {
             Quantity = item.Quantity - 1
         };
     }
     else
     {
         cart.Items.RemoveAt(index);
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
        }
    }
}