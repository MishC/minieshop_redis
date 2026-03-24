namespace OrderService.Models;

public record OrderItem(
    int ProductId,
    int Quantity,
    decimal UnitPrice
);