using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
namespace OrderService.Models;

public class OrderItem
{
    public int Id { get; set; }

    public Guid OrderId { get; set; }
    [ValidateNever]
    [JsonIgnore]
    public Order Order { get; set; } = default!;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;

}