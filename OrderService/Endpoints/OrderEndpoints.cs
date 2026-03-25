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

        group.MapGet("/", (OrderStore store) =>
        {
            return Results.Ok(store.Orders);
        });

        group.MapGet("/{id:guid}", (Guid id, OrderStore store) =>
        {
            var order = store.Orders.FirstOrDefault(x => x.Id == id);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        group.MapGet("/user/{userId}", (string userId, OrderStore store) =>
        {
            var orders = store.Orders.Where(x => x.UserId == userId).ToList();
            return Results.Ok(orders);
        });

        group.MapPost("/", async (
            CreateOrderRequest request,
            OrderStore store,
            IHttpClientFactory httpClientFactory) =>
        {
            var cartClient = httpClientFactory.CreateClient("CartApi");
            var catalogClient = httpClientFactory.CreateClient("CatalogApi");

            var cartResponse = await cartClient.GetAsync($"/api/cart/{request.UserId}");

            if (cartResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.BadRequest("Cart not found.");
            }

            if (!cartResponse.IsSuccessStatusCode)
            {
                return Results.Problem("CartService request failed.");
            }

            var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();

            if (cart is null)
            {
                return Results.Problem("Failed to read cart data.");
            }

            if (cart.Items is null || cart.Items.Count == 0)
            {
                return Results.BadRequest("Cart is empty.");
            }

            var orderItems = new List<OrderItem>();

            foreach (var cartItem in cart.Items)
            {
                var productResponse = await catalogClient.GetAsync($"/api/products/{cartItem.ProductId}");

                if (productResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    return Results.BadRequest($"Product with id {cartItem.ProductId} was not found.");
                }

                if (!productResponse.IsSuccessStatusCode)
                {
                    return Results.Problem("CatalogService request failed.");
                }

                var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();

                if (product is null)
                {
                    return Results.Problem($"Failed to read product {cartItem.ProductId}.");
                }

                var lineTotal = product.Price * cartItem.Quantity;

                orderItems.Add(new OrderItem(
                    product.Id,
                    product.Name,
                    product.Price,
                    cartItem.Quantity,
                    lineTotal
                ));
            }

            var totalAmount = orderItems.Sum(x => x.LineTotal);

            var createdOrder = store.Add(request, orderItems, totalAmount);

            foreach (var item in cart.Items)
            {
                await cartClient.DeleteAsync($"/api/cart/{request.UserId}/items/{item.ProductId}");
            }

            return Results.Created($"/api/orders/{createdOrder.Id}", createdOrder);
        });
    }
}