using OrderService.Models;

namespace OrderService.Data;

public class OrderStore
{
    public List<Order> Orders { get; } = new();

    public Order Add(CreateOrderRequest request, List<OrderItem> items, decimal totalAmount)
    {
        var order = new Order
        {
            UserId = request.UserId,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
            CreatedAtUtc = DateTime.UtcNow,
            TotalAmount = totalAmount,
            Items = items
        };

        Orders.Add(order);

        return order;
    }
}




