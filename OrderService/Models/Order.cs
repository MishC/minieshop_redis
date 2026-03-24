namespace OrderService.Models;

public record Order(
    Guid Id,
    string UserId,
    List<OrderItem> Items,
    DateTime CreatedAtUtc
);

//DTO
public record CreateOrderRequest(
    string UserId,
    List<OrderItem> Items
);