using CatalogService.Models;

namespace CatalogService.Data;

public class ProductStore
{
    private int _nextId = 4;

    public List<Product> Products { get; set; } = new()
    {
        new(1, "Laptop", 1200),
        new(2, "Mouse", 25),
        new(3, "Keyboard", 60)
    };

    public Product Add(Product product)
    {
        var newProduct = product with { Id = _nextId++ };
        Products.Add(newProduct);
        return newProduct;
    }
}