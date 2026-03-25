using OrderService.Models;

namespace OrderService.Data;

public class OrderStore
{
    public List<Order> Orders { get; } = new();

    public Order Add(CreateOrderRequest request, List<OrderItem> items, decimal totalAmount)
    {
        var order = new Order(
            Guid.NewGuid(),
            request.UserId,
            request.Email,
            request.Address,
            request.City,
            request.PostalCode,
            request.Country,
            items,
            totalAmount,
            DateTime.UtcNow
        );

        Orders.Add(order);

        return order;
    }
}