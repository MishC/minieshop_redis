using CatalogService.Models;

namespace CatalogService.Data;

public class ProductStore
{
    public List<Product> Products { get; set; } = new()
    {
        new(1, "Laptop", 1200),
        new(2, "Mouse", 25),
        new(3, "Keyboard", 60)
    };
}