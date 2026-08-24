using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/api/orders/health", () => Results.Ok("/api/orders works"));

        var group = app.MapGroup("/api/orders").RequireAuthorization();

        group.MapGet("/", (OrderDbContext db, ClaimsPrincipal user) =>
        {
            var currentUserId = GetCurrentUserId(user);
            return Results.Ok(db.Orders.Where(x => x.UserId == currentUserId).ToList());
        });

        group.MapGet("/{id:guid}", (Guid id, OrderDbContext db, ClaimsPrincipal user) =>
        {
            var currentUserId = GetCurrentUserId(user);
            var order = db.Orders.FirstOrDefault(x => x.Id == id);
            if (order is not null && order.UserId != currentUserId)
            {
                return Results.Forbid();
            }

            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        group.MapGet("/user/{userId}", (string userId, OrderDbContext db, ClaimsPrincipal user) =>
        {
            if (!CanAccessUserOrders(user, userId))
            {
                return Results.Forbid();
            }

            var orders = db.Orders.Where(x => x.UserId == userId).ToList();
            return Results.Ok(orders);
        });

        group.MapPost("/", async (
        CreateOrderRequest request,
        OrderDbContext db,
        IHttpClientFactory httpClientFactory,
        ClaimsPrincipal user,
        HttpContext httpContext) =>
    {
        if (!CanAccessUserOrders(user, request.UserId))
        {
            return Results.Forbid();
        }

        var cartClient = httpClientFactory.CreateClient("CartApi");
        ForwardAuthContext(httpContext, cartClient);

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

    private static bool CanAccessUserOrders(ClaimsPrincipal user, string userId)
    {
        var currentUserId = GetCurrentUserId(user);
        return string.Equals(currentUserId, userId, StringComparison.Ordinal);
    }

    private static string GetCurrentUserId(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user id is missing.");
    }

    private static void ForwardAuthContext(HttpContext httpContext, HttpClient client)
    {
        if (AuthenticationHeaderValue.TryParse(httpContext.Request.Headers.Authorization, out var authorization))
        {
            client.DefaultRequestHeaders.Authorization = authorization;
        }

        if (!string.IsNullOrEmpty(httpContext.Request.Headers.Cookie))
        {
            client.DefaultRequestHeaders.Add("Cookie", httpContext.Request.Headers.Cookie.ToString());
        }
    }
}
