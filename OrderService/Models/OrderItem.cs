namespace OrderService.Models;

public record OrderItem(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal
);