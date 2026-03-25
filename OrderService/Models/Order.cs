namespace OrderService.Models;

public record Order(
    Guid Id,
    string UserId,
    string Email,
    string Address,
    string City,
    string PostalCode,
    string Country,
    List<OrderItem> Items,
    decimal TotalAmount,
    DateTime CreatedAtUtc
);
//DTO - from client
public record CreateOrderRequest(
    string UserId,
    string Email,
    string Address,
    string City,
    string PostalCode,
    string Country
);
public record ProductResponse(
    int Id,
    string Name,
    decimal Price
);

public record CartItemResponse(
    int ProductId,
    int Quantity
);

public record CartResponse(
    string UserId,
    List<CartItemResponse> Items
);