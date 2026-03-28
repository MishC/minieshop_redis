namespace OrderService.Models;

public class Order
{

    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string City { get; set; } = default!;
    public string PostalCode { get; set; } = default!;
    public string Country { get; set; } = default!;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;


    public List<OrderItem> Items { get; set; } = new();
}
//DTO - from client
public record CreateOrderRequest(
    string UserId,
    string Email,
    string Address,
    string City,
    string PostalCode,
    string Country
);

public record CartResponse(
    string UserId,
    List<CartItemResponse> Items
);