using System.Net;
using System.Net.Http.Json;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders");

        group.MapGet("/health", async () => { Console.WriteLine("/api/orders works"); });

        group.MapGet("/", (OrderDbContext db) =>
        {
            return Results.Ok(db.Orders.ToList());
        });

        group.MapGet("/{id:guid}", (Guid id, OrderDbContext db) =>
        {
            var order = db.Orders.FirstOrDefault(x => x.Id == id);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        group.MapGet("/user/{userId}", (string userId, OrderDbContext db) =>
        {
            var orders = db.Orders.Where(x => x.UserId == userId).ToList();
            return Results.Ok(orders);
        });

        group.MapPost("/", async (
        CreateOrderRequest request,
        OrderDbContext db,
        IHttpClientFactory httpClientFactory) =>
    {
        var cartClient = httpClientFactory.CreateClient("CartApi");
        var catalogClient = httpClientFactory.CreateClient("CatalogApi");

        var cartResponse = await cartClient.GetAsync($"/api/cart/{request.UserId}");

        if (cartResponse.StatusCode == HttpStatusCode.NotFound)
            return Results.BadRequest("Cart not found.");

        if (!cartResponse.IsSuccessStatusCode)
            return Results.Problem("CartService request failed.");

        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();

        if (cart is null)
            return Results.Problem("Failed to read cart data.");

        if (cart.Items is null || cart.Items.Count == 0)
            return Results.BadRequest("Cart is empty.");

        var order = new Order
        {
            UserId = request.UserId,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var cartItem in cart.Items)
        {
            var productResponse = await catalogClient.GetAsync($"/api/products/{cartItem.ProductId}");

            if (productResponse.StatusCode == HttpStatusCode.NotFound)
                return Results.BadRequest($"Product {cartItem.ProductId} not found.");

            if (!productResponse.IsSuccessStatusCode)
                return Results.Problem("CatalogService request failed.");

            var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();

            if (product is null)
                return Results.Problem($"Failed to read product {cartItem.ProductId}.");

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = cartItem.Quantity
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        // 🔥 SAVE TO DB
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // 🔥 clear cart 
        foreach (var item in cart.Items)
        {
            await cartClient.DeleteAsync($"/api/cart/{request.UserId}/items/{item.ProductId}");
        }

        return Results.Created($"/api/orders/{order.Id}", order);
    });
    }
}