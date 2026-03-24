using OrderService.Models;

namespace OrderService.Data;

public class OrderStore
{
    public List<Order> Orders { get; set; } = new();
}